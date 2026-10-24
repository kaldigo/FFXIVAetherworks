using Aetherworks.Hotbars;
using Aetherworks.Tweaks;
using Dalamud.Plugin.Services;

namespace Aetherworks.Features;

public sealed class SharedHotbarSync(IFramework framework, ISharedHotbarAccess access,
    ISharedHotbarStore store, IPluginLog log) : ITweak
{
    public const string FeatureId = "shared-hotbar-sync";
    private readonly SharedHotbarEngine engine = new(access, store);
    private bool enabled;
    private long nextTick;
    public string Id => FeatureId;
    public string Name => "Shared Hotbar Sync";
    public string Category => "Hotbars";
    public string Description => "Keep shared PvE and PvP hotbar assignments in sync. The first sync copies PvE into PvP; later edits flow in both directions.";
    public string Status => engine.Status;
    public IReadOnlyList<int> SharedBars => engine.SharedBars;
    public IReadOnlyList<SharedSlotConflict> Conflicts => engine.Conflicts;
    public void ResolveConflicts(bool usePvP) => engine.ResolveConflicts(usePvP);

    public void Enable()
    {
        if (enabled) return;
        engine.Start();
        nextTick = 0;
        framework.Update += Update;
        enabled = true;
    }

    public void Disable()
    {
        enabled = false;
        framework.Update -= Update;
        engine.Stop();
    }

    private void Update(IFramework _)
    {
        if (!enabled || Environment.TickCount64 < nextTick) return;
        nextTick = Environment.TickCount64 + 250;
        try { engine.Tick(Environment.TickCount64); }
        catch (Exception ex) { log.Error(ex, "Shared Hotbar Sync paused; before-sync backups were retained"); }
    }
}
