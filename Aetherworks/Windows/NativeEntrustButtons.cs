using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
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
            // Chest's unused left column below Gil; saddlebag button immediately below
            // its bottom edge. Never create a rectangular background over the item grid.
            current.Position = chest ? new Vector2(16, 246) : new Vector2(16, addon->RootNode->Height + 4);
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
        all?.Dispose(); all = null;
        current?.Dispose(); current = null;
    }
    public void Dispose() => framework.RunOnFrameworkThread(() =>
    {
        controller.Dispose();
        Clear();
    }).GetAwaiter().GetResult();
}
