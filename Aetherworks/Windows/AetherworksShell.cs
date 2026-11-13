using AetherglassUI;
using Dalamud.Bindings.ImGui;

namespace Aetherworks.Windows;

internal sealed class AetherworksShell
{
    internal const string ReturnId = "return-via-teleport", HotbarId = "shared-hotbar-sync", SaddlebagId = "saddlebag-withdraw-leave-one";
    internal SettingsNavigation Navigation { get; } = new();
    private readonly WindowShell window = new();
    private readonly SettingsPage overview = new(), features = new(), recovery = new();
    internal int FeatureRowsShown => features.LastRowCount;
    private string? target;

    internal void Draw(AetherworksUiState state, AetherworksUiActions actions, float uiScale)
    {
        window.Close = actions.Close;
        window.Selected = Navigation.Area.ToString();
        if (Navigation.Feature != target)
        {
            target = Navigation.Feature;
            if (target != null)
            {
                features.Query = Navigation.Search = "";
                features.SetCollapsed(target, false);
                features.ScrollToGroup(target);
            }
        }
        features.Query = Navigation.Search;
        Destination Page(Workspace area, string glyph, Action draw) =>
            new(area.ToString(), SettingsNavigation.Name(area), glyph, () =>
            {
                if (Navigation.Area != area) Navigation.OpenArea(area);
                draw();
            }) { DrawIcon = (p, size, color) => AetherworksChrome.NavigationIcon(area, p, size, color) };
        window.Draw("Aetherworks", "", [
            Page(Workspace.Overview, "▦", () => overview.Draw("overview", [
                Section("overview", "Conveniences", state.Features.Select(f =>
                    OverviewRow(f, actions))),
                Section("health", "Status", [Button("review", "Status & recovery", Health(state), "Review",
                    () => Navigation.OpenArea(Workspace.Recovery))])
            ])),
            Page(Workspace.Features, "☰", () =>
            {
                features.Draw("features", state.Features.Select(f =>
                    Section(f.Id, f.Name, FeatureRows(f, state, actions))).ToArray());
                Navigation.Search = features.Query;
            }),
            Page(Workspace.Recovery, "!", () => recovery.Draw("recovery", [
                Section("issues", "Status & recovery", RecoveryRows(state, actions)),
                Section("backups", "Shared hotbar backups", [Info("location", "Backup location",
                    "Open shared-hotbars/backups in the Aetherworks configuration folder. Initial backups preserve original assignments. last-change.json records the latest before-sync state.")])
            ]))
        ], AetherworksChrome.Mark, () => ImGui.TextUnformatted(state.SaveError != null ? "Settings could not be saved · See recovery" : Health(state)),
            () => state.SaveError != null ? StateTone.Error : state.IssueCount > 0 ? StateTone.Attention : StateTone.Ready);
    }

    private Setting OverviewRow(FeatureView f, AetherworksUiActions actions) =>
        new(f.Id, f.Name, () =>
        {
            ImGui.BeginDisabled(f.NeedsCleanup);
            bool next = Controls.Switch("enabled", f.Enabled);
            ImGui.EndDisabled();
            if (next != f.Enabled) actions.SetFeature(f.Id, next);
            ImGui.SameLine();
            if (ImGui.Button("Settings")) Navigation.OpenFeature(f.Id);
        }, f.Category, f.Id == HotbarId && !f.Enabled
            ? "First sync backs up PvP assignments, then copies shared PvE bars to PvP." : f.Status);

    private static string Health(AetherworksUiState state) => state.IssueCount > 0
        ? $"{state.IssueCount} issue(s) need attention" : $"{state.EnabledCount} of {state.Features.Count} conveniences enabled";

    private static Group Section(string id, string title, IEnumerable<Setting> rows) =>
        new(id, title, new Topic("", rows.ToArray()));

    private static Setting Info(string id, string title, string detail) => new(id, title, () => {}, detail, detail);
    private static Setting Button(string id, string title, string detail, string label, Action action) =>
        new(id, title, () => { if (ImGui.Button(label)) action(); }, detail, detail);

    private static Setting Toggle(string id, string title, string detail, bool value, Action<bool> change) =>
        new Setting(id, title, () =>
        {
            bool next = Controls.Switch(id, value);
            if (next != value) change(next);
        }, detail, detail) { Layout = FieldLayout.Switch };

    private IEnumerable<Setting> FeatureRows(FeatureView f, AetherworksUiState state, AetherworksUiActions actions)
    {
        string description = f.Id == HotbarId && !f.Enabled
            ? "First sync copies shared PvE assignments to PvP after backing up existing PvP assignments." : f.Description;
        yield return Toggle("enable", "Enable feature", description, f.Enabled, enabled => actions.SetFeature(f.Id, enabled))
            with { DisabledReason = () => f.NeedsCleanup ? "Retry disabling to finish cleanup." : null };
        yield return Info("status", "Current state", f.Status);
        if (f.NeedsCleanup) yield return Cleanup(f, actions);
        if (f.Id == ReturnId)
        {
            yield return Toggle("fare", "Ask before paid travel", state.SkipConfirmation
                ? "On cooldown, Return spends the normal Teleport fare immediately." : "Review the home destination and fare before paying.",
                !state.SkipConfirmation, confirm => actions.SetSkipConfirmation(!confirm));
            yield return Info("travel", "Travel rules", "Free Return when ready. On cooldown, normal travel restrictions and aetheryte ticket settings apply.");
        }
        if (f.Id == HotbarId)
        {
            foreach (var row in ConflictRows(state, actions)) yield return row;
            yield return Info("bars", "Included bars", state.SharedBars.Count > 0 ? string.Join(", ", state.SharedBars) : "No shared bars currently available.");
            yield return Info("sync", "Sync behavior", "Shared keyboard and cross hotbars sync in both directions. Sprint converts between PvE and PvP. Sync waits during combat, loading and hotbar edits.");
        }
        if (f.Id == SaddlebagId)
            yield return Info("withdraw", "Withdraw all but one", "Right-click a regular or premium saddlebag stack of two or more. A stack of 99 withdraws 98. HQ and NQ remain separate.");
        if (f.Id == "ember-overlay" && f.Enabled && state.Ember is {} ember)
            foreach (var row in EmberRows(ember, actions)) yield return row;
        if (f.Id != "party-damage" || !f.Enabled) yield break;
        yield return new("timing", "Entry point", () =>
        {
            string mode = state.PartyDamageBeforeRequestedUpdate ? "After requested update" : "Before draw";
            string next = Controls.Choice("##timing", mode, "Before draw", "After requested update");
            if (mode != next) actions.SetPartyDamageEntryPoint?.Invoke(next == "After requested update");
        }, "party list update timing");
        yield return new Setting("color", "Prefix color", () =>
        {
            var color = state.PartyDamageColor;
            Controls.Rgb(ref color);
            if (color != state.PartyDamageColor) actions.SetPartyDamageColor?.Invoke(color);
        }, "party list rank") { Layout = FieldLayout.Color };
    }

    private static IEnumerable<Setting> EmberRows(EmberOptions options, AetherworksUiActions actions)
    {
        Setting Percent(string id, string title, float value, float min, float max, Action<float> set) =>
            new Setting(id, title, () =>
            {
                float next = value;
                Controls.Decimal(ref next, min, max);
                if (next != value) set(next);
            }, "Ember overlay percent", $"{min}–{max}%") { Layout = FieldLayout.Number };
        yield return Percent("zoom", "Ember zoom", options.Zoom, 10, 500, v => { options.Zoom = v; actions.ApplyZoom?.Invoke(); });
        yield return Percent("opacity", "Ember opacity", options.Opacity, 10, 100, v => { options.Opacity = v; actions.SaveOptions?.Invoke(); });
        yield return Toggle("lock", "Lock Ember overlay", "Keep the meter in position.", options.Locked, v => { options.Locked = v; actions.SaveOptions?.Invoke(); });
        yield return Toggle("combat", "Hide Ember out of combat", "Hide the meter after combat ends.", options.HideOutOfCombat, v => { options.HideOutOfCombat = v; actions.SaveOptions?.Invoke(); });
        yield return new Setting("delay", "Ember hide delay", () =>
        {
            int next = options.HideDelay;
            Controls.Integer(ref next, 0, int.MaxValue);
            if (next != options.HideDelay) { options.HideDelay = next; actions.SaveOptions?.Invoke(); }
        }, "combat seconds", "Seconds after leaving combat.")
        { Layout = FieldLayout.Number, DisabledReason = () => options.HideOutOfCombat ? null : "Enable out-of-combat hiding first." };
        yield return Toggle("pvp", "Hide Ember in PvP", "Hide the meter in PvP areas.", options.HideInPvP, v => { options.HideInPvP = v; actions.SaveOptions?.Invoke(); });
    }

    private static Setting Cleanup(FeatureView f, AetherworksUiActions actions) =>
        Button(f.Id + "-cleanup", "Incomplete cleanup", f.Error ?? f.Status, "Retry disabling", () => actions.SetFeature(f.Id, false));

    private static IEnumerable<Setting> ConflictRows(AetherworksUiState state, AetherworksUiActions actions)
    {
        if (state.Conflicts.Count == 0) yield break;
        foreach (string conflict in state.Conflicts) yield return Info(conflict, "Conflicting assignments", conflict);
        yield return Button("pve", "Use PvE assignments", "Replace the PvP side of the listed conflicts.", "Keep PvE", () => actions.ResolveConflicts(false));
        yield return Button("pvp", "Use PvP assignments", "Replace the PvE side of the listed conflicts.", "Keep PvP", () => actions.ResolveConflicts(true));
    }

    private IEnumerable<Setting> RecoveryRows(AetherworksUiState state, AetherworksUiActions actions)
    {
        if (state.SaveError != null) yield return Button("save", "Settings could not be saved", state.SaveError, "Retry save", actions.RetrySave);
        foreach (var row in ConflictRows(state, actions)) yield return row;
        foreach (var f in state.Features)
        {
            yield return Button(f.Id, f.Name, f.Error ?? f.Status, "Settings", () => Navigation.OpenFeature(f.Id));
            if (f.NeedsCleanup) yield return Cleanup(f, actions);
        }
        if (state.SyncFaulted) yield return Info("paused", "Sync paused", "Check the error and backups, then disable and re-enable Shared Hotbar Sync to retry.");
    }
}
