using Aetherworks.Tweaks;

var checks = 0;
checks += ReturnFlowTests.Run();
checks += SharedHotbarTests.Run();
void Check(bool value, string name)
{
    if (!value) throw new Exception(name);
    checks++;
}

var first = new FakeTweak("first");
var second = new FakeTweak("second");
var saved = new HashSet<string>();
var saves = 0;
var manager = new TweakManager([first, second], saved, () => saves++, (_, _) => { });
manager.RestoreEnabled();
Check(first.Enables == 0 && second.Enables == 0, "New features default off");
manager.SetEnabled(manager.Entries[0], true);
manager.SetEnabled(manager.Entries[0], true);
Check(first.Enables == 1 && second.Enables == 0 && saved.SetEquals(["first"]), "Independent and idempotent enable");
manager.SetEnabled(manager.Entries[0], false);
Check(first.Disables == 1 && saved.Count == 0, "Disable releases behavior and saves choice");
manager.SetEnabled(manager.Entries[1], true);
manager.Dispose();
manager.Dispose();
Check(second.Disables == 1 && saved.Contains("second") && saves > 0, "Unload preserves choices and is idempotent");

var restored = new FakeTweak("second");
using (var reload = new TweakManager([restored, new FakeTweak("new")], saved, () => { }, (_, _) => { }))
{
    reload.RestoreEnabled();
    Check(restored.Enables == 1 && !reload.Entries[1].IsEnabled, "Reload restores only opted-in features");
}

var broken = new FakeTweak("broken") { FailEnable = true };
var healthy = new FakeTweak("healthy");
var failures = new HashSet<string> { "broken", "healthy" };
using (var isolated = new TweakManager([broken, healthy], failures, () => { }, (_, _) => { }))
{
    isolated.RestoreEnabled();
    Check(broken.Disables == 1 && !failures.Contains("broken") && isolated.Entries[0].Error != null,
        "Partial enable is cleaned up and not restored again");
    Check(isolated.Entries[1].IsEnabled, "One broken feature does not block others");
    healthy.FailDisable = true;
    isolated.SetEnabled(isolated.Entries[1], false);
    Check(isolated.Entries[1].NeedsCleanup && !failures.Contains("healthy"), "Cleanup failure is visible and saved off");
    isolated.SetEnabled(isolated.Entries[1], true);
    Check(healthy.Enables == 1, "Cannot re-enable while cleanup is pending");
    healthy.FailDisable = false;
    isolated.SetEnabled(isolated.Entries[1], false);
    Check(!isolated.Entries[1].NeedsCleanup && isolated.Entries[1].Error == null, "Cleanup can be retried");
}

var saveFails = true;
using (var persistence = new TweakManager([new FakeTweak("save")], [],
    () => { if (saveFails) throw new IOException(); }, (_, _) => { }))
{
    persistence.SetEnabled(persistence.Entries[0], true);
    Check(persistence.SaveError != null && persistence.Entries[0].IsEnabled, "Save failure is visible without losing runtime state");
    saveFails = false;
    persistence.Save();
    Check(persistence.SaveError == null, "Saving can be retried");
}
try
{
    using var invalid = new TweakManager([new FakeTweak("same"), new FakeTweak("same")], [], () => { }, (_, _) => { });
    throw new Exception("Duplicate IDs accepted");
}
catch (ArgumentException) { checks++; }
Console.WriteLine($"Passed {checks} lifecycle, Return fallback and shared hotbar checks.");
sealed class FakeTweak(string id) : ITweak
{
    public string Id => id;
    public string Name => id;
    public string Description => "Test feature";
    public string Category => "Tests";
    public int Enables { get; private set; }
    public int Disables { get; private set; }
    public bool FailEnable { get; set; }
    public bool FailDisable { get; set; }
    public void Enable() { Enables++; if (FailEnable) throw new InvalidOperationException(); }
    public void Disable() { Disables++; if (FailDisable) throw new InvalidOperationException(); }
}
