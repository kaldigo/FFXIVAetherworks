using System.Text.Json;
using Aetherworks.Hotbars;

static class SharedHotbarTests
{
    private const string CharacterA = "0123456789ABCDEF", CharacterB = "FEDCBA9876543210";
    public static int Run()
    {
        var checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        void Throws(Action action, string message)
        {
            try { action(); } catch (Exception) { checks++; return; }
            throw new Exception(message);
        }

        var h = new Harness(Snapshot(pvp: true, bars: [0, 17]));
        h.Access.Current.Bars[17].PvE[15] = new(8, 0); // Macro zero is a real assignment.
        h.Settle();
        Check(h.Read(0, 0, true) == new HotbarCommand(1, 100), "First sync uses PvE even while PvP is active");
        Check(h.Read(17, 15, true) == new HotbarCommand(8, 0), "Shared cross hotbar slot 16 and macro zero are preserved");
        Check(h.Store.Backups.Single().Read(0, 0, true) == new HotbarCommand(1, 200), "Initial backup preserves replaced PvP assignments");
        var writes = h.Access.Writes;
        h.Settle();
        Check(h.Access.Writes == writes && h.Store.Saves == 1, "Stable assignments do not echo or rewrite the state file");
        h.Access.Current.Bars[0].PvP[0] = new(2, 500);
        h.Settle();
        Check(h.Read(0, 0, false) == new HotbarCommand(2, 500), "PvP edits propagate to PvE");
        h.Access.Current.Bars[0].PvE[0] = default;
        h.Access.Current.Bars[0].PvE[1] = new(2, 500);
        h.Settle();
        Check(h.Read(0, 0, true) == default && h.Read(0, 1, true) == new HotbarCommand(2, 500), "Moving a slot propagates the new position and clearing");
        h.Access.Current.Bars[0].PvE[2] = new(10, 4);
        h.Settle();
        Check(h.Read(0, 2, false) == new HotbarCommand(1, 3) && h.Read(0, 2, true) == new HotbarCommand(1, 29057), "Sprint gets the native action for each mode");
        h.Access.Current.Bars[0].PvP[2] = default;
        h.Settle();
        Check(h.Read(0, 2, false) == default, "Removing PvP Sprint also removes PvE Sprint");

        h.Engine.Stop();
        h.Access.Current.Bars[0].PvP[1] = new(2, 501);
        writes = h.Access.Writes;
        h.Settle();
        Check(h.Access.Writes == writes, "Disabled engine never writes");
        h.Engine.Start();
        h.Access.Current = h.Access.Current with { Context = h.Access.Current.Context with { Job = 30, PvP = false, Territory = 200 } };
        h.Settle();
        Check(h.Read(0, 1, false) == new HotbarCommand(2, 501), "Reload and job/mode changes resume two-way history instead of initializing again");
        h.Access.Current.Bars[0].PvE[3] = new(1, 700);
        h.Access.Current.Bars[0].PvP[4] = new(1, 800);
        h.Settle();
        Check(h.Read(0, 3, true).Id == 700 && h.Read(0, 4, false).Id == 800, "Independent edits on both sides merge");
        h.Access.Current.Bars[0].PvE[5] = new(1, 900);
        h.Access.Current.Bars[0].PvP[5] = new(1, 901);
        h.Access.Current.Bars[0].PvE[6] = new(1, 902);
        h.Settle();
        Check(h.Engine.Conflicts.Count == 1 && h.Read(0, 5, false).Id == 900 && h.Read(0, 5, true).Id == 901,
            "Diverging same-slot changes are preserved as a conflict");
        Check(h.Read(0, 6, true).Id == 902, "Unrelated slots continue syncing during conflicts");
        h.Engine.ResolveConflicts(true);
        h.Settle();
        Check(h.Read(0, 5, false).Id == 901 && h.Engine.Conflicts.Count == 0, "Choosing PvP resolves only the captured conflicts");
        h.Access.Current.Bars[0].PvE[5] = new(1, 910);
        h.Access.Current.Bars[0].PvP[5] = new(1, 911);
        h.Settle();
        h.Engine.ResolveConflicts(false);
        h.Access.Current.Bars[0].PvP[5] = new(1, 912);
        h.Settle();
        Check(h.Read(0, 5, false).Id == 910 && h.Read(0, 5, true).Id == 912 && h.Engine.Conflicts.Count == 1,
            "A stale conflict choice cannot overwrite a newer assignment");
        h.Engine.ResolveConflicts(false);
        h.Settle();
        Check(h.Read(0, 5, true).Id == 910, "Choosing PvE resolves a current conflict");
        h.Access.Current.Bars[0].PvE[5] = h.Access.Current.Bars[0].PvP[5] = new(1, 920);
        h.Settle();
        Check(h.Engine.Conflicts.Count == 0 && h.Store.States[CharacterA].Baselines[0][5].Id == 920,
            "Identical edits in both modes advance history without conflict");

        h.Access.Current = Snapshot(character: CharacterB);
        h.Settle();
        Check(h.Store.States.Count == 2 && h.Read(0, 0, true).Id == 100, "Each character initializes independently");
        h.Access.Current = new(h.Access.Current.Context with { SharedMask = 0 }, []);
        h.Settle();
        Check(h.Store.States[CharacterB].Baselines.Count == 0 && h.Engine.SharedBars.Count == 0, "Unshared bars leave the tracked baseline");
        h.Access.Current = Snapshot(character: CharacterB);
        h.Settle();
        Check(h.Read(0, 0, true).Id == 100, "Sharing a previously unshared bar initializes from PvE");
        var invalid = Snapshot();
        invalid.Bars.Add(1, new(new HotbarCommand[12], new HotbarCommand[12]));
        Throws(() => SharedHotbarPlanner.Build(invalid, new()), "Capture must reject unshared bars");

        var paused = new Harness(Snapshot());
        paused.Access.Ready = false;
        paused.Settle();
        Check(paused.Access.Writes == 0, "Unavailable or combat-blocked capture cannot write");
        paused.Access.Ready = true;
        paused.Engine.Tick(3_000);
        paused.Engine.Tick(3_100);
        Check(paused.Access.Writes == 0, "Context must settle before initial writes");
        paused.Engine.Tick(5_100);
        Check(paused.Access.Writes != 0, "Ready stable context resumes syncing");

        var backupFailure = new Harness(Snapshot());
        backupFailure.Store.FailBackup = true;
        Throws(backupFailure.Settle, "Backup failure should stop sync");
        Check(backupFailure.Access.Writes == 0 && backupFailure.Engine.Faulted, "Backup failure pauses before writes");
        backupFailure.Settle();
        Check(backupFailure.Access.Writes == 0, "Faulted sync does not automatically retry");

        var writeFailure = new Harness(Snapshot());
        var original = Copy(writeFailure.Access.Current);
        writeFailure.Access.AfterWrite = count => { if (count == 1) throw new IOException("Injected write verification failure"); };
        Throws(writeFailure.Settle, "Write failure should stop sync");
        Check(writeFailure.Access.Current.SameAs(original) && writeFailure.Store.States.Count == 0,
            "Rollback includes a write that completed before verification failed");
        var saveFailure = new Harness(Snapshot());
        original = Copy(saveFailure.Access.Current);
        saveFailure.Store.FailSave = true;
        Throws(saveFailure.Settle, "State persistence failure should stop sync");
        Check(saveFailure.Access.Current.SameAs(original) && saveFailure.Store.Backups.Count == 1,
            "State save failure restores assignments and retains backup");

        var interrupted = new Harness(Snapshot());
        interrupted.Access.Current.Bars[0].PvE[1] = new(1, 101);
        var otherCharacter = Snapshot(character: CharacterB);
        interrupted.Access.AfterWrite = count => { if (count == 1) interrupted.Access.Current = Copy(otherCharacter); };
        Throws(interrupted.Settle, "Context switch mid-write should stop sync");
        Check(interrupted.Access.Current.SameAs(otherCharacter) && interrupted.Access.Writes == 1 && interrupted.Store.Backups.Count == 1,
            "Rollback never writes into another character after a context switch");

        var racing = new Harness(Snapshot());
        racing.Store.BeforeBackup = () => racing.Access.Current.Bars[0].PvE[0] = new(1, 999);
        Throws(racing.Settle, "A changed source should abort the batch");
        Check(racing.Access.Writes == 0 && racing.Read(0, 0, false).Id == 999, "Source changes during backup are not overwritten");

        var directory = Path.Combine(AppContext.BaseDirectory, "hotbar-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var files = new SharedHotbarStore(directory);
            var snapshot = Snapshot();
            var plan = SharedHotbarPlanner.Build(snapshot, files.Load(CharacterA));
            files.Backup(snapshot, plan);
            files.Save(CharacterA, plan.State);
            Check(files.Load(CharacterA).SameAs(plan.State), "Persisted history survives a fresh store instance");
            var savedBackups = Directory.GetFiles(Path.Combine(directory, "backups", CharacterA));
            Check(savedBackups.Length == 2 && savedBackups.Any(p => Path.GetFileName(p).StartsWith("initial-")),
                "Initial backup is separate from the rolling backup");
            using var json = JsonDocument.Parse(File.ReadAllText(savedBackups[0]));
            Check(json.RootElement.GetProperty("Before").GetProperty("Bars").GetProperty("0").GetProperty("PvP")[0].GetProperty("Id").GetUInt32() == 200,
                "Backup JSON preserves original assignments without recursive computed properties");
            var statePath = Path.Combine(directory, CharacterA + ".json");
            foreach (var corrupt in new[] { "{", "{}", "{\"Version\":2,\"Baselines\":{}}", "{\"Version\":1,\"Baselines\":{\"0\":[]}}" })
            {
                File.WriteAllText(statePath, corrupt);
                Throws(() => files.Load(CharacterA), "Corrupt or incomplete state must not silently initialize again");
                Check(File.ReadAllText(statePath) == corrupt, "Unreadable state remains untouched for recovery");
            }
            Throws(() => files.Load("../other"), "Invalid character keys cannot escape storage directory");
        }
        finally { Directory.Delete(directory, true); }
        return checks;
    }

    private static SharedHotbarSnapshot Snapshot(string character = CharacterA, bool pvp = false, int[]? bars = null)
    {
        bars ??= [0];
        var values = bars.ToDictionary(bar => bar, bar => new SharedBar(new HotbarCommand[SharedHotbarSnapshot.SlotCount(bar)], new HotbarCommand[SharedHotbarSnapshot.SlotCount(bar)]));
        foreach (var bar in values.Values) { bar.PvE[0] = new(1, 100); bar.PvP[0] = new(1, 200); }
        return new(new(character, 19, pvp, 100, bars.Aggregate(0u, (mask, bar) => mask | (1u << bar))), values);
    }
    private static SharedHotbarSnapshot Copy(SharedHotbarSnapshot snapshot) => new(snapshot.Context,
        snapshot.Bars.ToDictionary(p => p.Key, p => new SharedBar([.. p.Value.PvE], [.. p.Value.PvP])));
    private static SharedHotbarState CopyState(SharedHotbarState state) => new() { Baselines = state.Baselines.ToDictionary(p => p.Key, p => p.Value.ToArray()) };

    private sealed class Harness
    {
        public readonly FakeAccess Access;
        public readonly FakeStore Store = new();
        public readonly SharedHotbarEngine Engine;
        private long now;
        public Harness(SharedHotbarSnapshot snapshot) { Access = new(snapshot); Engine = new(Access, Store); Engine.Start(); }
        public HotbarCommand Read(int bar, int slot, bool pvp) => Access.Current.Read(bar, slot, pvp);
        public void Settle() { Engine.Tick(now); now += 2_100; Engine.Tick(now); }
    }
    private sealed class FakeAccess(SharedHotbarSnapshot snapshot) : ISharedHotbarAccess
    {
        public SharedHotbarSnapshot Current = snapshot;
        public bool Ready = true;
        public int Writes;
        public Action<int>? AfterWrite;
        public SharedHotbarSnapshot? Capture(out string reason) { reason = "Waiting"; return Ready ? Copy(Current) : null; }
        public void Write(HotbarContext context, SharedSlotEdit edit)
        {
            if (Current.Context != context || Current.Read(edit.Bar, edit.Slot, edit.PvP) != edit.Before) throw new InvalidOperationException("Context or target changed");
            (edit.PvP ? Current.Bars[edit.Bar].PvP : Current.Bars[edit.Bar].PvE)[edit.Slot] = edit.After;
            Writes++;
            AfterWrite?.Invoke(Writes);
        }
        public void Flush(HotbarContext context) { if (Current.Context != context) throw new InvalidOperationException("Flush context changed"); }
    }
    private sealed class FakeStore : ISharedHotbarStore
    {
        public Dictionary<string, SharedHotbarState> States = [];
        public List<SharedHotbarSnapshot> Backups = [];
        public int Saves;
        public bool FailSave, FailBackup;
        public Action? BeforeBackup;
        public SharedHotbarState Load(string character) => States.TryGetValue(character, out var state) ? CopyState(state) : new();
        public void Save(string character, SharedHotbarState state)
        { if (FailSave) throw new IOException("Injected save failure"); States[character] = CopyState(state); Saves++; }
        public void Backup(SharedHotbarSnapshot before, SharedHotbarPlan plan)
        { if (FailBackup) throw new IOException("Injected backup failure"); Backups.Add(Copy(before)); BeforeBackup?.Invoke(); }
    }
}
