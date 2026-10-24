namespace Aetherworks.Hotbars;

public sealed class SharedHotbarEngine(ISharedHotbarAccess access, ISharedHotbarStore store)
{
    private string? character;
    private SharedHotbarState state = new();
    private SharedHotbarSnapshot? observed;
    private HotbarConflictResolution? resolution;
    private long contextSince, unchangedSince;
    private bool running;
    public string Status { get; private set; } = "Disabled.";
    public bool Faulted { get; private set; }
    public IReadOnlyList<SharedSlotConflict> Conflicts { get; private set; } = [];
    public IReadOnlyList<int> SharedBars { get; private set; } = [];

    public void Start()
    {
        Stop();
        running = true;
        Status = "Waiting for ready hotbars.";
    }

    public void Stop()
    {
        running = false;
        character = null;
        state = new();
        observed = null;
        resolution = null;
        Conflicts = [];
        SharedBars = [];
        Faulted = false;
        Status = "Disabled.";
    }

    public void ResolveConflicts(bool usePvP)
    {
        if (!Faulted && observed is not null && Conflicts.Count != 0)
            resolution = new(observed.Context, Conflicts.ToList(), usePvP);
    }

    public void Tick(long now)
    {
        if (!running || Faulted) return;
        try { TickCore(now); }
        catch (Exception ex)
        {
            Faulted = true;
            resolution = null;
            Status = "Sync paused: " + ex.Message + " Disable and re-enable the feature to retry.";
            throw;
        }
    }

    private void TickCore(long now)
    {
        var snapshot = access.Capture(out var waiting);
        if (snapshot is null)
        {
            observed = null;
            resolution = null;
            Conflicts = [];
            SharedBars = [];
            Status = waiting;
            return;
        }
        snapshot.Validate();
        if (character != snapshot.Context.Character)
        {
            var loaded = store.Load(snapshot.Context.Character);
            loaded.Validate();
            state = loaded;
            character = snapshot.Context.Character;
            observed = null;
            resolution = null;
        }
        SharedBars = snapshot.Bars.Keys.Order().ToArray();
        if (observed is null || observed.Context != snapshot.Context)
        {
            observed = snapshot;
            contextSince = unchangedSince = now;
            resolution = null;
            Conflicts = [];
            Status = "Waiting for character, job and hotbar changes to settle.";
            return;
        }
        if (!snapshot.SameAs(observed))
        {
            observed = snapshot;
            unchangedSince = now;
            Conflicts = [];
        }
        if (now - contextSince < 2_000 || now - unchangedSince < 500)
        {
            Status = "Waiting for hotbar edits to settle.";
            return;
        }
        var requested = resolution;
        resolution = null;
        if (requested is not null && (requested.Context != snapshot.Context || requested.Conflicts.Any(c =>
            !snapshot.Bars.ContainsKey(c.Bar) || snapshot.Read(c.Bar, c.Slot, false) != c.PvE || snapshot.Read(c.Bar, c.Slot, true) != c.PvP)))
        {
            Status = "The conflicting slots changed. Review their current values and choose again.";
            Conflicts = SharedHotbarPlanner.Build(snapshot, state).Conflicts;
            return;
        }
        var plan = SharedHotbarPlanner.Build(snapshot, state, requested);
        if (plan.Edits.Count != 0 || !state.SameAs(plan.State)) Apply(snapshot, plan);
        Conflicts = plan.Conflicts;
        Status = Conflicts.Count != 0
            ? $"{Conflicts.Count} conflicting slot(s) need a choice. Other shared slots continue syncing."
            : SharedBars.Count == 0 ? "No hotbars are marked Shared in Character Configuration."
            : $"Syncing {SharedBars.Count} shared hotbar(s) in both directions.";
    }

    private void Apply(SharedHotbarSnapshot before, SharedHotbarPlan plan)
    {
        // Backup failures stop before the first native write.
        if (plan.Edits.Count != 0) store.Backup(before, plan);
        var current = access.Capture(out _);
        if (current is null || !before.SameAs(current))
            throw new InvalidOperationException("Hotbars changed before the sync could be applied.");
        var attempted = new List<SharedSlotEdit>();
        try
        {
            foreach (var edit in plan.Edits)
            {
                attempted.Add(edit); // Includes a native write that succeeds and then fails verification.
                access.Write(before.Context, edit);
            }
            var after = access.Capture(out _);
            if (after is null || after.Context != before.Context)
                throw new InvalidOperationException("The hotbar context changed during sync.");
            foreach (var (bar, values) in before.Bars)
                for (var slot = 0; slot < values.PvE.Length; slot++)
                    foreach (var pvp in new[] { false, true })
                    {
                        var edit = plan.Edits.Find(e => e.Bar == bar && e.Slot == slot && e.PvP == pvp);
                        if (after.Read(bar, slot, pvp) != (edit?.After ?? before.Read(bar, slot, pvp)))
                            throw new InvalidOperationException("Hotbars changed during verification.");
                    }
            if (attempted.Count != 0) access.Flush(before.Context);
            store.Save(before.Context.Character, plan.State);
            state = plan.State;
            observed = after;
        }
        catch (Exception failure)
        {
            var rollbackErrors = new List<Exception>();
            foreach (var edit in attempted.AsEnumerable().Reverse())
            {
                try
                {
                    var actual = access.Capture(out _);
                    if (actual is null || actual.Context != before.Context)
                        throw new InvalidOperationException("Context changed; backup retained for recovery.");
                    var value = actual.Read(edit.Bar, edit.Slot, edit.PvP);
                    if (value == edit.Before) continue;
                    if (value != edit.After) throw new InvalidOperationException("A slot changed again; backup retained for recovery.");
                    access.Write(before.Context, edit with { Before = edit.After, After = edit.Before });
                }
                catch (Exception ex) { rollbackErrors.Add(ex); }
            }
            if (attempted.Count != 0)
            {
                try { access.Flush(before.Context); }
                catch (Exception ex) { rollbackErrors.Add(ex); }
            }
            if (rollbackErrors.Count != 0)
                throw new AggregateException("Sync failed and could not completely roll back. The before-sync backup is retained.", [failure, .. rollbackErrors]);
            throw;
        }
    }
}
