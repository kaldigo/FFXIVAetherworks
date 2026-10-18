using Aetherworks.Tweaks;
using Aetherworks.Ownership;
using Aetherworks.Glamour;
using Aetherworks.Crafting;
using Aetherworks.Transfers;
using Aetherworks.Actions;

var checks = 0;
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
var locations = new (string Id, string Label)[] { ("inventory", "Inventory"), ("saddlebag", "Saddlebag") };
var store = new OwnershipStore();
Check(store.Find(100, locations).All(r => r.Quantity == null), "Unobserved locations show Missing");
store.Replace("inventory", [(100, 2), (100, 3), (0, 4), (101, 0)]);
Check(store.Find(100, locations)[0].Quantity == 5, "Multiple stacks combine into one item total");
Check(store.Find(101, locations).Count == 1, "Known absent items are hidden while unknown locations remain");
store.Replace("saddlebag", []);
Check(store.Find(101, locations).Count == 0, "Known-empty locations do not produce tooltip lines");
store.MarkSaved();
store.Replace("inventory", [(100, 5)]);
Check(!store.Dirty, "Unchanged quantities do not cause repeated saves");
store.Replace("inventory", [(100, 1)]);
Check(store.Find(100, locations).Single().Quantity == 1 && store.Dirty, "Refresh replaces quantities rather than accumulating");
var restoredStore = new OwnershipStore();
restoredStore.Load(store.Serialize());
Check(restoredStore.Find(100, locations).Single().Quantity == 1 && !restoredStore.Dirty,
    "Snapshot round trip preserves quantities and empty locations");
var anotherCharacter = new OwnershipStore();
Check(anotherCharacter.Find(100, locations).All(r => r.Quantity == null), "Fresh character has no previous character's inventory");
foreach (var invalidJson in new[] { "null", "{", "{\"Version\":2}", "{\"Locations\":null}",
    "{\"Locations\":{\"inventory\":{\"100\":-1}}}" })
{
    try { restoredStore.Load(invalidJson); throw new Exception("Invalid snapshot accepted"); }
    catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
    {
        Check(restoredStore.Find(100, locations).Single().Quantity == 1, "Failed load preserves existing snapshot");
    }
}
store.Replace("inventory", []);
Check(store.Find(100, locations).Count == 0, "Removing final item hides the location");
var oldSnapshot = new OwnershipStore();
oldSnapshot.Load("{\"Version\":1,\"Locations\":{\"inventory\":{\"100\":2}}}");
Check(!oldSnapshot.Snapshot.RetainersKnown && oldSnapshot.RetainerLocations().Single().Label == "Retainers",
    "Existing snapshots migrate with an unknown retainer roster");
var alice = OwnershipStore.RetainerKey(123);
var bob = OwnershipStore.RetainerKey(456);
oldSnapshot.ReplaceRetainers(new Dictionary<string, string> { [alice] = "Alice", [bob] = "Bob" });
oldSnapshot.Replace(alice, [(100, 7)]);
var retainerRows = oldSnapshot.Find(100, oldSnapshot.RetainerLocations());
Check(retainerRows[0] == new LocationResult("Retainer Alice", 7) && retainerRows[1].Quantity == null,
    "Named unopened retainers remain Missing separately");
oldSnapshot.ReplaceRetainers(new Dictionary<string, string> { [alice] = "Renamed", [bob] = "Bob" });
Check(oldSnapshot.Find(100, oldSnapshot.RetainerLocations()).Single(r => r.Label == "Retainer Renamed").Quantity == 7,
    "Retainer rename preserves contents by stable ID");
var rosterReload = new OwnershipStore();
rosterReload.Load(oldSnapshot.Serialize());
Check(rosterReload.Snapshot.RetainersKnown && rosterReload.Snapshot.Retainers[alice] == "Renamed" &&
    rosterReload.Snapshot.Locations[alice][100] == 7, "Retainer names and contents persist together");
rosterReload.ReplaceRetainers(new Dictionary<string, string> { [bob] = "Bob" });
Check(!rosterReload.Snapshot.Locations.ContainsKey(alice), "Dismissed retainer contents no longer contribute");
rosterReload.ReplaceRetainers(new Dictionary<string, string>());
Check(!rosterReload.RetainerLocations().Any(), "Confirmed empty roster is distinct from missing roster");

var gate = new StableObservation<int[]>();
bool Same(int[] a, int[] b) => a.SequenceEqual(b);
Check(!gate.Accept(alice, [1], 0, Same) && !gate.Accept(alice, [1], 500, Same) && gate.Accept(alice, [1], 1000, Same),
    "Storage observation requires repeat reads over the settling interval");
Check(!gate.Accept(bob, [1], 1500, Same), "Switching retainer resets readiness even for identical contents");
Check(!gate.Accept(bob, [2], 2500, Same) && !gate.Accept(bob, [2], 3000, Same) && gate.Accept(bob, [2], 3500, Same),
    "Changing contents restart the stability window");
gate.Reset();
Check(!gate.Accept(bob, [2], 5000, Same), "Closing or unloading storage invalidates the pending observation");
Check(!gate.Accept(bob, [], 5500, Same) && gate.Accept(bob, [], 6500, Same), "Stable empty observation is accepted");

var outfitDefinitions = new Dictionary<uint, uint[]> { [900] = [0, 201, 202, 0, 203] };
var calls = new List<(int Slot, int Part)>();
var dresser = DresserContents.Expand([100, 900, 0, 900], outfitDefinitions, (slot, part) =>
{
    calls.Add((slot, part));
    return slot == 1 && part == 2 || slot == 3 && part == 4;
});
Check(dresser.SequenceEqual(new (uint, long)[] { (100, 1), (202, 1), (203, 1) }),
    "Partial outfits expand only owned members, alongside standalone items");
Check(calls.Contains((1, 2)) && calls.Contains((3, 4)) && !calls.Any(c => c.Part is 0 or 3),
    "Outfit checks preserve physical dresser slot and original column indices");
Check(!dresser.Any(i => i.ItemId == 900 || i.ItemId == 201), "Set tokens and locked pieces never imply ownership");
store.Replace("dresser", dresser);
store.Replace("armoire", [(500, 1)]);
var glamourReload = new OwnershipStore();
glamourReload.Load(store.Serialize());
Check(glamourReload.Snapshot.Locations["dresser"][202] == 1 && glamourReload.Snapshot.Locations["armoire"][500] == 1,
    "Glamour ownership survives reload independently of regular inventory");
store.Replace("dresser", DresserContents.Expand([], outfitDefinitions, (_, _) => throw new Exception()));
Check(store.Snapshot.Locations["dresser"].Count == 0, "Confirmed empty dresser removes previous pieces");
var target = new Appearance(1000, "Target", 4, 0, 0x10000002A, 0, 2);
var exact = target with { ItemId = 1001, Name = "Equivalent", ArmoireEligible = true };
var variant = target with { ItemId = 1002, Name = "Variant", ModelMain = 0x20000002A };
var dye = target with { ItemId = 1003, Name = "Single dye", DyeChannels = 1 };
var secondary = target with { ItemId = 1004, Name = "Secondary", ModelSub = 7 };
var unrelated = target with { ItemId = 1005, ModelMain = 0x10000002B };
var otherSlot = target with { ItemId = 1006, Slot = 5 };
var otherWeapon = target with { ItemId = 1007, WeaponKind = 1 };
Check(AppearanceCatalog.Compare(target, exact) == AppearanceMatch.Exact, "Different item IDs can share an exact appearance");
foreach (var close in new[] { variant, dye, secondary })
    Check(AppearanceCatalog.Compare(target, close) == AppearanceMatch.Close, "Variant, dye count and secondary model changes are close");
foreach (var different in new[] { unrelated, otherSlot, otherWeapon })
    Check(AppearanceCatalog.Compare(target, different) == AppearanceMatch.None, "Unrelated models, slots and weapon categories cannot match");
var catalog = new AppearanceCatalog([target, exact, variant, dye, secondary, unrelated, otherSlot, otherWeapon,
    target with { ItemId = 2000, Slot = 0 }, target with { ItemId = 2001, ModelMain = 0 }]);
Check(catalog.Candidates(1000).Count == 5 && catalog.Get(2000) == null && catalog.Get(2001) == null,
    "Candidate index excludes unrelated gear and items without appearances");
Check(AppearanceCatalog.Difference(target, dye) == "1 dye channel instead of 2" &&
    AppearanceCatalog.Difference(target, variant) == "Different variant", "Close reasons describe known differences only");
var appearanceStore = new OwnershipStore();
var appearanceLocations = new (string Id, string Label)[] { ("inventory", "Inventory"), (alice, "Retainer Alice") };
IReadOnlyList<string> Tooltip(bool items = false, bool glamour = true, uint item = 1000) =>
    OwnershipTooltip.Compose(item, appearanceStore, catalog, appearanceLocations, items, glamour);
Check(Tooltip().SequenceEqual(new[] { "Inventory: Missing", "Retainer Alice: Missing", "Glamour Dresser: Missing", "Armoire: Missing" }),
    "Glamour-only unknown snapshots include armoire eligibility of an alternative");
Check(Tooltip(false, false).Count == 0 && Tooltip(false, true, 2000).Count == 0,
    "Both toggles off and non-gear glamour-only produce no panel");
foreach (var location in new[] { "inventory", alice, "dresser", "armoire" }) appearanceStore.Replace(location, []);
Check(Tooltip().Count == 0, "Known absent appearances do not show negative ownership rows");
appearanceStore.Replace(alice, [(1001, 1), (1003, 1)]);
Check(Tooltip().Contains("Appearance owned: Equivalent — Retainer Alice") &&
    Tooltip().Contains("Close match: Single dye — Retainer Alice · 1 dye channel instead of 2") &&
    !Tooltip().Any(l => l.StartsWith("Glamour:")), "Held appearances are not collected glamour and do not suppress close matches");
appearanceStore.Replace("dresser", [(1002, 1)]);
Check(Tooltip().Contains("Close match: Variant — Glamour Dresser · Different variant"), "Stored variant is only a close match");
appearanceStore.Replace("armoire", [(1001, 1)]);
Check(Tooltip().Contains("Glamour: Exact match — Armoire") && !Tooltip().Any(l => l.StartsWith("Close match:")),
    "Exact collected alternative suppresses all close rows");
appearanceStore.Replace("dresser", [(1000, 1)]);
Check(Tooltip().Single(l => l.StartsWith("Glamour:")) == "Glamour: Exact match — Glamour Dresser · Armoire",
    "Exact collected appearances produce one summary across both stores");
appearanceStore.Replace("inventory", [(1000, 3)]);
Check(Tooltip(true).Contains("Inventory: 3") && !Tooltip(true).Contains("Appearance owned: Target — Inventory"),
    "Combined toggles avoid repeating hovered gear ownership");
Check(Tooltip().Contains("Appearance owned: Target — Inventory") && !Tooltip().Contains("Inventory: 3"),
    "Glamour-only toggle still reports held matching gear");
Check(Tooltip(true, false).SequenceEqual(new[] { "Inventory: 3" }), "Item-only toggle omits appearance comparisons");
var revision = appearanceStore.Revision;
appearanceStore.MarkSaved();
appearanceStore.Replace("inventory", [(1000, 3)]);
Check(appearanceStore.Revision == revision, "Unchanged snapshots and saving preserve tooltip cache revision");
appearanceStore.Replace("inventory", []);
Check(appearanceStore.Revision > revision && !Tooltip(true).Contains("Inventory: 3"), "Ownership changes invalidate cached tooltip results");
var protectionStore = new OwnershipStore();
GlamourConflict? Conflict(uint id = 1000) => DuplicateGlamourProtection.FindConflict(id, protectionStore, catalog);
Check(Conflict() == null, "Missing snapshots do not invent collected appearances or block deposits");
protectionStore.Replace("inventory", [(1000, 1)]);
protectionStore.Replace(alice, [(1001, 1)]);
Check(Conflict() == null, "Gear in inventory or retainers never blocks glamour storage");
protectionStore.Replace("dresser", [(1002, 1), (1003, 1), (1004, 1)]);
Check(Conflict() == null, "Variants, different dye channel counts and secondary models stay storable");
protectionStore.Replace("armoire", [(1001, 1)]);
Check(Conflict() == new GlamourConflict(1001, "Armoire"), "Exact alternative in armoire blocks dresser storage");
protectionStore.Replace("dresser", [(1000, 1)]);
Check(Conflict() == new GlamourConflict(1000, "Glamour Dresser"), "Identical dresser item blocks storage");
Check(DuplicateGlamourProtection.FindDepositConflict([(1000, true)], protectionStore, catalog) == null,
    "Consolidating an already stored outfit piece remains allowed");
Check(DuplicateGlamourProtection.FindDepositConflict([(1000, true), (1005, false)], protectionStore, catalog) == null,
    "Consolidation can add a genuinely missing outfit appearance");
Check(DuplicateGlamourProtection.FindDepositConflict([(1005, false), (1000, false)], protectionStore, catalog) != null,
    "One duplicate incoming piece rejects a mixed outfit deposit before any pieces move");
protectionStore.Replace("dresser", []);
protectionStore.Replace("armoire", []);
Check(Conflict() == null, "Removing collected matches allows storage again");
protectionStore.Replace("dresser", [(98765, 1)]);
Check(Conflict(98765) != null && Conflict(0) == null, "Same item is protected without model metadata; zero is never an item");
Check(DuplicateGlamourProtection.FindConflict(1000, new OwnershipStore(), catalog) == null,
    "A fresh character cannot inherit another character's protection decision");
protectionStore.Replace("dresser", DresserContents.Expand([9000],
    new Dictionary<uint, uint[]> { [9000] = [1001, 1005] }, (_, part) => part == 0));
Check(Conflict() != null && Conflict(1005) == null,
    "Only unlocked outfit pieces block matching deposits; missing pieces remain allowed");
var recipeStore = new OwnershipStore();
RecipeMaterial[] recipeMaterials = [new(700, "Iron Ore"), new(701, "Fire Shard")];
var unknownMaterials = RecipeMaterialOwnership.Find(recipeMaterials, recipeStore);
Check(unknownMaterials.Count == 2 && unknownMaterials.All(m => m.Indicator == "Missing" && m.TotalLabel == "Total known"),
    "Unchecked recipe storage stays visible even with no known outside materials");
recipeStore.ReplaceRetainers(new Dictionary<string, string> { [alice] = "Alice", [bob] = "Bob" });
recipeStore.Replace("inventory", [(700, 12), (701, 999)]);
recipeStore.Replace("saddlebag", [(700, 8)]);
recipeStore.Replace(alice, [(700, 20), (700, 10), (701, 100)]);
recipeStore.Replace(bob, []);
var materialRows = RecipeMaterialOwnership.Find(recipeMaterials, recipeStore);
Check(materialRows[0].OutsideQuantity == 38 && materialRows[0].Total == 50 && materialRows[0].TotalLabel == "Total owned",
    "Batch planning includes all outside quantities even with ample inventory");
Check(materialRows[0].Locations.SequenceEqual(new[] { new LocationResult("Inventory", 12), new LocationResult("Saddlebag", 8),
    new LocationResult("Retainer Alice", 30) }), "Ingredient breakdown combines stacks and hides observed empty locations");
Check(materialRows[1].OutsideQuantity == 100 && materialRows[1].Total == 1099,
    "Crystal ingredients include tracked inventory and retainer quantities");
recipeStore.ReplaceRetainers(new Dictionary<string, string> { [alice] = "Alice", [bob] = "Bob", ["retainer:789"] = "Carol" });
var incompleteMaterial = RecipeMaterialOwnership.Find(recipeMaterials, recipeStore)[0];
Check(incompleteMaterial.OutsideQuantity == 38 && incompleteMaterial.HasMissingData && incompleteMaterial.TotalLabel == "Total known" &&
    incompleteMaterial.Locations.Contains(new LocationResult("Retainer Carol", null)), "An unopened retainer makes totals explicitly incomplete without hiding known outside stock");
recipeStore.ReplaceRetainers(new Dictionary<string, string> { [alice] = "Renamed" });
Check(RecipeMaterialOwnership.Find(recipeMaterials, recipeStore)[0].Locations.Any(l => l.Label == "Retainer Renamed"),
    "Recipe breakdown follows retainer roster changes");
recipeStore.Replace("saddlebag", []);
recipeStore.Replace(alice, []);
recipeStore.Replace("dresser", [(700, 1)]);
recipeStore.Replace("armoire", [(700, 1)]);
Check(RecipeMaterialOwnership.Find(recipeMaterials, recipeStore).Count == 0,
    "Inventory-only ingredients and stored glamour do not produce outside-material indicators");
recipeStore.Replace(alice, [(1001, 8)]);
Check(RecipeMaterialOwnership.Find([new(1000, "Target")], recipeStore).Count == 0,
    "An appearance-equivalent item is never a substitute for a recipe ingredient");
recipeStore.Replace(alice, [(700, 4)]);
Check(RecipeMaterialOwnership.Find([new(700, "Iron Ore"), new(700, "Iron Ore"), new(0, "Empty")], recipeStore).Count == 1,
    "Duplicate ingredient IDs are listed once and empty ingredient slots are ignored");
Check(RecipeMaterialOwnership.Find([new(999, "Other recipe")], recipeStore).Count == 0,
    "Changing recipe does not retain previous ingredient ownership rows");
Check(RecipeMaterialOwnership.Find([], recipeStore).Count == 0, "No selected ingredients produces no panel");
TransferSlot BagSlot(uint container, ushort slot, uint item, int quantity, byte flags = 0, int stack = 99) =>
    new(container, slot, new(item, flags), quantity, item == 0 ? 0 : stack);
var sourceStack = BagSlot(0, 0, 700, 50);
var secondStack = BagSlot(1, 0, 700, 20);
var bagStack = BagSlot(4000, 0, 700, 99);
var transferView = new TransferView([sourceStack, secondStack, BagSlot(0, 1, 701, 10), BagSlot(0, 2, 700, 10, 1)], [bagStack]);
var entrust = new DuplicateTransfer();
entrust.Start(transferView);
Check(entrust.Advance(transferView, 0) == sourceStack, "Entrust queues existing stackable duplicates even when destination stack is full; game decides placement");
Check(entrust.Advance(transferView, 300) == null && entrust.Running, "Entrust waits for source update before another action");
var partialView = transferView with { Inventory = [sourceStack with { Quantity = 10 }, secondStack, transferView.Inventory[2], transferView.Inventory[3]] };
entrust.Advance(partialView, 600);
Check(entrust.Moved == 40 && entrust.Advance(partialView, 900) == secondStack, "Partial entrust records moved quantity and continues through queued stacks");
var secondMoved = partialView with { Inventory = [partialView.Inventory[0], BagSlot(1, 0, 0, 0), partialView.Inventory[2], partialView.Inventory[3]] };
entrust.Advance(secondMoved, 1200);
Check(entrust.Advance(secondMoved, 1500) == partialView.Inventory[0], "Remainder is retried through the game's entrust action");
var finishedView = secondMoved with { Inventory = [BagSlot(0, 0, 0, 0), secondMoved.Inventory[1], secondMoved.Inventory[2], secondMoved.Inventory[3]] };
entrust.Advance(finishedView, 1800);
entrust.Advance(finishedView, 2100);
Check(!entrust.Running && entrust.Moved == 70, "All duplicate source stacks move; unrelated and incompatible HQ stacks stay out of the queue");
entrust.Start(transferView);
entrust.Advance(transferView, 0);
entrust.Advance(transferView, 5000);
Check(!entrust.Running && entrust.Moved == 0, "Unconfirmed entrust stops without retrying indefinitely or claiming success");
entrust.Start(transferView);
entrust.Advance(transferView, 0);
entrust.Advance(transferView with { Inventory = [sourceStack with { Key = new(999, 0) }, secondStack] }, 300);
Check(!entrust.Running, "Replacing a pending source item stops the queue");
entrust.Start(transferView);
entrust.Stop("Cancelled");
Check(entrust.Advance(transferView, 0) == null && !entrust.Running, "Cancellation discards queued actions");
entrust.Start(new([BagSlot(0, 0, 700, 1, stack: 1), BagSlot(0, 1, 700, 1, flags: 8)], [bagStack]));
entrust.Advance(new([BagSlot(0, 0, 700, 1, stack: 1), BagSlot(0, 1, 700, 1, flags: 8)], [bagStack]), 0);
Check(!entrust.Running && entrust.Moved == 0, "Nonstackable and collectable items are not entrusted");
entrust.Start(transferView);
Check(entrust.Advance(transferView with { Destination = [] }, 0) == null && !entrust.Running,
    "Removed destination matches are rechecked before invoking entrust");
var chestPartial = BagSlot(20002, 0, 700, 30);
var chestEmpty = BagSlot(20002, 1, 0, 0);
var otherChestTab = BagSlot(20001, 0, 700, 1);
Check(ChestDeposit.Destination(sourceStack, [chestEmpty, otherChestTab, chestPartial], 20002) == chestPartial,
    "FC deposit prefers a compatible whole-stack fit on the current tab");
Check(ChestDeposit.Destination(sourceStack, [chestPartial with { Quantity = 90 }, chestEmpty], 20002) == chestEmpty,
    "FC deposit uses an empty slot when a matching stack lacks room");
Check(ChestDeposit.Destination(sourceStack, [otherChestTab], 20002) == null,
    "FC deposit never chooses a destination on another tab");
Check(ChestDeposit.Destination(sourceStack, [chestPartial with { Key = new(700, 1) }], 20002) == null,
    "FC deposit does not merge HQ and NQ");
Check(ChestDeposit.Destination(sourceStack, [chestPartial with { Quantity = 90 }], 20002) == null,
    "FC deposit stops when only insufficient partial space remains; it does not open a split dialog");
Check(ChestDeposit.Destination(sourceStack, [chestEmpty], 22000) == null &&
    ChestDeposit.Destination(sourceStack, [chestEmpty], 22001) == null,
    "Gil and crystal chest containers are never transfer destinations");
Check(ChestDeposit.Destination(sourceStack with { StackSize = 1 }, [chestEmpty], 20002) == null,
    "FC deposit refuses nonstackable or filtered untradable source items");
var chestQueue = new DuplicateTransfer("FC chest");
var chestView = new TransferView([sourceStack, BagSlot(0, 1, 701, 8)], [chestPartial, chestEmpty]);
chestQueue.Start(chestView);
Check(chestQueue.Advance(chestView, 0) == sourceStack, "FC queue uses the same actual-item matching as saddlebag entrust");
chestQueue.Stop("Tab changed");
Check(chestQueue.Advance(chestView, 300) == null, "Stopping on a tab change clears pending FC work");
chestQueue.Start(chestView);
chestQueue.Advance(chestView, 0);
chestQueue.Advance(chestView, 5000);
Check(!chestQueue.Running && chestQueue.Status.Contains("FC chest") && chestQueue.Moved == 0,
    "Rejected or unconfirmed chest moves stop with chest-specific feedback");
var fcStore = new OwnershipStore();
fcStore.ReplaceRetainers(new Dictionary<string, string>());
fcStore.Replace("inventory", [(700, 12)]);
fcStore.Replace("saddlebag", []);
var fcTab1 = OwnershipStore.FcChestKey(1234, 1);
var fcTab2 = OwnershipStore.FcChestKey(1234, 2);
fcStore.Replace(fcTab1, [(700, 30), (700, 8)]);
Check(!fcStore.ItemStorageLocations().Any(l => l.Id.StartsWith("fc-chest")), "FC chest inclusion defaults off");
Check(fcStore.Find(700, fcStore.ItemStorageLocations(true, 1234)).Single(l => l.Label == "FC Chest — Tab 1").Quantity == 38,
    "FC tooltip location combines stacks within the observed tab");
Check(fcStore.Find(700, fcStore.ItemStorageLocations(true, 1234)).Count(l => l.Quantity == null) == 4,
    "Unopened FC tabs remain separately Missing");
fcStore.Replace(fcTab2, []);
Check(!fcStore.Find(700, fcStore.ItemStorageLocations(true, 1234)).Any(l => l.Label == "FC Chest — Tab 2"),
    "Known-empty FC tabs are hidden rather than Missing");
var fcRecipe = RecipeMaterialOwnership.Find([new(700, "Iron Ore")], fcStore, true, 1234).Single();
Check(fcRecipe.OutsideQuantity == 38 && fcRecipe.Total == 50 && fcRecipe.TotalLabel == "Total known",
    "Optional FC quantities contribute to batch planning while unopened tabs keep totals incomplete");
Check(RecipeMaterialOwnership.Find([new(700, "Iron Ore")], fcStore).Count == 0 && fcStore.Snapshot.Locations.ContainsKey(fcTab1),
    "Disabling inclusion removes FC-only indicators without deleting remembered tabs");
for (var tab = 3; tab <= 5; tab++) fcStore.Replace(OwnershipStore.FcChestKey(1234, tab), []);
Check(RecipeMaterialOwnership.Find([new(700, "Iron Ore")], fcStore, true, 1234).Single().TotalLabel == "Total owned",
    "All observed relevant storage permits a complete total");
var fcReload = new OwnershipStore();
fcReload.Load(fcStore.Serialize());
Check(fcReload.Snapshot.Locations[fcTab1][700] == 38 && fcReload.Snapshot.Locations[fcTab2].Count == 0,
    "FC snapshots persist including observed empty tabs without changing the existing schema");
Check(fcReload.Find(700, fcReload.ItemStorageLocations(true, 4321)).Where(l => l.Label.StartsWith("FC Chest")).All(l => l.Quantity == null),
    "Changing companies never reuses another company's tab contents");
Check(fcReload.Find(700, fcReload.ItemStorageLocations(true)).Single(l => l.Label == "FC Chest").Quantity == null,
    "Unavailable FC identity shows Missing instead of an old company's quantities");
fcReload.Replace(fcTab1, []);
Check(RecipeMaterialOwnership.Find([new(700, "Iron Ore")], fcReload, true, 1234).Count == 0,
    "Refreshing a now-empty FC tab removes its old ingredient indicator");
var fcTooltip = OwnershipTooltip.Compose(700, fcStore, catalog, fcStore.ItemStorageLocations(true, 1234), true, false);
Check(fcTooltip.Contains("FC Chest — Tab 1: 38") && !fcTooltip.Any(l => l.Contains("Tab 2")),
    "Item tooltip composition includes FC quantities and hides observed empty tabs");
var fcObservation = new StableObservation<int>();
Check(!fcObservation.Accept(fcTab1, 1, 0, (a, b) => a == b) &&
    !fcObservation.Accept(fcTab2, 1, 1000, (a, b) => a == b) &&
    fcObservation.Accept(fcTab2, 1, 2000, (a, b) => a == b),
    "Switching FC tabs resets the settling interval even for identical contents");
Check(ActionRules.DismountEligible(1, 100, false), "Regular non-mount actions can trigger dismount");
Check(!ActionRules.DismountEligible(1, 5, false) && !ActionRules.DismountEligible(1, 6, false),
    "Teleport and Return never trigger auto dismount");
Check(!ActionRules.DismountEligible(1, 100, true), "Adjusted mount-category actions remain usable while mounted");
Check(ActionRules.DismountEligible(5, 3, false) && ActionRules.DismountEligible(5, 4, false),
    "Limit Break and Sprint support dismount");
Check(!ActionRules.DismountEligible(5, 23, false) && !ActionRules.DismountEligible(2, 100, false),
    "Other general actions and item actions do not trigger dismount");
Check(ActionRules.RetryDismountedAction(2000) && !ActionRules.RetryDismountedAction(2001),
    "Dismount retry includes the two-second boundary but discards late actions");
Check(ActionRules.CancelEligible(1, true, false) && !ActionRules.CancelEligible(1, true, true) &&
    !ActionRules.CancelEligible(2, true, false) && !ActionRules.CancelEligible(1, false, false),
    "Cast cancellation only considers supported regular actions and excludes ground targets");
Check(ActionRules.ShouldTarget(true, false, 1, false, false, false, true),
    "Auto Target finds a target for supported hostile actions with no selected target");
Check(!ActionRules.ShouldTarget(false, true, 1, true, false, false, true),
    "Auto Change Target alone has no effect");
Check(!ActionRules.ShouldTarget(true, false, 1, true, false, false, true),
    "Auto Target alone preserves an existing unsuitable selected target");
Check(ActionRules.ShouldTarget(true, true, 1, true, false, false, true),
    "Both toggles allow replacement of an unsuitable selected target");
Check(!ActionRules.ShouldTarget(true, true, 1, true, true, false, true),
    "Explicit alternative targets, including macro targets, are preserved");
Check(!ActionRules.ShouldTarget(true, true, 1, true, false, true, true),
    "A usable target is not replaced");
Check(!ActionRules.ShouldTarget(true, true, 2, false, false, false, true) &&
    !ActionRules.ShouldTarget(true, true, 1, false, false, false, false),
    "Non-regular actions and non-hostile actions do not auto-target");
Check(ActionRules.InTargetCone(0, false, 0, -1) && !ActionRules.InTargetCone(0, false, 0, 1),
    "Target cone follows camera direction and excludes enemies behind it");
Check(ActionRules.InTargetCone(0, true, 0, 1) && !ActionRules.InTargetCone(0, true, 0, -1),
    "First-person/look-back rotation offset reverses the cone");
Check(ActionRules.InTargetCone(Math.PI / 2, false, -1, 0) &&
    ActionRules.InTargetCone(Math.PI / 2, false, -1, 0.1), "Camera cone wraps correctly through zero radians");
var outsideCone = Math.PI * 0.3501;
Check(!ActionRules.InTargetCone(0, false, Math.Sin(outsideCone), -Math.Cos(outsideCone)),
    "Candidates outside the original 63-degree half-cone are excluded");
var tabRun = new ChestTabRun();
tabRun.Start(42, [20004, 20000, 20002, 20002, 22000]);
Check(tabRun.Next(20002, 0) && tabRun.Page == 20000, "All-tabs sorts and excludes non-item tabs");
Check(tabRun.Observe(42, 20002, 100) && tabRun.Waiting, "Previous tab tolerated only during requested switch");
Check(tabRun.Observe(42, 20000, 200) && !tabRun.Waiting, "Requested tab selection is acknowledged");
Check(tabRun.Next(20000, 300, 15) && tabRun.Page == 20002 && tabRun.Moved == 15,
    "All-tabs skips inaccessible tabs and accumulates confirmed moves");
Check(tabRun.Observe(42, 20002, 400) && tabRun.Next(20002, 500) && tabRun.Page == 20004,
    "An empty/full tab can advance without adding moves or revisiting duplicate tabs");
Check(tabRun.Observe(42, 20004, 600) && !tabRun.Next(20004, 700, 5) && tabRun.Moved == 20 && !tabRun.Running,
    "All-tabs finishes once and retains total");
tabRun.Start(42, [20000]); tabRun.Next(20002, 0);
Check(!tabRun.Observe(42, 20003, 100) && !tabRun.Running, "Unexpected tab selection cancels all-tabs");
tabRun.Start(42, [20000]); tabRun.Next(20002, 0); tabRun.Observe(42, 20000, 100);
Check(!tabRun.Observe(42, 20002, 200), "Returning to old tab after acknowledgment also cancels");
tabRun.Start(42, [20000]); tabRun.Next(20002, 0);
Check(!tabRun.Observe(42, 20002, 5000), "A tab switch that never completes times out");
tabRun.Start(42, [20000]); tabRun.Next(20000, 0);
Check(!tabRun.Observe(43, 20000, 100), "Character change cancels all-tabs");
tabRun.Start(42, [20000]); tabRun.Next(20000, 0);
Check(!tabRun.Observe(42, null, 100), "Closed chest or non-item view cancels all-tabs");
tabRun.Start(42, [20000, 20001]); tabRun.Next(20000, 0); tabRun.Stop();
Check(!tabRun.Next(20000, 100) && !tabRun.Observe(42, 20000, 200), "Stop or disabling prevents remaining tab work");
tabRun.Start(42, []);
Check(!tabRun.Running && !tabRun.Next(20000, 0), "No accessible tabs does not start a run");
var completionQueue = new DuplicateTransfer("FC chest");
completionQueue.Start(new TransferView([], [])); completionQueue.Advance(new TransferView([], []), 0);
Check(completionQueue.Completed && !completionQueue.Running, "No duplicates is a successful tab completion");
completionQueue.Start(transferView);
Check(!completionQueue.Completed && completionQueue.Advance(transferView, 0) == sourceStack,
    "New tab clears completion and reads its own candidates");
completionQueue.SkipUnsent();
Check(completionQueue.Advance(transferView, 300) == secondStack, "A stack without room is skipped without losing later candidates");
completionQueue.Advance(transferView, 5300);
Check(!completionQueue.Completed && !completionQueue.Running, "An unconfirmed move is not successful completion");
completionQueue.Stop("Stopped");
Check(!completionQueue.Completed, "Manual stop cannot advance to the next tab");
var tabSettling = new StableObservation<TransferSlot[]>();
Check(!tabSettling.Accept("20000", [bagStack], 0, (a,b) => a.SequenceEqual(b)) &&
    !tabSettling.Accept("20000", [bagStack with { Quantity = 1 }], 900, (a,b) => a.SequenceEqual(b)) &&
    !tabSettling.Accept("20000", [bagStack with { Quantity = 1 }], 1000, (a,b) => a.SequenceEqual(b)) &&
    tabSettling.Accept("20000", [bagStack with { Quantity = 1 }], 1900, (a,b) => a.SequenceEqual(b)),
    "Changed chest contents restart the settling delay");
for (var permissionTab = 0; permissionTab < 5; permissionTab++)
{
    foreach (var access in new[] { 1, 2, 4 })
    {
        var permissionBytes = new byte[10];
        var packedAccess = access << (7 + permissionTab * 3);
        permissionBytes[1] = (byte)packedAccess;
        permissionBytes[2] = (byte)(packedAccess >> 8);
        permissionBytes[3] = (byte)(packedAccess >> 16);
        Check(ChestPermissions.CanDeposit(permissionBytes, (uint)(20000 + permissionTab)) == (access == 4),
            $"Tab {permissionTab + 1} distinguishes no access/view-only/full access");
        if (permissionTab < 4) permissionBytes[4] |= (byte)(1 << (4 + permissionTab));
        else permissionBytes[5] |= 1;
        Check(ChestPermissions.CanDeposit(permissionBytes, (uint)(20000 + permissionTab)) == true,
            $"Tab {permissionTab + 1} accepts the separate deposit-only flag");
    }
}
Check(ChestPermissions.CanDeposit(new byte[10], 20000) is null &&
    ChestPermissions.CanDeposit([], 20000) is null && ChestPermissions.CanDeposit(new byte[10], 22001) is null,
    "Missing permissions and non-item pages are unknown, never assumed writable");
var conflictingPermissions = new byte[10]; conflictingPermissions[2] = 3;
Check(ChestPermissions.CanDeposit(conflictingPermissions, 20000) is null,
    "Contradictory access bits are rejected");
var roomQueue = new DuplicateTransfer("FC chest");
var tooLarge = BagSlot(0, 0, 700, 90);
var fitsLater = BagSlot(0, 1, 701, 5);
var roomView = new TransferView([tooLarge, fitsLater], [BagSlot(20000, 0, 700, 50), BagSlot(20000, 1, 701, 50)]);
roomQueue.Start(roomView);
var oversized = roomQueue.Advance(roomView, 0);
Check(oversized == tooLarge && ChestDeposit.Destination(oversized, roomView.Destination, 20000) is null,
    "First duplicate can fail whole-stack capacity");
roomQueue.SkipUnsent();
var fitting = roomQueue.Advance(roomView, 300);
Check(fitting == fitsLater && ChestDeposit.Destination(fitting, roomView.Destination, 20000) is not null,
    "Current-tab queue continues to a fitting later duplicate");
var movedView = roomView with { Inventory = [tooLarge, fitsLater with { Key = default, Quantity = 0 }] };
roomQueue.Advance(movedView, 600); roomQueue.Advance(movedView, 900);
Check(roomQueue.Completed && roomQueue.Moved == 5, "Only confirmed fitting stack contributes to completion");
tabRun.Start(42, [20000, 20001, 20002]); tabRun.Next(20000, 0); tabRun.Observe(42, 20000, 1);
tabRun.Next(20000, 2); tabRun.Observe(42, 20001, 3);
Check(tabRun.Next(20001, 4) && tabRun.Page == 20002 && tabRun.Moved == 0,
    "Skipping a read-only tab advances to later tabs without counting a move");
Console.WriteLine($"Passed {checks} lifecycle, ownership, glamour, crafting, transfer and action checks.");

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

