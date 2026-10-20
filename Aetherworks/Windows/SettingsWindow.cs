using System.Numerics;
using Aetherworks.Tweaks;
using Aetherworks.Ownership;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherworks.Windows;

public sealed class SettingsWindow(TweakManager manager, OwnershipTracker ownership) : Window("Aetherworks###AetherworksSettings")
{
    private string search = string.Empty;

    public override void Draw()
    {
        ImGui.TextWrapped("Your collection of small improvements. Every tweak has its own toggle and starts disabled.");
        ImGui.TextWrapped("Ownership is collected even with tooltips disabled. Open your saddlebag, retainer list and each retainer's inventory, glamour dresser, armoire and each FC chest item tab to record their contents. Include FC Chest controls whether shared chest quantities appear in results.");
        if (ownership.Error is { } trackingError) ImGui.TextWrapped(trackingError);
        ImGui.TextWrapped(ownership.RetainerStatus);
        if (manager.SaveError is { } saveError)
        {
            ImGui.TextWrapped(saveError);
            if (ImGui.Button("Retry saving")) manager.Save();
        }
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Search", "Search tweaks...", ref search, 256);
        ImGui.Separator();

        if (manager.Entries.Count == 0)
        {
            ImGui.TextWrapped("No tweaks have been added yet. New features will appear here with their own on/off switch.");
            return;
        }

        var matches = manager.Entries.Where(e =>
            $"{e.Tweak.Name} {e.Tweak.Description} {e.Tweak.Category}".Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Tweak.Category).ThenBy(e => e.Tweak.Name).ToList();
        if (matches.Count == 0) ImGui.TextUnformatted("No tweaks match your search.");
        foreach (var group in matches.GroupBy(e => e.Tweak.Category))
        {
            ImGui.TextUnformatted(group.Key);
            foreach (var entry in group)
            {
                ImGui.PushID(entry.Tweak.Id);
                var enabled = entry.IsEnabled;
                ImGui.BeginDisabled(entry.NeedsCleanup);
                if (ImGui.Checkbox(entry.Tweak.Name, ref enabled)) manager.SetEnabled(entry, enabled);
                ImGui.EndDisabled();
                ImGui.TextWrapped(entry.Tweak.Description);
                if (entry.Error is { } error) ImGui.TextWrapped(error + ". See the Dalamud log for details.");
                if (entry.NeedsCleanup && ImGui.Button("Retry disabling")) manager.SetEnabled(entry, false);
                ImGui.Spacing();
                ImGui.PopID();
            }
            ImGui.Separator();
        }
    }

    public void ConfigureSize()
    {
        Size = new Vector2(620, 460);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(400, 260), MaximumSize = new Vector2(float.MaxValue) };
    }
}
