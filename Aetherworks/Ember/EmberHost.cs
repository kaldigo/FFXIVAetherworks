using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using System.Numerics;

namespace Aetherworks.Ember;

internal sealed class EmberHost(IDalamudPluginInterface plugin, EmberOptions options, ICondition condition, IClientState client, Action saveOpenState) : IDisposable
{
    private readonly string directory = plugin.AssemblyLocation.DirectoryName!;
    private DependencyManager? dependencies;
    private RenderProcess? process;
    private EmberPage? page;
    private Overlay? overlay;
    private bool enabled;
    private EmberVisibility visibility = new();
    private string? error;

    public void Toggle()
    {
        options.Open = !options.Open;
        saveOpenState();
    }

    public void SetEnabled(bool value)
    {
        enabled = value;
        visibility = new();
    }

    public void Draw()
    {
        if (!enabled) return;
        bool open = visibility.Update(options.Open, options.HideOutOfCombat, condition[ConditionFlag.InCombat],
            options.HideDelay, Environment.TickCount64);
        if (!open || !client.IsLoggedIn || (options.HideInPvP && client.IsPvP)) return;
        try
        {
            if (dependencies == null && error == null)
            {
                plugin.Create<Services>();
                dependencies = new(directory, plugin.GetPluginConfigDirectory());
                dependencies.DependenciesReady += Start;
                dependencies.Initialise();
            }
            dependencies?.Render();
            process?.EnsureRenderProcessIsAlive();
            if (error != null)
            {
                ImGui.Begin("Ember overlay", ref open);
                ImGui.TextWrapped(error);
                ImGui.End();
                if (!open) Toggle();
                return;
            }
            if (overlay == null) return;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            try { overlay.Render(); }
            finally { ImGui.PopStyleVar(); }
        }
        catch (Exception ex)
        {
            error = $"Ember could not start. {ex.Message}";
            Services.PluginLog.Error(ex, "Ember overlay failed");
        }
    }

    private void Start(object? sender, EventArgs args)
    {
        page = new(directory);
        DxHandler.Initialise(plugin);
        var renderer = new RenderProcess(Environment.ProcessId, directory, plugin.GetPluginConfigDirectory(), dependencies!, Services.PluginLog);
        process = renderer;
        renderer.Rpc!.RendererReady += message => Services.Framework.RunOnFrameworkThread(() =>
        {
            if (process != renderer) return;
            if (message.HasDxSharedTexturesSupport)
                overlay ??= new(renderer, options, page.Url);
            else
                error = "Ember could not initialize DirectX shared textures.";
        });
        renderer.Rpc.UpdateTexture += message => Services.Framework.RunOnFrameworkThread(() =>
        {
            if (process == renderer) overlay?.SetTexture((nint)message.TextureHandle);
        });
        renderer.Rpc.SetCursor += message => Services.Framework.RunOnFrameworkThread(() =>
        {
            if (process == renderer) overlay?.SetCursor(message.Cursor);
        });
        renderer.Start();
    }

    public void ApplyZoom(float zoom) => overlay?.Zoom(zoom);

    public void Dispose()
    {
        var renderer = process;
        process = null;
        overlay?.Dispose();
        overlay = null;
        renderer?.Dispose();
        page?.Dispose();
        dependencies?.Dispose();
        DxHandler.Shutdown();
    }
}
