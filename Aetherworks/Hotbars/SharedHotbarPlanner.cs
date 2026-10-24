namespace Aetherworks.Hotbars;

public static class SharedHotbarPlanner
{
    public static SharedHotbarPlan Build(SharedHotbarSnapshot snapshot, SharedHotbarState state,
        HotbarConflictResolution? resolution = null)
    {
        snapshot.Validate();
        state.Validate();
        var next = new SharedHotbarState();
        var edits = new List<SharedSlotEdit>();
        var conflicts = new List<SharedSlotConflict>();
        var initial = new List<int>();
        // Unshared bars leave the baseline, so sharing one again starts with PvE.
        foreach (var (bar, values) in snapshot.Bars.OrderBy(p => p.Key))
        {
            var initialized = state.Baselines.TryGetValue(bar, out var prior);
            if (!initialized) initial.Add(bar);
            var baseline = new HotbarCommand[values.PvE.Length];
            next.Baselines.Add(bar, baseline);
            for (var slot = 0; slot < baseline.Length; slot++)
            {
                var pve = values.PvE[slot].Canonical;
                var pvp = values.PvP[slot].Canonical;
                var previous = initialized ? prior![slot].Canonical : default;
                HotbarCommand desired;
                if (!initialized || pve == pvp || pvp == previous) desired = pve;
                else if (pve == previous) desired = pvp;
                else
                {
                    var conflict = new SharedSlotConflict(bar, slot, values.PvE[slot], values.PvP[slot]);
                    if (resolution is not null && resolution.Context == snapshot.Context && resolution.Conflicts.Contains(conflict))
                        desired = resolution.UsePvP ? pvp : pve;
                    else
                    {
                        conflicts.Add(conflict);
                        baseline[slot] = previous;
                        continue;
                    }
                }
                baseline[slot] = desired;
                var desiredPvE = desired.ForMode(false);
                var desiredPvP = desired.ForMode(true);
                if (values.PvE[slot] != desiredPvE) edits.Add(new(bar, slot, false, values.PvE[slot], desiredPvE));
                if (values.PvP[slot] != desiredPvP) edits.Add(new(bar, slot, true, values.PvP[slot], desiredPvP));
            }
        }
        return new(next, edits, conflicts, initial);
    }
}
