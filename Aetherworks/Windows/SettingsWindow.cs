using System.Numerics;
using Aetherworks.Features;
using Aetherworks.Hotbars;
using Aetherworks.Tweaks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Aetherworks.Windows;

public sealed class SettingsWindow : Window
{
    private readonly TweakManager manager;
    private readonly Configuration config;
    private readonly ReturnViaTeleport fallback;
    private readonly SharedHotbarSync hotbarSync;
    private readonly PartyDamage partyDamage;
    private readonly AetherworksShell shell = new();
    private readonly AetherworksUiActions actions;

    public SettingsWindow(TweakManager manager, Configuration config, ReturnViaTeleport fallback, SharedHotbarSync hotbarSync, PartyDamage partyDamage, Action<float> applyZoom)
        : base("Aetherworks###AetherworksSettings", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.partyDamage = partyDamage;
        this.manager = manager;
        this.config = config;
        this.fallback = fallback;
        this.hotbarSync = hotbarSync;
        Size = new Vector2(780,610);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360,320), MaximumSize = new Vector2(float.MaxValue) };
        actions = new AetherworksUiActions(SetFeature,SetSkipConfirmation,hotbarSync.ResolveConflicts,manager.Save,()=>IsOpen=false,
            value => { partyDamage.SetShieldsOverHp(value); manager.Save(); },
            color => { partyDamage.SetColor(color); manager.Save(); }, () => { applyZoom(config.Ember.Zoom); manager.Save(); }, manager.Save);
    }
    private static void DrawHeaderMark(Vector2 headerPosition, float size)
    {
        var texture = Ember.Services.TextureProvider
            .GetFromManifestResource(typeof(SettingsWindow).Assembly, "Aetherworks.mark.png")
            .GetWrapOrDefault();
        if (texture == null) return;

        // Reserve space for the shell's close button at the right edge.
        var position = new Vector2(ImGui.GetWindowPos().X + ImGui.GetWindowWidth() - 3 * size, headerPosition.Y);
        ImGui.GetWindowDrawList().AddImage(texture.Handle, position, position + new Vector2(size));
    }

    public override void Draw() => shell.Draw(Snapshot(),actions,ImGuiHelpers.GlobalScale,DrawHeaderMark);

    private AetherworksUiState Snapshot()
    {
        var features = manager.Entries.Select(entry =>
        {
            string status = !entry.IsEnabled ? "This convenience is switched off." : entry.Tweak.Id switch
            {
                ReturnViaTeleport.FeatureId => config.SkipPaidTeleportConfirmation
                    ? "Paid fallback enabled. Teleport starts without a fare confirmation."
                    : "Paid fallback enabled. Review the fare before travelling.",
                SharedHotbarSync.FeatureId => hotbarSync.Status,
                PartyDamage.FeatureId => partyDamage.Status,
                EmberOverlay.FeatureId => config.Ember.Open ? "Meter open. Visibility rules apply." : "Meter closed. Click the party-list heading to show it.",
                _ => "Available from the saddlebag item menu for stacks of two or more."
            };
            UiTone tone = entry.IsEnabled ? UiTone.Good : UiTone.Neutral;
            if (entry.Tweak.Id == SharedHotbarSync.FeatureId && !hotbarSync.Status.StartsWith("Syncing",StringComparison.Ordinal)) tone = UiTone.Neutral;
            if (entry.Tweak.Id == SharedHotbarSync.FeatureId && hotbarSync.Conflicts.Count != 0) tone = UiTone.Attention;
            if (entry.Tweak.Id == SharedHotbarSync.FeatureId && hotbarSync.Faulted) tone = UiTone.Error;
            if (entry.Error != null || entry.NeedsCleanup) { status = entry.Error ?? "Cleanup is incomplete."; tone = UiTone.Error; }
            return new FeatureView(entry.Tweak.Id,entry.Tweak.Name,entry.Tweak.Category,entry.Tweak.Description,
                entry.IsEnabled,entry.NeedsCleanup,entry.Error,status,tone);
        }).ToArray();
        return new(features,config.SkipPaidTeleportConfirmation,hotbarSync.Faulted,
            hotbarSync.SharedBars.Select(SharedHotbarSnapshot.BarName).ToArray(),
            hotbarSync.Conflicts.Select(c => $"{c.Label} · PvE {c.PvE} · PvP {c.PvP}").ToArray(),manager.SaveError, config.ShieldsOverHp, config.PartyDamageColor, config.Ember);
    }
    private void SetFeature(string id,bool enabled) => manager.SetEnabled(manager.Entries.First(e=>e.Tweak.Id==id),enabled);
    private void SetSkipConfirmation(bool value)
    {
        config.SkipPaidTeleportConfirmation = value;
        fallback.Cancel();
        manager.Save();
    }
}
