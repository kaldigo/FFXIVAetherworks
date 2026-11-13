namespace Aetherworks.Windows;

// Value snapshots keep live game services out of the renderer and its previews.
internal enum UiTone { Neutral, Good, Attention, Error }
internal enum Workspace { Overview, Features, Recovery }
internal sealed record FeatureView(string Id, string Name, string Category, string Description,
    bool Enabled, bool NeedsCleanup, string? Error, string Status, UiTone Tone = UiTone.Neutral);
internal sealed record AetherworksUiState(IReadOnlyList<FeatureView> Features, bool SkipConfirmation,
    bool SyncFaulted, IReadOnlyList<string> SharedBars, IReadOnlyList<string> Conflicts, string? SaveError, bool PartyDamageBeforeRequestedUpdate = false)
{
    public int EnabledCount => Features.Count(f => f.Enabled);
    public int IssueCount => Features.Count(f => f.Error != null || f.NeedsCleanup) +
        (SyncFaulted ? 1 : 0) + (Conflicts.Count > 0 ? 1 : 0) + (SaveError != null ? 1 : 0);
}
internal sealed record AetherworksUiActions(Action<string, bool> SetFeature, Action<bool> SetSkipConfirmation,
    Action<bool> ResolveConflicts, Action RetrySave, Action Close, Action<bool>? SetPartyDamageEntryPoint = null);

internal sealed class SettingsNavigation
{
    public Workspace Area { get; private set; }
    public string? Feature { get; private set; }
    public string Search = "";
    private Workspace origin;
    public string BackLabel => Name(origin);
    public static string Name(Workspace area) => area switch
    {
        Workspace.Overview => "Overview", Workspace.Features => "Features", _ => "Status & recovery"
    };
    public void OpenArea(Workspace area) { Area = area; Feature = null; }
    public void OpenFeature(string id) { origin = Area; Area = Workspace.Features; Feature = id; }
    public void Back() { Area = origin; Feature = null; }
    public static bool Matches(FeatureView feature, string search) =>
        search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).All(word =>
            $"{feature.Name} {feature.Category} {feature.Description}".Contains(word, StringComparison.OrdinalIgnoreCase));
}
