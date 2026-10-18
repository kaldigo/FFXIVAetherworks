using System.Numerics;
using Aetherworks.Transfers;
using Aetherworks.Ownership;
using Aetherworks.Tweaks;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

public sealed class FcChestEntrustDuplicates(IDalamudPluginInterface pi, IFramework framework,
    IGameGui gui, IPlayerState player, IDataManager data, IPluginLog log) : ITweak
{
    private readonly DuplicateTransfer transfer = new("FC chest");
    private readonly LiveTransferInventory inventory = new(data, requireTradable: true);
    private bool enabled;
    private bool errorLogged;
    private (ulong Character, uint Page)? context;
    private long readyAt;
    private long nextMove;
    private readonly ChestTabRun tabs = new();
    private readonly StableObservation<TransferSlot[]> settled = new();
    private bool tabStarted;
    private long tabDeadline;
    private bool showAllTabs;
    public bool ShowAllTabs
    {
        get => showAllTabs;
        set { showAllTabs = value; if (!value && tabs.Running) Stop("All-tabs deposit stopped."); }
    }

    private void Stop(string message) { tabs.Stop(); transfer.Stop(message); }
    public string Id => "fc-chest-entrust-duplicates";
    public string Name => "FC Chest Entrust Duplicates";
    public string Description => "Add an Entrust Duplicates button for the current FC chest item tab. Move matching tradable stacks; changing tabs stops the queue.";
    public string Category => "Items and storage";

    public void Enable()
    {
        if (enabled) return;
        pi.UiBuilder.Draw += Draw;
        framework.Update += Update;
        enabled = true;
    }

    public void Disable()
    {
        enabled = false;
        pi.UiBuilder.Draw -= Draw;
        framework.Update -= Update;
        Stop("");
        context = null;
        readyAt = 0;
        errorLogged = false;
    }

    private uint? CurrentPage() => player.ContentId == 0 ? null : Aetherworks.Ownership.FcChestAccess.CurrentPage(gui);

    private void Update(IFramework _)
    {
        if (!enabled) return;
        try
        {
            var page = CurrentPage();
            var current = page is null ? ((ulong Character, uint Page)?)null : (player.ContentId, page.Value);
            if (tabs.Running)
            {
                UpdateAllTabs(page);
                return;
            }
            if (context != current)
            {
                if (transfer.Running) transfer.Stop("Stopped: chest closed, tab or character changed.");
                context = current;
                readyAt = Environment.TickCount64 + 1000;
                return;
            }
            if (current is null || !transfer.Running || Environment.TickCount64 < nextMove) return;
            nextMove = Environment.TickCount64 + 300;
            var view = inventory.Read((InventoryType)current.Value.Page);
            if (view is null) { transfer.Stop("Stopped: current tab or inventory data is unavailable."); return; }
            var source = transfer.Advance(view, Environment.TickCount64);
            if (source is not null) SendOrSkip(source, view, current.Value.Page);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void NextTab(uint current)
    {
        if (!tabs.Next(current, Environment.TickCount64, tabStarted ? transfer.Moved : 0))
        {
            transfer.Stop($"Done: entrusted {tabs.Moved:N0} items across accessible tabs.");
            return;
        }
        tabStarted = false;
        settled.Reset();
        tabDeadline = Environment.TickCount64 + 10000;
        transfer.Stop($"Opening tab {tabs.Page - 19999}…");
        if (!FcChestAccess.SelectPage(gui, tabs.Page)) Stop("Stopped: chest tab is no longer accessible.");
    }

    private void UpdateAllTabs(uint? page)
    {
        var now = Environment.TickCount64;
        if (!tabs.Observe(player.ContentId, page, now))
        {
            Stop("Stopped: chest closed, tab changed, or tab selection timed out.");
            return;
        }
        if (tabs.Waiting) return;
        context = (player.ContentId, tabs.Page);
        readyAt = now + 1000;
        if (!tabStarted)
        {
            var permission = FcChestAccess.CanDeposit(gui, player.ContentId, tabs.Page);
            if (permission == false) { NextTab(tabs.Page); return; }
            if (permission is null)
            {
                Stop("Deposit permissions unavailable. Open the FC member list and rank details, then try again.");
                return;
            }
            if (now >= tabDeadline) { Stop("Stopped: chest tab data is not ready."); return; }
            var initial = inventory.Read((InventoryType)tabs.Page);
            if (initial is null) { settled.Reset(); return; }
            if (!settled.Accept(tabs.Page.ToString(), initial.Destination, now,
                    (a, b) => a.SequenceEqual(b))) return;
            // Ignore stacks that cannot fit on this tab; later tabs may have room.
            transfer.Start(initial with { Inventory = initial.Inventory.Where(s =>
                ChestDeposit.Destination(s, initial.Destination, tabs.Page) is not null).ToArray() });
            tabStarted = true;
            nextMove = 0;
        }
        if (now < nextMove) return;
        nextMove = now + 300;
        var view = inventory.Read((InventoryType)tabs.Page);
        if (view is null) { Stop("Stopped: current tab or inventory data is unavailable."); return; }
        var source = transfer.Advance(view, now);
        if (source is not null && !SendOrSkip(source, view, tabs.Page)) return;
        if (!transfer.Running)
        {
            if (transfer.Completed) NextTab(tabs.Page);
            else tabs.Stop(); // Never proceed after an unconfirmed native request.
        }
    }

    private bool SendOrSkip(TransferSlot source, TransferView view, uint page)
    {
        if (ChestDeposit.Destination(source, view.Destination, page) is null)
        {
            transfer.SkipUnsent();
            return true;
        }
        if (tabs.Running)
        {
            var permission = FcChestAccess.CanDeposit(gui, player.ContentId, page);
            if (permission == false)
            {
                // No request was sent: safely abandon this tab and continue.
                transfer.SkipUnsent();
                NextTab(page);
                return false;
            }
            if (permission is null) { Stop("Stopped: deposit permissions are unavailable."); return false; }
        }
        if (!Send(source, page)) Stop("Stopped: chest or inventory changed.");
        return transfer.Running;
    }

    private unsafe bool Send(TransferSlot source, uint page)
    {
        if (CurrentPage() != page || context != (player.ContentId, page)) return false;
        var view = inventory.Read((InventoryType)page);
        var agent = AgentFreeCompanyChest.Instance();
        if (view is null || agent == null || !view.Inventory.Contains(source)) return false;
        var destination = ChestDeposit.Destination(source, view.Destination, page);
        if (destination is null) return false;
        // Same native operation as Pandora's FC Chest Quick Deposit. The game enforces
        // chest permissions and locks; a rejected request times out without being retried.
        agent->MoveItemInChest((InventoryType)source.Container, source.Slot,
            (InventoryType)page, destination.Slot);
        return true;
    }

    private void Draw()
    {
        if (!enabled) return;
        try
        {
            var page = CurrentPage();
            if (page is null) return;
            var addon = gui.GetAddonByName("FreeCompanyChest");
            var viewport = ImGui.GetMainViewport();
            var size = new Vector2(Math.Min(370 * ImGui.GetIO().FontGlobalScale, viewport.WorkSize.X),
                ImGui.GetTextLineHeightWithSpacing() * (ShowAllTabs ? 7 : 5) + ImGui.GetStyle().WindowPadding.Y * 2);
            var position = viewport.Pos + new Vector2(addon.X, addon.Y + addon.Height * addon.Scale + 6);
            position = Vector2.Clamp(position, viewport.WorkPos, Vector2.Max(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize - size));
            ImGui.SetNextWindowPos(position);
            ImGui.SetNextWindowSize(size);
            ImGui.SetNextWindowViewport(viewport.ID);
            var visible = ImGui.Begin("###AetherworksEntrustFcChest", ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing);
            try
            {
                if (!visible) return;
                ImGui.TextUnformatted($"FC chest — Tab {page.Value - (uint)InventoryType.FreeCompanyPage1 + 1}");
                if (transfer.Running || tabs.Running)
                {
                    if (ImGui.Button("Stop")) Stop("Stopped. A move already sent may still finish.");
                }
                else
                {
                    var ready = context == (player.ContentId, page.Value) && Environment.TickCount64 >= readyAt;
                    ImGui.BeginDisabled(!ready);
                    try
                    {
                        if (ImGui.Button("Entrust Duplicates"))
                        {
                            var view = inventory.Read((InventoryType)page.Value);
                            if (view is null) transfer.Stop("Inventory or current tab is not ready.");
                            else { transfer.Start(view); nextMove = 0; }
                        }
                        if (ShowAllTabs && ImGui.Button("Entrust Duplicates — All Tabs"))
                        {
                            tabs.Start(player.ContentId, FcChestAccess.AccessiblePages(gui));
                            tabStarted = false;
                            NextTab(page.Value);
                        }
                    }
                    finally { ImGui.EndDisabled(); }
                }
                if (transfer.Status.Length > 0) ImGui.TextWrapped(transfer.Status);
            }
            finally { ImGui.End(); }
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Fail(Exception ex)
    {
        Stop("Stopped: could not transfer items. See the plugin log.");
        if (!errorLogged) log.Error(ex, "FC chest entrust duplicates failed");
        errorLogged = true;
    }
}

