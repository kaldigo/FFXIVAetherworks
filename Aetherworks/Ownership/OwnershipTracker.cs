using System.Security.Cryptography;
using System.Text;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Sheets = Lumina.Excel.Sheets;

namespace Aetherworks.Ownership;

/// <summary>Always-on shared service. Feature toggles control presentation, not collection.</summary>
public sealed class OwnershipTracker : IDisposable
{
    public static readonly (string Id, string Label)[] SupportedLocations =
        [("inventory", "Inventory"), ("saddlebag", "Saddlebag")];
    private readonly IFramework framework;
    private readonly IPlayerState player;
    private readonly IGameGui gui;
    private readonly IPluginLog log;
    private readonly IDataManager data;
    private readonly Dictionary<uint, uint[]> outfits;
    private readonly (uint Row, uint Item)[] cabinetItems;
    private readonly HashSet<uint> cabinetItemIds;
    private readonly StableObservation<List<(uint ItemId, long Quantity)>> retainerObservation = new();
    private readonly StableObservation<List<(uint ItemId, long Quantity)>> dresserObservation = new();
    private readonly StableObservation<List<(uint ItemId, long Quantity)>> armoireObservation = new();
    private readonly StableObservation<Dictionary<string, string>> rosterObservation = new();
    private readonly StableObservation<List<(uint ItemId, long Quantity)>> chestObservation = new();
    private readonly string directory;
    private ulong character;
    private string? path;
    private long nextScan;
    private long nextSave;
    private long saddlebagReadyAt;
    private bool loadFailed;
    private bool disposed;
    private bool scanFailureLogged;
    private readonly Dictionary<string, OwnershipStore> pendingSaves = new();
    public OwnershipStore Store { get; private set; } = new();
    public bool IncludeFcChest { get; set; }
    public ulong CurrentFreeCompanyId { get; private set; }
    private string? persistenceError;
    private readonly HashSet<string> failedCollectors = new();
    public string? Error => persistenceError ?? (failedCollectors.Count == 0 ? null :
        $"Could not refresh {string.Join(", ", failedCollectors)}. Previous snapshots are retained; retrying automatically.");
    public bool HasCharacter => character != 0 && character == player.ContentId;

    public OwnershipTracker(IDalamudPluginInterface pi, IFramework framework, IPlayerState player, IGameGui gui, IPluginLog log, IDataManager data)
    {
        this.framework = framework;
        this.player = player;
        this.gui = gui;
        this.log = log;
        this.data = data;
        outfits = data.GetExcelSheet<Sheets.MirageStoreSetItem>().ToDictionary(r => r.RowId, r => new[]
        {
            r.MainHand.RowId, r.OffHand.RowId, r.Head.RowId, r.Body.RowId, r.Hands.RowId, r.Legs.RowId,
            r.Feet.RowId, r.Earrings.RowId, r.Necklace.RowId, r.Bracelets.RowId, r.Ring.RowId,
        });
        cabinetItems = data.GetExcelSheet<Sheets.Cabinet>().Where(r => r.Item.RowId != 0)
            .Select(r => (r.RowId, r.Item.RowId)).ToArray();
        cabinetItemIds = cabinetItems.Select(r => r.Item).ToHashSet();
        directory = Path.Combine(pi.GetPluginConfigDirectory(), "ownership");
        framework.Update += Update;
    }

    private void Update(IFramework _)
    {
        try
        {
            var now = Environment.TickCount64;
            if (character != player.ContentId) SwitchCharacter(player.ContentId);
            if (!HasCharacter || now < nextScan) return;
            nextScan = now + 500;
            Scan();
            failedCollectors.Remove("ownership tracking");
            scanFailureLogged = false;
            if (now >= nextSave) { Save(); nextSave = now + 5000; }
        }
        catch (Exception ex)
        {
            failedCollectors.Add("ownership tracking");
            if (!scanFailureLogged) log.Error(ex, "Ownership scan failed");
            scanFailureLogged = true;
        }
    }

    private void SwitchCharacter(ulong id)
    {
        Save();
        character = id;
        Store = new();
        persistenceError = null;
        failedCollectors.Clear();
        path = null;
        loadFailed = false;
        saddlebagReadyAt = 0;
        retainerObservation.Reset();
        dresserObservation.Reset();
        armoireObservation.Reset();
        rosterObservation.Reset();
        chestObservation.Reset();
        CurrentFreeCompanyId = 0;
        nextScan = nextSave = 0;
        if (id == 0) return;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString(System.Globalization.CultureInfo.InvariantCulture))))[..24];
        path = Path.Combine(directory, key + ".json");
        if (pendingSaves.Remove(path, out var pending)) { Store = pending; return; }
        try { if (File.Exists(path)) Store.Load(File.ReadAllText(path)); }
        catch (Exception ex)
        {
            // Preserve the unreadable file rather than silently replacing user data.
            loadFailed = true;
            persistenceError = "Saved ownership data could not be read. It has been preserved; new observations will not be saved this session.";
            log.Error(ex, "Loading ownership snapshot failed");
        }
    }

    private unsafe void Scan()
    {
        ScanPart("inventory", () => ReadLocation("inventory", [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4, InventoryType.Crystals]));
        ScanPart("retainers", ScanRetainers, () => { retainerObservation.Reset(); rosterObservation.Reset(); });
        ScanPart("glamour dresser", ScanDresser, dresserObservation.Reset);
        ScanPart("armoire", ScanArmoire, armoireObservation.Reset);
        ScanPart("FC chest", ScanFcChest, () => { chestObservation.Reset(); CurrentFreeCompanyId = 0; });
        var addon = gui.GetAddonByName("InventoryBuddy");
        if (addon.IsNull || !addon.IsVisible) { saddlebagReadyAt = 0; return; }
        // Allow the newly opened storage UI to settle; also require both native containers loaded.
        if (saddlebagReadyAt == 0) saddlebagReadyAt = Environment.TickCount64 + 1000;
        if (Environment.TickCount64 >= saddlebagReadyAt)
            ScanPart("saddlebag", () => ReadLocation("saddlebag", [InventoryType.SaddleBag1, InventoryType.SaddleBag2]));
    }

    private void ScanPart(string name, Action scan, Action? reset = null)
    {
        try { scan(); failedCollectors.Remove(name); }
        catch (Exception ex)
        {
            reset?.Invoke();
            if (failedCollectors.Add(name)) log.Error(ex, $"Ownership collector failed: {name}");
        }
    }

    private void ReadLocation(string id, InventoryType[] containers)
    {
        var items = ReadContainers(containers);
        if (items is not null) Store.Replace(id, items);
    }

    private unsafe List<(uint ItemId, long Quantity)>? ReadContainers(InventoryType[] containers)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return null;
        var items = new List<(uint, long)>();
        foreach (var type in containers)
        {
            var container = manager->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded || container->Size <= 0) return null;
            for (var slot = 0; slot < container->Size; slot++)
            {
                var item = container->GetInventorySlot(slot);
                if (item == null) return null;
                if (item->ItemId != 0 && item->Quantity > 0)
                    items.Add((ItemUtil.GetBaseId(item->ItemId).ItemId, item->Quantity));
            }
        }
        // Commit only after every container was available, never a partial inventory.
        return items;
    }

    public IEnumerable<(string Id, string Label)> LocationsFor(uint itemId)
    {
        foreach (var location in Store.ItemStorageLocations(IncludeFcChest, CurrentFreeCompanyId)) yield return location;
        if (data.GetExcelSheet<Sheets.Item>().TryGetRow(itemId, out var item) && item.EquipSlotCategory.RowId != 0)
            yield return ("dresser", "Glamour Dresser");
        if (cabinetItemIds.Contains(itemId)) yield return ("armoire", "Armoire");
    }

    // Protection must see newly deposited/withdrawn pieces before the periodic snapshot settles.
    // Use loaded native storage when available; retain remembered ownership for unavailable storage.
    public unsafe OwnershipStore ReadCollectedForProtection()
    {
        var result = new OwnershipStore();
        if (!HasCharacter) return result;
        foreach (var id in new[] { "dresser", "armoire" })
            if (Store.Snapshot.Locations.TryGetValue(id, out var items))
                result.Replace(id, items.Select(i => (i.Key, i.Value)));
        var manager = MirageManager.Instance();
        if (manager != null && manager->PrismBoxLoaded && Visible("MiragePrismPrismBox"))
            result.Replace("dresser", DresserContents.Expand(
                manager->PrismBoxItemIds.ToArray().Select(id => ItemUtil.GetBaseId(id).ItemId).ToArray(), outfits,
                (slot, part) => manager->IsSetSlotUnlocked((uint)slot, part)));
        var state = UIState.Instance();
        if (state != null && state->Cabinet.IsCabinetLoaded())
            result.Replace("armoire", cabinetItems.Where(i => state->Cabinet.IsItemInCabinet(i.Row)).Select(i => (i.Item, 1L)));
        return result;
    }

    private bool Visible(string name)
    {
        var addon = gui.GetAddonByName(name);
        return !addon.IsNull && addon.IsVisible;
    }

    private unsafe void ScanFcChest()
    {
        var info = InfoModule.Instance();
        var company = InfoProxyFreeCompany.Instance();
        // Never reuse a prior character/company's cached containers when identity is unavailable.
        CurrentFreeCompanyId = info != null && info->LocalContentId == character && company != null ? company->Id : 0;
        var page = FcChestAccess.CurrentPage(gui);
        if (CurrentFreeCompanyId == 0 || page is null) { chestObservation.Reset(); return; }
        var items = ReadContainers([(InventoryType)page.Value]);
        if (items is null) { chestObservation.Reset(); return; }
        var key = OwnershipStore.FcChestKey(CurrentFreeCompanyId, (int)(page.Value - (uint)InventoryType.FreeCompanyPage1 + 1));
        if (chestObservation.Accept(key, items, Environment.TickCount64, (a, b) => a.SequenceEqual(b)))
            Store.Replace(key, items);
    }

    private unsafe void ScanRetainers()
    {
        var manager = RetainerManager.Instance();
        if (manager == null || !manager->IsReady) { rosterObservation.Reset(); retainerObservation.Reset(); return; }
        if (Visible("RetainerList"))
        {
            var roster = new Dictionary<string, string>();
            foreach (ref var retainer in manager->Retainers)
                if (retainer.RetainerId != 0 && !string.IsNullOrWhiteSpace(retainer.NameString))
                    roster[OwnershipStore.RetainerKey(retainer.RetainerId)] = retainer.NameString;
            if (roster.Count == manager->GetRetainerCount() && rosterObservation.Accept("roster", roster, Environment.TickCount64,
                (a, b) => a.Count == b.Count && a.All(r => b.TryGetValue(r.Key, out var name) && name == r.Value)))
                Store.ReplaceRetainers(roster);
        }
        else rosterObservation.Reset();

        var order = ItemOrderModule.Instance();
        var active = manager->GetActiveRetainer();
        if (order == null || order->ActiveRetainerId == 0 || active == null ||
            active->RetainerId != order->ActiveRetainerId ||
            !(Visible("InventoryRetainer") || Visible("InventoryRetainerLarge")))
        { retainerObservation.Reset(); return; }
        var key = OwnershipStore.RetainerKey(active->RetainerId);
        // Only collect named retainers from this character's observed roster.
        if (!Store.Snapshot.Retainers.ContainsKey(key)) { retainerObservation.Reset(); return; }
        var items = ReadContainers([InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3,
            InventoryType.RetainerPage4, InventoryType.RetainerPage5, InventoryType.RetainerPage6, InventoryType.RetainerPage7,
            InventoryType.RetainerCrystals]);
        if (items is null) { retainerObservation.Reset(); return; }
        if (retainerObservation.Accept(key, items, Environment.TickCount64, (a, b) => a.SequenceEqual(b)))
            Store.Replace(key, items);
    }

    private unsafe void ScanDresser()
    {
        var manager = MirageManager.Instance();
        if (!Visible("MiragePrismPrismBox") || manager == null || !manager->PrismBoxLoaded)
        { dresserObservation.Reset(); return; }
        var slots = manager->PrismBoxItemIds.ToArray().Select(id => ItemUtil.GetBaseId(id).ItemId).ToArray();
        var items = DresserContents.Expand(slots, outfits, (slot, part) => manager->IsSetSlotUnlocked((uint)slot, part));
        if (dresserObservation.Accept("dresser", items, Environment.TickCount64, (a, b) => a.SequenceEqual(b)))
            Store.Replace("dresser", items);
    }

    private unsafe void ScanArmoire()
    {
        var state = UIState.Instance();
        if (state == null || !state->Cabinet.IsCabinetLoaded()) { armoireObservation.Reset(); return; }
        var items = new List<(uint ItemId, long Quantity)>();
        foreach (var (row, item) in cabinetItems)
            if (state->Cabinet.IsItemInCabinet(row)) items.Add((item, 1));
        if (armoireObservation.Accept("armoire", items, Environment.TickCount64, (a, b) => a.SequenceEqual(b)))
            Store.Replace("armoire", items);
    }

    private void Save()
    {
        if (path is not null && Store.Dirty && !loadFailed) pendingSaves[path] = Store;
        if (pendingSaves.Count == 0) return;
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var (savePath, store) in pendingSaves.ToArray())
            {
                var temporary = savePath + ".tmp";
                File.WriteAllText(temporary, store.Serialize());
                File.Move(temporary, savePath, true);
                store.MarkSaved();
                pendingSaves.Remove(savePath);
            }
            if (!loadFailed) persistenceError = null;
        }
        catch (Exception ex)
        {
            if (persistenceError != "Ownership snapshots could not be saved. Retrying automatically.")
                log.Error(ex, "Saving ownership snapshots failed");
            persistenceError = "Ownership snapshots could not be saved. Retrying automatically.";
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        framework.Update -= Update;
        Save();
    }
}
