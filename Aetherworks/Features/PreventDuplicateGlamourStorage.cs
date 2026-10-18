using System.Runtime.InteropServices;
using Aetherworks.Glamour;
using Aetherworks.Ownership;
using Aetherworks.Tweaks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

public sealed unsafe class PreventDuplicateGlamourStorage(
    IAddonLifecycle lifecycle, IGameInteropProvider interop, IGameGui gui, IChatGui chat,
    IPluginLog log, OwnershipTracker tracker, AppearanceCatalog catalog) : ITweak
{
    private const string AddonName = "MiragePrismPrismBoxCrystallize";
    [return: MarshalAs(UnmanagedType.U1)]
    private delegate bool StoreOutfit(MirageManager* self, uint setOrIndex, InventoryType* containers, ushort* slots);
    private Hook<StoreOutfit>? newOutfit;
    private Hook<StoreOutfit>? existingOutfit;
    private readonly HashSet<int> disabledRows = [];
    private nint modifiedAddon;
    private nint modifiedConfirmation;
    private uint modifiedConfirmationId;
    private bool enabled;
    private bool errorLogged;
    private long nextNotice;

    public string Id => "prevent-duplicate-glamour-storage";
    public string Name => "Prevent Duplicate Glamour Storage";
    public string Description => "Prevent dresser and armoire deposits of appearances already collected. Close matches remain allowed. Uses remembered ownership when storage is unavailable.";
    public string Category => "Items and storage";

    public void Enable()
    {
        if (enabled) return;
        newOutfit = interop.HookFromAddress<StoreOutfit>((nint)MirageManager.MemberFunctionPointers.StoreNewOutfit, OnNewOutfit);
        existingOutfit = interop.HookFromAddress<StoreOutfit>((nint)MirageManager.MemberFunctionPointers.StoreExistingOutfit, OnExistingOutfit);
        lifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, OnDraw);
        lifecycle.RegisterListener(AddonEvent.PreRefresh, AddonName, OnReset);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, OnReset);
        lifecycle.RegisterListener(AddonEvent.PreDraw, "SelectYesno", OnCabinetConfirmation);
        lifecycle.RegisterListener(AddonEvent.PreRefresh, "SelectYesno", OnConfirmationReset);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, "SelectYesno", OnConfirmationReset);
        newOutfit.Enable();
        existingOutfit.Enable();
        enabled = true;
    }

    public void Disable()
    {
        enabled = false;
        lifecycle.UnregisterListener(OnDraw);
        lifecycle.UnregisterListener(OnReset);
        lifecycle.UnregisterListener(OnCabinetConfirmation);
        lifecycle.UnregisterListener(OnConfirmationReset);
        newOutfit?.Dispose();
        newOutfit = null;
        existingOutfit?.Dispose();
        existingOutfit = null;
        var addon = gui.GetAddonByName(AddonName);
        if (!addon.IsNull && addon.Address == modifiedAddon) RestoreRows((AddonMiragePrismPrismBoxCrystallize*)addon.Address);
        disabledRows.Clear();
        modifiedAddon = 0;
        var confirmation = gui.GetAddonByName("SelectYesno");
        if (!confirmation.IsNull) RestoreConfirmation((AddonSelectYesno*)confirmation.Address);
        modifiedConfirmation = 0;
        errorLogged = false;
    }

    private void RestoreConfirmation(AddonSelectYesno* addon)
    {
        if ((nint)addon == modifiedConfirmation && addon->Id == modifiedConfirmationId && addon->YesButton != null)
            addon->YesButton->SetEnabledState(true);
        modifiedConfirmation = 0;
    }

    private void OnConfirmationReset(AddonEvent type, AddonArgs args)
    {
        try { RestoreConfirmation((AddonSelectYesno*)args.Addon.Address); }
        catch (Exception ex) { Report(ex); }
    }

    private void OnCabinetConfirmation(AddonEvent type, AddonArgs args)
    {
        if (!enabled) return;
        try
        {
            var addon = (AddonSelectYesno*)args.Addon.Address;
            var wasBlocked = modifiedConfirmation == args.Addon.Address && modifiedConfirmationId == addon->Id;
            RestoreConfirmation(addon);
            var cabinet = AgentCabinet.Instance();
            // The armoire withdrawal agent is separate. Only this agent's own deposit
            // confirmation in normal mode is eligible; never touch unrelated Yes/No dialogs.
            if (!tracker.HasCharacter || cabinet == null || cabinet->OpenMode != 0 ||
                cabinet->ConfirmationAddonId == 0 || cabinet->ConfirmationAddonId != addon->Id ||
                addon->YesButton == null || !addon->YesButton->IsEnabled) return;
            var conflict = DuplicateGlamourProtection.FindConflict(ItemUtil.GetBaseId(cabinet->SelectedItemId).ItemId,
                tracker.ReadCollectedForProtection(), catalog);
            if (conflict is null) return;
            modifiedConfirmation = args.Addon.Address;
            modifiedConfirmationId = addon->Id;
            addon->YesButton->SetEnabledState(false);
            if (!wasBlocked) Notice($"Aetherworks: Item already collected in {conflict.Location}; armoire storage blocked.");
        }
        catch (Exception ex) { Report(ex); }
    }

    private void OnReset(AddonEvent type, AddonArgs args)
    {
        try { RestoreRows((AddonMiragePrismPrismBoxCrystallize*)args.Addon.Address); }
        catch (Exception ex) { Report(ex); }
        disabledRows.Clear();
        modifiedAddon = 0;
    }

    private void RestoreRows(AddonMiragePrismPrismBoxCrystallize* addon)
    {
        if ((nint)addon != modifiedAddon || addon->ItemTreeList == null) return;
        var list = addon->ItemTreeList;
        foreach (var index in disabledRows)
            if (index >= 0 && index < list->Items.Count)
            {
                var item = list->GetItem(index);
                if (item == null) continue;
                item->State &= ~TreeListItemState.Disabled;
                if (item->Renderer != null) item->Renderer->AtkComponentButton.SetEnabledState(true);
            }
        disabledRows.Clear();
    }

    private void OnDraw(AddonEvent type, AddonArgs args)
    {
        if (!enabled) return;
        try
        {
            var addon = (AddonMiragePrismPrismBoxCrystallize*)args.Addon.Address;
            RestoreRows(addon);
            if (modifiedAddon != args.Addon.Address) disabledRows.Clear();
            modifiedAddon = args.Addon.Address;
            if (!tracker.HasCharacter || addon->ItemTreeList == null) return;
            var agent = AgentMiragePrismPrismBox.Instance();
            if (agent == null || agent->Data == null || !agent->IsDataLoaded) return;
            var store = tracker.ReadCollectedForProtection();
            // Localized labels are the exposed link between tree rows and crystallize entries.
            // If different entries share a label, disable it only when every candidate is a duplicate.
            var candidates = agent->Data->CrystallizeItems.ToArray()
                .Take(Math.Min(140, (int)agent->Data->CrystallizeItemCount))
                .Where(i => i.ItemId != 0).Select(i => ItemUtil.GetBaseId(i.ItemId).ItemId)
                .Distinct().Select(id => (Id: id, Appearance: catalog.Get(id)))
                .Where(i => i.Appearance is not null).GroupBy(i => i.Appearance!.Name)
                .Where(g => g.All(i => DuplicateGlamourProtection.FindConflict(i.Id, store, catalog) is not null))
                .Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
            var list = addon->ItemTreeList;
            if (list->Items.Count > 200) return;
            for (var index = 0; index < list->Items.Count; index++)
            {
                var item = list->GetItem(index);
                if (item == null || (item->Type & (TreeListItemType.Group | TreeListItemType.SectionHeader)) != 0 ||
                    item->StringValues.Count == 0 || (item->State & TreeListItemState.Disabled) != 0) continue;
                var label = item->StringValues[0].ToString().Replace(" \uE03C", "");
                if (!candidates.Contains(label)) continue;
                // Never undo the game's own disabled state when this tweak is turned off.
                if (item->Renderer != null && !item->Renderer->AtkComponentButton.IsEnabled) continue;
                disabledRows.Add(index);
                item->State |= TreeListItemState.Disabled;
                if (item->Renderer != null) item->Renderer->AtkComponentButton.SetEnabledState(false);
            }
        }
        catch (Exception ex) { Report(ex); }
    }

    private bool OnNewOutfit(MirageManager* self, uint set, InventoryType* containers, ushort* slots) =>
        !BlockOutfit(containers, slots) && newOutfit!.Original(self, set, containers, slots);

    private bool OnExistingOutfit(MirageManager* self, uint index, InventoryType* containers, ushort* slots) =>
        !BlockOutfit(containers, slots) && existingOutfit!.Original(self, index, containers, slots);

    private bool BlockOutfit(InventoryType* containers, ushort* slots)
    {
        if (!enabled || !tracker.HasCharacter || containers == null || slots == null) return false;
        try
        {
            var manager = InventoryManager.Instance();
            if (manager == null) return false;
            var incoming = new List<(uint ItemId, bool AlreadyStored)>();
            // Eleven columns in MirageStoreSetItem, as specified by the native store functions.
            for (var i = 0; i < 11; i++)
            {
                var container = containers[i];
                // Invalid/special sources represent absent or already-stored outfit pieces.
                // Only real carried gear introduces a new appearance into the dresser.
                if (container is not (InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4) &&
                    !container.ToString().StartsWith("Armory", StringComparison.Ordinal)) continue;
                var inventory = manager->GetInventoryContainer(container);
                if (inventory == null || !inventory->IsLoaded || slots[i] >= inventory->Size) continue;
                var item = inventory->GetInventorySlot(slots[i]);
                if (item != null && item->ItemId != 0)
                    incoming.Add((ItemUtil.GetBaseId(item->ItemId).ItemId, false));
            }
            var conflict = DuplicateGlamourProtection.FindDepositConflict(incoming, tracker.ReadCollectedForProtection(), catalog);
            if (conflict is null) return false;
            Notice($"Aetherworks: Outfit not stored — an exact appearance is already in {conflict.Location}. Remove the duplicate from the hand-in selection.");
            return true;
        }
        catch (Exception ex) { Report(ex); return false; }
    }

    private void Report(Exception ex)
    {
        if (errorLogged) return;
        errorLogged = true;
        log.Error(ex, "Duplicate glamour protection could not inspect the current storage action");
        Notice("Aetherworks: Duplicate glamour protection could not check this action. See the plugin log.");
    }

    private void Notice(string message)
    {
        if (Environment.TickCount64 < nextNotice) return;
        nextNotice = Environment.TickCount64 + 5000;
        // Failure to display feedback must never turn an established block into permission to deposit.
        try { chat.PrintError(message); }
        catch (Exception ex) { log.Error(ex, "Could not display glamour protection feedback"); }
    }
}
