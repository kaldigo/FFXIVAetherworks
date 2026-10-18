using Aetherworks.Windows;
using Aetherworks.Transfers;
using Aetherworks.Tweaks;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

public sealed class SaddlebagEntrustDuplicates(IFramework framework,
    IGameGui gui, IPlayerState player, IDataManager data, IPluginLog log) : ITweak
{
    private readonly DuplicateTransfer transfer = new();
    private readonly LiveTransferInventory inventoryReader = new(data);
    private bool enabled;
    private NativeEntrustButtons? buttons;
    private bool errorLogged;
    private ulong character;
    private long nextMove;
    private long readyAt;
    public string Id => "saddlebag-entrust-duplicates";
    public string Name => "Saddlebag Entrust Duplicates";
    public string Description => "Add an Entrust Duplicates button beside the regular saddlebag. Move matching stackable inventory items, preserving HQ/NQ stack compatibility and available space.";
    public string Category => "Items and storage";

    public void Enable()
    {
        if (enabled) return;
        buttons = new NativeEntrustButtons("InventoryBuddy", ButtonState, Click, log);
        buttons.Enable();
        framework.Update += Update;
        enabled = true;
    }

    public void Disable()
    {
        enabled = false;
        buttons?.Dispose();
        buttons = null;
        framework.Update -= Update;
        transfer.Stop("");
        readyAt = 0;
        character = 0;
        errorLogged = false;
    }

    private unsafe bool Visible()
    {
        var addon = gui.GetAddonByName("InventoryBuddy");
        return player.ContentId != 0 && !addon.IsNull && addon.IsVisible &&
            ((AddonInventoryBuddy*)addon.Address)->TabIndex == 0;
    }

    private void Update(IFramework _)
    {
        if (!enabled) return;
        try
        {
            if (!Visible() || (character != 0 && character != player.ContentId))
            {
                if (transfer.Running) transfer.Stop("Stopped: saddlebag closed or character changed.");
                readyAt = 0;
                character = player.ContentId;
                return;
            }
            character = player.ContentId;
            var now = Environment.TickCount64;
            if (readyAt == 0) readyAt = now + 1000;
            if (!transfer.Running || now < nextMove) return;
            nextMove = now + 300;
            var view = ReadLive();
            if (view is null) { transfer.Stop("Stopped: inventory or saddlebag data is unavailable."); return; }
            var move = transfer.Advance(view, now);
            if (move is not null && !Send(move))
                transfer.Stop($"Entrusted {transfer.Moved:N0}. Entrust is unavailable; check saddlebag space.");
        }
        catch (Exception ex) { Fail(ex); }
    }

    private TransferView? ReadLive() => inventoryReader.Read(InventoryType.SaddleBag1, InventoryType.SaddleBag2);

    private unsafe bool Send(TransferSlot source)
    {
        // Pandora's entrust event: let the game choose stacks/empty slots and enforce restrictions.
        const byte entrustEvent = 56;
        var previousMenu = gui.GetAddonByName("ContextMenu");
        if (!previousMenu.IsNull && previousMenu.IsVisible) return false;
        var agent = AgentInventoryContext.Instance();
        var module = AgentModule.Instance();
        if (agent == null || module == null) return false;
        var inventoryAgent = module->GetAgentByInternalId(AgentId.Inventory);
        if (inventoryAgent == null) return false;
        agent->OpenForItemSlot((InventoryType)source.Container, source.Slot, 0, inventoryAgent->GetAddonId());
        var addon = gui.GetAddonByName("ContextMenu");
        if (addon.IsNull || !addon.IsVisible) return false;
        var menu = (AddonContextMenu*)addon.Address;
        if (agent->TargetInventoryId != (InventoryType)source.Container || agent->TargetInventorySlotId != source.Slot ||
            menu->AtkValuesCount < 1) return false;
        var count = Math.Min((int)menu->AtkValues[0].UInt, 32);
        for (var index = 0; index < count; index++)
        {
            var eventIndex = agent->ContexItemStartIndex + index;
            if (eventIndex < 0 || eventIndex >= agent->EventIds.Length || agent->EventIds[eventIndex] != entrustEvent ||
                agent->IsContextItemDisabled(index)) continue;
            // Another plugin can insert menu entries. Do not invoke a shifted or unrelated label.
            var labelIndex = 8 + index;
            if (labelIndex >= menu->AtkValuesCount ||
                agent->EventParams[eventIndex].Type is not (AtkValueType.String or AtkValueType.ManagedString) ||
                menu->AtkValues[labelIndex].Type is not (AtkValueType.String or AtkValueType.ManagedString) ||
                agent->EventParams[eventIndex].String.ToString() != menu->AtkValues[labelIndex].String.ToString()) continue;
            var values = stackalloc AtkValue[3];
            values[0].SetInt(0);
            values[1].SetInt(index);
            values[2].SetInt(0);
            menu->FireCallback(3, values, true);
            return true;
        }
        menu->Close(true);
        return false;
    }

    private EntrustButtonState ButtonState() => new(enabled && Visible(),
        readyAt != 0 && Environment.TickCount64 >= readyAt && character == player.ContentId,
        transfer.Running, false, transfer.Status);

    private void Click(bool _)
    {
        if (!enabled || !Visible()) return;
        if (transfer.Running) { transfer.Stop("Stopped. A move already sent may still finish."); return; }
        var view = ReadLive();
        if (view is null) transfer.Stop("Inventory or saddlebag data is not ready.");
        else { transfer.Start(view); nextMove = 0; }
    }

    private void Fail(Exception ex)
    {
        transfer.Stop("Stopped: could not transfer items. See the plugin log.");
        if (!errorLogged) log.Error(ex, "Saddlebag entrust duplicates failed");
        errorLogged = true;
    }
}

