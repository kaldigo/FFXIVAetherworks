using System.Numerics;
using Aetherworks.Features;
using Aetherworks.Hotbars;
using Aetherworks.Tweaks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherworks.Windows;

public sealed class SettingsWindow : Window
{
    private readonly TweakManager manager;
    private readonly Configuration config;
    private readonly ReturnViaTeleport fallback;
    private readonly SharedHotbarSync hotbarSync;
    private string search = string.Empty;
    private string selected = ReturnViaTeleport.FeatureId;

    public SettingsWindow(TweakManager manager, Configuration config, ReturnViaTeleport fallback, SharedHotbarSync hotbarSync)
        : base("Aetherworks###AetherworksSettings")
    {
        this.manager = manager;
        this.config = config;
        this.fallback = fallback;
        this.hotbarSync = hotbarSync;
        Size = new Vector2(780, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(620, 330), MaximumSize = new Vector2(float.MaxValue) };
    }

    public override void Draw()
    {
        if (manager.SaveError is { } saveError)
        {
            ImGui.TextWrapped(saveError);
            if (ImGui.Button("Retry saving")) manager.Save();
        }
        var width = Math.Clamp(ImGui.GetContentRegionAvail().X * 0.34f, 190, 280);
        if (ImGui.BeginChild("FeatureList", new Vector2(width, 0), true))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##Search", "Search features...", ref search, 128);
            ImGui.Separator();
            var matches = manager.Entries.Where(e =>
                $"{e.Tweak.Name} {e.Tweak.Description} {e.Tweak.Category}".Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var group in matches.GroupBy(e => e.Tweak.Category))
            {
                ImGui.TextDisabled(group.Key);
                foreach (var entry in group)
                {
                    var label = $"{(entry.IsEnabled ? "[On]" : "[Off]")} {entry.Tweak.Name}";
                    if (ImGui.Selectable(label, selected == entry.Tweak.Id)) selected = entry.Tweak.Id;
                }
            }
            if (matches.Count == 0) ImGui.TextWrapped("No matching features.");
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("FeatureDetails", Vector2.Zero, false))
        {
            var entry = manager.Entries.First(e => e.Tweak.Id == selected);
            ImGui.TextUnformatted(entry.Tweak.Name);
            ImGui.TextDisabled(entry.Tweak.Category);
            ImGui.Separator();
            ImGui.TextWrapped(entry.Tweak.Description);
            ImGui.Spacing();
            var enabled = entry.IsEnabled;
            ImGui.BeginDisabled(entry.NeedsCleanup);
            if (ImGui.Checkbox("Enable feature", ref enabled)) manager.SetEnabled(entry, enabled);
            ImGui.EndDisabled();
            ImGui.Spacing();
            if (entry.Tweak.Id == ReturnViaTeleport.FeatureId) DrawReturnSettings();
            else if (entry.Tweak.Id == SharedHotbarSync.FeatureId) DrawHotbarSettings();
            if (entry.Error is { } error) ImGui.TextWrapped(error + ". See the Dalamud log for details.");
            if (entry.NeedsCleanup && ImGui.Button("Retry disabling")) manager.SetEnabled(entry, false);
        }
        ImGui.EndChild();
    }

    private void DrawReturnSettings()
    {
        var skip = config.SkipPaidTeleportConfirmation;
        if (ImGui.Checkbox("Skip paid Teleport confirmation", ref skip))
        {
            config.SkipPaidTeleportConfirmation = skip;
            fallback.Cancel();
            manager.Save();
        }
        ImGui.TextWrapped(skip
            ? "Pressing Return during its cooldown immediately teleports you home at the normal fare."
            : "Pressing Return during its cooldown shows the destination and fare before you confirm.");
        ImGui.Spacing();
        ImGui.TextWrapped("Uses your current registered home point. Normal travel restrictions and aetheryte ticket settings apply. Return stays free whenever it is ready.");
    }

    private void DrawHotbarSettings()
    {
        ImGui.TextWrapped("Uses the bars marked Shared in Character Configuration, including shared cross hotbars. Enabling this feature initially replaces their PvP assignments with PvE assignments.");
        ImGui.Spacing();
        ImGui.TextWrapped("Later additions, moves and cleared slots sync both ways. Each character keeps its own sync history. Sprint uses the correct action for each mode.");
        ImGui.Spacing();
        ImGui.TextWrapped(hotbarSync.Status);
        if (hotbarSync.SharedBars.Count != 0)
            ImGui.TextWrapped("Included: " + string.Join(", ", hotbarSync.SharedBars.Select(SharedHotbarSnapshot.BarName)));
        if (hotbarSync.Conflicts.Count != 0)
        {
            ImGui.Separator();
            ImGui.TextWrapped("Both modes changed these slots. Choose which assignments to keep for the conflicting slots:");
            foreach (var conflict in hotbarSync.Conflicts) ImGui.TextUnformatted(conflict.Label);
            if (ImGui.Button("Keep PvE for conflicts")) hotbarSync.ResolveConflicts(false);
            if (ImGui.Button("Keep PvP for conflicts")) hotbarSync.ResolveConflicts(true);
        }
        ImGui.Spacing();
        ImGui.TextWrapped("Backups are saved locally before changes. Sync waits during loading, combat and hotbar edits. Turn the feature off to stop syncing; turning it back on resumes the saved history.");
    }
}
