using Aetherworks.Transfers;
using Aetherworks.Tweaks;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Sheets = Lumina.Excel.Sheets;

namespace Aetherworks.Features;

public sealed unsafe class SaddlebagWithdrawLeaveOne(IContextMenu menus, IFramework framework,
    IGameGui gui, IPlayerState player, IDataManager data, IPluginLog log, IChatGui chat) : ITweak
{
    private bool enabled;
    private bool openingNativeMenu;
    private bool waitingForQuantity;
    private nint numericAddress;
    private uint numericId;
    private long deadline;
    private LeaveOneWithdrawal? pending;
    private string quantityLabel = "";
    private string quantityPrompt = "";

    public const string FeatureId = "saddlebag-withdraw-leave-one";
    public string Id => FeatureId;
    public string Name => "Saddlebag Withdraw All but One";
    public string Description => "Add Withdraw all but one to saddlebag item menus. Leave one in the selected stack so Entrust Duplicates can put matching items back.";
    public string Category => "Items and storage";

    public void Enable()
    {
        if (enabled) return;
        var sheet = data.GetExcelSheet<Sheets.Addon>();
        // Saddlebag menu: 887 = Remove All from Saddlebag; 888 = Remove from Saddlebag.
        // The latter opens 889 (How many do you want to remove?). Retainer labels
        // 773/914 do not occur on this native path.
        quantityLabel = sheet.GetRow(888).Text.ExtractText();
        quantityPrompt = sheet.GetRow(889).Text.ExtractText();
        menus.OnMenuOpened += OnMenuOpened;
        framework.Update += Update;
        enabled = true;
    }

    public void Disable()
    {
        enabled = false;
        menus.OnMenuOpened -= OnMenuOpened;
        framework.Update -= Update;
        pending = null;
        waitingForQuantity = false;
    }

    private bool Visible(InventoryType type)
    {
        var addon = gui.GetAddonByName("InventoryBuddy");
        if (addon.IsNull || !addon.IsVisible || player.ContentId == 0) return false;
        var premium = type is InventoryType.PremiumSaddleBag1 or InventoryType.PremiumSaddleBag2;
        return ((AddonInventoryBuddy*)addon.Address)->TabIndex == (premium ? 1 : 0);
    }

    private static bool IsSaddlebag(InventoryType type) => type is InventoryType.SaddleBag1 or
        InventoryType.SaddleBag2 or InventoryType.PremiumSaddleBag1 or InventoryType.PremiumSaddleBag2;

    private static InventoryItem* Read(InventoryType type, int slot)
    {
        if (!IsSaddlebag(type)) return null;
        var manager = InventoryManager.Instance();
        if (manager == null) return null;
        var container = manager->GetInventoryContainer(type);
        if (container == null || !container->IsLoaded || slot < 0 || slot >= container->Size) return null;
        var item = container->GetInventorySlot(slot);
        return item == null || item->IsSymbolic ? null : item;
    }

    private bool Unchanged(LeaveOneWithdrawal request)
    {
        var type = (InventoryType)request.Container;
        if (!Visible(type)) return false;
        var item = Read(type, request.Slot);
        return item != null && request.Matches(player.ContentId, item->ItemId, (byte)item->Flags, item->Quantity);
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!enabled || openingNativeMenu || pending != null || args.MenuType != ContextMenuType.Inventory ||
            args.AddonName != "InventoryBuddy") return;
        try
        {
            var agent = AgentInventoryContext.Instance();
            if (agent == null || !IsSaddlebag(agent->TargetInventoryId) || !Visible(agent->TargetInventoryId)) return;
            var item = Read(agent->TargetInventoryId, agent->TargetInventorySlotId);
            if (item == null || item->ItemId == 0 || item->Quantity <= 1) return;
            var request = new LeaveOneWithdrawal(player.ContentId, (uint)agent->TargetInventoryId,
                agent->TargetInventorySlotId, item->ItemId, (byte)item->Flags, item->Quantity);
            args.AddMenuItem(new MenuItem
            {
                Name = "Withdraw all but one",
                OnClicked = _ =>
                {
                    if (!enabled || pending != null || !Unchanged(request)) return;
                    // Let Dalamud finish closing this menu before opening the game's quantity action.
                    pending = request;
                    waitingForQuantity = false;
                    deadline = Environment.TickCount64 + 2000;
                },
            });
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Update(IFramework _)
    {
        if (!enabled || pending is not { } request) return;
        try
        {
            if (!Unchanged(request)) { pending = null; return; }
            if (Environment.TickCount64 >= deadline)
            { Stop("The saddlebag withdrawal timed out. No automatic retry was sent."); return; }
            if (!waitingForQuantity)
            {
                var numeric = gui.GetAddonByName("InputNumeric");
                var context = gui.GetAddonByName("ContextMenu");
                if (!numeric.IsNull && numeric.IsVisible) { pending = null; return; }
                // Native context-menu closing can take more than one framework update.
                if (!context.IsNull && context.IsVisible) return;
                waitingForQuantity = true;
                if (!OpenQuantity(request)) Stop("Could not open Remove from Saddlebag. No items were withdrawn.");
                return;
            }
            var addon = gui.GetAddonByName("InputNumeric");
            if (addon.IsNull || !addon.IsVisible) return;
            if (addon.Address != numericAddress || addon.Id != numericId) { pending = null; return; }
            var input = (AddonInputNumeric*)addon.Address;
            if (input->AtkValuesCount < 7 || input->AtkValues[2].Type != AtkValueType.UInt ||
                input->AtkValues[3].Type != AtkValueType.UInt ||
                !TextEquals(input->AtkValues[6], quantityPrompt))
            { Stop("The saddlebag quantity dialog was not recognized. No automatic amount was submitted."); return; }
            var amount = request.Amount;
            pending = null; // Exactly one submission; never retry a rejected or partially completed move.
            if (amount < input->AtkValues[2].UInt || amount > input->AtkValues[3].UInt)
            { Stop("The requested amount is unavailable. Check inventory space or use the quantity dialog manually."); return; }
            log.Debug("Removing {Amount} from saddlebag {Container}, slot {Slot}, leaving one", amount, request.Container, request.Slot);
            input->FireCallbackInt(amount);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private bool OpenQuantity(LeaveOneWithdrawal request)
    {
        var agent = AgentInventoryContext.Instance();
        var owner = gui.GetAddonByName("InventoryBuddy");
        if (agent == null || owner.IsNull) return false;
        openingNativeMenu = true;
        try { agent->OpenForItemSlot((InventoryType)request.Container, request.Slot, 0, owner.Id); }
        finally { openingNativeMenu = false; }
        var addon = gui.GetAddonByName("ContextMenu");
        if (addon.IsNull || !addon.IsVisible) return false;
        var menu = (AddonContextMenu*)addon.Address;
        if (agent->TargetInventoryId == (InventoryType)request.Container && agent->TargetInventorySlotId == request.Slot &&
            menu->AtkValuesCount > 0)
        {
            var count = Math.Min((int)menu->AtkValues[0].UInt, 32);
            for (var index = 0; index < count; index++)
            {
                var eventIndex = agent->ContexItemStartIndex + index;
                if (eventIndex < 0 || eventIndex >= agent->EventParams.Length || 8 + index >= menu->AtkValuesCount ||
                    agent->IsContextItemDisabled(index) || !TextEquals(agent->EventParams[eventIndex], quantityLabel) ||
                    !TextEquals(menu->AtkValues[8 + index], quantityLabel)) continue;
                var values = stackalloc AtkValue[3];
                values[0].SetInt(0);
                values[1].SetInt(index);
                values[2].SetInt(0);
                menu->FireCallback(3, values, true);
                var numeric = gui.GetAddonByName("InputNumeric");
                if (numeric.IsNull || !numeric.IsVisible) return false;
                numericAddress = numeric.Address;
                numericId = numeric.Id;
                log.Debug("Opened saddlebag removal quantity dialog from native menu entry {Index}", index);
                return true;
            }
        }
        menu->Close(true);
        return false;
    }

    private static bool TextEquals(AtkValue value, string text) =>
        value.Type is AtkValueType.String or AtkValueType.ManagedString &&
        value.String.AsReadOnlySeStringSpan().ExtractText() == text;

    private void Fail(Exception ex)
    {
        pending = null;
        log.Error(ex, "Saddlebag withdraw all but one failed");
        chat.PrintError("[Aetherworks] Could not withdraw from the saddlebag. See the plugin log.");
    }

    private void Stop(string reason)
    {
        pending = null;
        log.Warning("Saddlebag withdraw all but one: {Reason}", reason);
        chat.PrintError($"[Aetherworks] {reason}");
    }
}
