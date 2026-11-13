using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using System.Numerics;

namespace Aetherworks.Ember;

internal sealed class EmberHost(IDalamudPluginInterface plugin, EmberOptions options) : IDisposable
{
    private readonly string directory = plugin.AssemblyLocation.DirectoryName!;
    private DependencyManager? dependencies;
    private RenderProcess? process;
    private Overlay? overlay;
    private bool open;
    private string? error;

    public void Toggle() => open = !open;
    public void Hide() => open = false;

    public void Draw()
    {
        if (!open) return;
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
        DxHandler.Initialise(plugin);
        var renderer = new RenderProcess(Environment.ProcessId, directory, plugin.GetPluginConfigDirectory(), dependencies!, Services.PluginLog);
        process = renderer;
        renderer.Rpc!.RendererReady += message => Services.Framework.RunOnFrameworkThread(() =>
        {
            if (process != renderer) return;
            if (message.HasDxSharedTexturesSupport)
                overlay ??= new(renderer, options);
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

    public void DrawOptions(Action save)
    {
        bool changed = ImGui.InputFloat("Zoom", ref options.Zoom, 1, 10, "%.0f%%");
        if (changed)
        {
            options.Zoom = Math.Clamp(options.Zoom, 10, 500);
            overlay?.Zoom(options.Zoom);
        }
        if (ImGui.InputFloat("Opacity", ref options.Opacity, 1, 10, "%.0f%%"))
        {
            options.Opacity = Math.Clamp(options.Opacity, 10, 100);
            changed = true;
        }
        changed |= ImGui.Checkbox("Locked", ref options.Locked);
        changed |= ImGui.Checkbox("Hide out of combat", ref options.HideOutOfCombat);
        changed |= ImGui.Checkbox("Hide in PvP", ref options.HideInPvP);
        ImGui.BeginDisabled(!options.HideOutOfCombat);
        changed |= ImGui.InputInt("Hide delay (seconds)", ref options.HideDelay);
        ImGui.EndDisabled();
        options.HideDelay = Math.Max(0, options.HideDelay);
        if (changed) save();
    }

    public void Dispose()
    {
        var renderer = process;
        process = null;
        overlay?.Dispose();
        overlay = null;
        renderer?.Dispose();
        dependencies?.Dispose();
        DxHandler.Shutdown();
    }
}
