using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using KamiToolKit.Extensions;
using Dalamud.Plugin.Services;

namespace Aetherworks.Windows;

public sealed record EntrustButtonState(bool Visible, bool Ready, bool Running, bool AllTabs, string Status);

/// <summary>Game-textured buttons attached to the storage addon; no ImGui window or panel.</summary>
public sealed unsafe class NativeEntrustButtons : IDisposable
{
    private readonly AddonController controller;
    private readonly Func<EntrustButtonState> state;
    private readonly Action<bool> clicked;
    private readonly IPluginLog log;
    private readonly IFramework framework;
    private readonly bool chest;
    private TextButtonNode? current, all;
    private bool errorLogged;
    private string? chestTexture;

    public NativeEntrustButtons(string addonName, Func<EntrustButtonState> state, Action<bool> clicked, IPluginLog log, IFramework framework)
    {
        this.state = state; this.clicked = clicked; this.log = log;
        this.framework = framework;
        chest = addonName == "FreeCompanyChest";
        controller = new AddonController
        {
            AddonName = addonName,
            OnSetup = Setup,
            OnUpdate = Update,
            OnFinalize = _ => Clear(),
        };
    }
    // Plugin restoration/unload can run on a loader thread. Wait for completion
    // so TweakManager can still report failures and roll back synchronously.
    public void Enable() => framework.RunOnFrameworkThread(controller.Enable).GetAwaiter().GetResult();
    private void Setup(AtkUnitBase* addon)
    {
        if (addon->RootNode == null || current != null) return;
        try
        {
            current = new TextButtonNode { Size = new Vector2(160, 28), String = "Entrust Duplicates", OnClick = () => Click(false) };
            current.AttachNode(addon);
            if (chest)
            {
                all = new TextButtonNode { Size = new Vector2(160, 28), String = "Entrust All Tabs", OnClick = () => Click(true) };
                all.AttachNode(addon);
            }
            Update(addon);
        }
        catch (Exception ex) { Clear(); Report(ex); }
    }
    private void Update(AtkUnitBase* addon)
    {
        if (current == null) return;
        try
        {
            var view = state();
            // FreeCompanyChest.uld: node 17 is the gil balance container, not the Gil tab.
            // Stack upwards from it; leave the game's controls and item grid untouched.
            var balance = chest ? addon->GetNodeById(17) : null;
            var showAll = view.AllTabs && !view.Running;
            current.Position = chest
                ? new Vector2(balance != null ? balance->X : 14,
                    (balance != null ? balance->Y : addon->RootNode->Height - 80) - (showAll ? 68 : 36))
                : new Vector2(16, addon->RootNode->Height - 48);
            if (chest) MatchChestTexture(addon);
            current.IsVisible = view.Visible;
            current.IsEnabled = view.Running || view.Ready;
            current.String = view.Running ? "Stop" : "Entrust Duplicates";
            current.TextTooltip = view.Status.Length > 0 ? view.Status : "Entrust stackable duplicates to this storage tab.";
            if (all != null)
            {
                all.Position = current.Position + new Vector2(0, 32);
                all.IsVisible = view.Visible && view.AllTabs && !view.Running;
                all.IsEnabled = view.Ready;
                all.TextTooltip = "Entrust Duplicates — All Tabs";
            }
        }
        catch (Exception ex)
        {
            current.IsVisible = false;
            if (all != null) all.IsVisible = false;
            Report(ex);
        }
    }
    private void MatchChestTexture(AtkUnitBase* addon)
    {
        // Read the actual themed texture from History (ULD node 104, background 3).
        // The global theme resolver can differ from the texture used by this addon.
        // Load our own texture reference; never share ownership of native assets.
        var history = addon->GetComponentNodeById(104);
        if (history == null || history->Component == null) return;
        var uld = &history->Component->UldManager;
        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node == null || node->NodeId != 3 || node->Type != NodeType.NineGrid) continue;
            var background = (AtkNineGridNode*)node;
            if (background->PartsList == null || background->PartId >= background->PartsList->PartCount) return;
            var path = background->PartsList->Parts[background->PartId].LoadedPath;
            if (string.IsNullOrEmpty(path) || path == chestTexture) return;
            current!.BackgroundNode.PartsList[0]->LoadTexture(path, resolveTheme: false);
            all?.BackgroundNode.PartsList[0]->LoadTexture(path, resolveTheme: false);
            chestTexture = path;
            return;
        }
    }
    private void Click(bool allTabs)
    {
        try
        {
            var view = state();
            if (!view.Visible || (!view.Running && !view.Ready) || (allTabs && (!view.AllTabs || view.Running))) return;
            clicked(allTabs);
        }
        catch (Exception ex) { Report(ex); }
    }
    private void Report(Exception ex)
    {
        if (!errorLogged) log.Error(ex, "Native entrust buttons failed");
        errorLogged = true;
    }
    private void Clear()
    {
        chestTexture = null;
        all?.Dispose(); all = null;
        current?.Dispose(); current = null;
    }
    public void Dispose() => framework.RunOnFrameworkThread(() =>
    {
        controller.Dispose();
        Clear();
    }).GetAwaiter().GetResult();
}
