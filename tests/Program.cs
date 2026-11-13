using Aetherworks.Features;
using Aetherworks.Tweaks;
using Aetherworks.Transfers;

var checks = 0;
checks += ReturnFlowTests.Run();
checks += SharedHotbarTests.Run();
void Check(bool value, string name)
{
    if (!value) throw new Exception(name);
    checks++;
}

string[] party = ["Player", "Alisaie", "Resistance Fighter", "Alphinaud"];
string?[] labels = new string?[4];
var ranks = new int[11];
var damage = new Dictionary<string, DamageEntry>(StringComparer.OrdinalIgnoreCase)
{
    ["YOU"] = new(67151, "44%"), ["Alisaie (YOU)"] = new(37996, "25%"), ["Resistance Fighter (YOU)"] = new(25724, "16%"),
    ["Alphinaud (YOU)"] = new(20630, "13%"), ["Enemy"] = new(999999, "99%")
};
Aetherworks.Features.PartyDamageRanks.Calculate(party, 1, "Player", damage, labels, ranks);
Check(labels.SequenceEqual(["44% ", "25% ", "16% ", "13% "]), "Duty support ranks and parser-provided shares");
Check(ranks.Take(4).SequenceEqual([1, 2, 3, 4]), "Separate rank values for native slot badges");
damage["Player@World"] = damage["YOU"];
damage.Remove("YOU");
Aetherworks.Features.PartyDamageRanks.Calculate(party, 1, "Player", damage, labels, ranks);
Check(labels[0] == "44% ", "World-qualified party member included");

PartyDamageRanks.Calculate(["Player"], 1, "Player", damage, labels, ranks);
Check(labels[0] == "44% ", "One matching row preserves the parser percentage");
damage["Player@World"] = new(67151, null);
PartyDamageRanks.Calculate(["Player"], 1, "Player", damage, labels, ranks);
Check(labels[0] == "  -% ", "Missing parser percentage remains unknown");

var largeParty = Enumerable.Range(1, 11).Select(i => $"Member {i}").ToArray();
var shares = new Dictionary<string, DamageEntry> { [largeParty[0]] = new(93, "93%"), [largeParty[1]] = new(7, "7%") };
var padded = new string?[largeParty.Length];
Aetherworks.Features.PartyDamageRanks.Calculate(largeParty, 11, null, shares, padded, ranks);
Check(padded[0] == "93% " && padded[1] == "  7% ", "Percentage-only labels on eleven-member roster");
Aetherworks.Features.PartyDamageRanks.Calculate(largeParty[..10], 10, null, shares, padded, ranks);
Check(padded[1] == "  7% ", "Align single-digit share on ten-member roster");
shares[largeParty[1]] = new(0, "0%");
shares[largeParty[0]] = new(93, "100%");
Aetherworks.Features.PartyDamageRanks.Calculate(largeParty, 11, null, shares, padded, ranks);
Check(padded[0] == "100% " && padded[1] == "  0% ", "Zero and one hundred percent fit");

Check(padded[2] == "  -% ", "Unreported occupied row gets an unknown label");
var icon = "\uE040 ";
var level = "\uE06A\uE069\uE060";
var native = System.Text.Encoding.UTF8.GetBytes(icon + level + " Alisaie");
var (start, end) = Aetherworks.Features.PartyLevelText.Locate(native);
Check(System.Text.Encoding.UTF8.GetString(native[..start]) == icon &&
    System.Text.Encoding.UTF8.GetString(native[end..]) == " Alisaie", "Replace only the level following a sync glyph");
Check(Aetherworks.Features.PartyLevelText.Locate(System.Text.Encoding.UTF8.GetBytes(icon + "Alisaie")) ==
    (start, start), "Preserve sync glyph when the row has no level");
Check(Aetherworks.Features.PartyLevelText.Locate(System.Text.Encoding.UTF8.GetBytes(icon)) ==
    (-1, -1), "Icon-only text before another payload is preserved");

byte[] syncPayload = [2, 0x12, 2, 0x3F, 3];
var levelBytes = System.Text.Encoding.UTF8.GetBytes(level);
byte[] syncedRow = [..syncPayload, ..levelBytes, .." Alisaie"u8];
Check(PartyLevelText.RemoveLevel(syncedRow, syncPayload.Length, syncPayload.Length + levelBytes.Length)
    .AsSpan().SequenceEqual((ReadOnlySpan<byte>)[..syncPayload, .."Alisaie"u8, 0]),
    "Sync icon uses its native padding before the name");
Check(PartyLevelText.RemoveLevel(syncedRow.AsSpan(syncPayload.Length), 0, levelBytes.Length)
    .AsSpan().SequenceEqual("Alisaie\0"u8), "Unsynced name has no duplicate percentage");

var withdrawal = new LeaveOneWithdrawal(42, 4000, 5, 100, 1, 99);
Check(withdrawal.Amount == 98 && withdrawal.Matches(42, 100, 1, 99), "Withdraw a stack minus its reserved copy");
Check((withdrawal with { Quantity = 2 }).Amount == 1, "Two-item stack leaves one");
Check((withdrawal with { Quantity = 1 }).Amount == 0 && !withdrawal.Matches(42, 100, 1, 1), "Single copy cannot be withdrawn");
Check(!withdrawal.Matches(43, 100, 1, 99) && !withdrawal.Matches(42, 101, 1, 99) &&
    !withdrawal.Matches(42, 100, 0, 99) && !withdrawal.Matches(42, 100, 1, 98),
    "Character, item, quality or stack changes invalidate a queued withdrawal");
Check(!(withdrawal with { Quantity = 0 }).Matches(42, 100, 1, 0), "Empty stack cannot be withdrawn");

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
Console.WriteLine($"Passed {checks} lifecycle, Return fallback, shared hotbar and withdrawal checks.");
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
