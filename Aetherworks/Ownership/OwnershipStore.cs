using System.Text.Json;

namespace Aetherworks.Ownership;

[Flags]
public enum RetainerParts { None = 0, Bags = 1, Crystals = 2, All = Bags | Crystals }

public sealed class OwnershipSnapshot
{
    public int Version { get; set; } = 1;
    public Dictionary<string, Dictionary<uint, long>> Locations { get; set; } = new();
    public Dictionary<string, string> Retainers { get; set; } = new();
    public bool RetainersKnown { get; set; }
    public Dictionary<string, RetainerParts> RetainerCoverage { get; set; } = new();
    public Dictionary<ulong, int[]> FcChestTabs { get; set; } = new();
}

public sealed record LocationResult(string Label, long? Quantity);

/// <summary>A snapshot's absence is distinct from an observed empty container.</summary>
public sealed class OwnershipStore
{
    public OwnershipSnapshot Snapshot { get; private set; } = new();
    public bool Dirty { get; private set; }
    public long Revision { get; private set; }

    public void Load(string json)
    {
        var snapshot = JsonSerializer.Deserialize<OwnershipSnapshot>(json)
            ?? throw new InvalidDataException("Empty ownership snapshot.");
        if (snapshot.Version != 1 || snapshot.Locations is null || snapshot.Retainers is null || snapshot.RetainerCoverage is null ||
            snapshot.FcChestTabs is null || snapshot.FcChestTabs.Any(c => c.Key == 0 || c.Value is null ||
                c.Value.Length == 0 || c.Value.Any(t => t is < 1 or > 5) || c.Value.Distinct().Count() != c.Value.Length) ||
            snapshot.Retainers.Any(r => !r.Key.StartsWith("retainer:", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(r.Value)) ||
            snapshot.RetainerCoverage.Any(r => !r.Key.StartsWith("retainer:", StringComparison.Ordinal) ||
                !snapshot.Locations.ContainsKey(r.Key) || r.Value is RetainerParts.None || (r.Value & ~RetainerParts.All) != 0) ||
            snapshot.Locations.Any(l => string.IsNullOrWhiteSpace(l.Key) || l.Value is null ||
                l.Value.Any(i => i.Key == 0 || i.Value <= 0)))
            throw new InvalidDataException("Invalid or unsupported ownership snapshot.");
        Snapshot = snapshot;
        Revision++;
        Dirty = false;
    }

    public void Replace(string location, IEnumerable<(uint ItemId, long Quantity)> items)
    {
        var counts = new Dictionary<uint, long>();
        foreach (var (id, quantity) in items)
        {
            if (id == 0 || quantity <= 0) continue;
            counts[id] = checked(counts.GetValueOrDefault(id) + quantity);
        }
        if (Snapshot.Locations.TryGetValue(location, out var previous) &&
            previous.Count == counts.Count && previous.All(i => counts.GetValueOrDefault(i.Key) == i.Value)) return;
        Snapshot.Locations[location] = counts;
        Revision++;
        Dirty = true;
    }

    public IReadOnlyList<LocationResult> Find(uint itemId, IEnumerable<(string Id, string Label)> locations)
    {
        var results = new List<LocationResult>();
        foreach (var (id, label) in locations)
        {
            if (!TryGetQuantity(id, itemId, out var quantity)) results.Add(new(label, null));
            else if (quantity > 0) results.Add(new(label, quantity));
        }
        return results;
    }

    // The game's eighteen shard/crystal/cluster base item IDs are 2–19.
    public static bool IsCrystal(uint itemId) => itemId is >= 2 and <= 19;

    public bool TryGetQuantity(string location, uint itemId, out long quantity)
    {
        quantity = 0;
        if (!Snapshot.Locations.TryGetValue(location, out var items)) return false;
        if (location.StartsWith("retainer:", StringComparison.Ordinal))
        {
            // Older snapshots were committed only after both parts loaded.
            var coverage = Snapshot.RetainerCoverage.GetValueOrDefault(location, RetainerParts.All);
            if ((coverage & (IsCrystal(itemId) ? RetainerParts.Crystals : RetainerParts.Bags)) == 0) return false;
        }
        quantity = items.GetValueOrDefault(itemId);
        return true;
    }

    public void ReplaceRetainerPart(string key, RetainerParts part, IEnumerable<(uint ItemId, long Quantity)> items)
    {
        if (!Snapshot.Retainers.ContainsKey(key) || part is not (RetainerParts.Bags or RetainerParts.Crystals))
            throw new ArgumentException("A named retainer and one storage part are required.");
        var incoming = items.ToArray();
        bool crystals = part == RetainerParts.Crystals;
        if (incoming.Any(i => i.ItemId == 0 || i.Quantity <= 0 || IsCrystal(i.ItemId) != crystals))
            throw new ArgumentException("Retainer contents do not match the captured storage part.");
        bool existed = Snapshot.Locations.TryGetValue(key, out var previous);
        var coverage = existed ? Snapshot.RetainerCoverage.GetValueOrDefault(key, RetainerParts.All) : RetainerParts.None;
        var retained = previous?.Where(i => IsCrystal(i.Key) != crystals).Select(i => (i.Key, i.Value)) ?? [];
        Replace(key, retained.Concat(incoming));
        if ((coverage | part) != coverage)
        {
            Revision++;
            Dirty = true;
        }
        Snapshot.RetainerCoverage[key] = coverage | part;
    }

    public string Serialize() => JsonSerializer.Serialize(Snapshot);
    public void MarkSaved() => Dirty = false;

    public void ObserveRetainer(ulong id, string name)
    {
        if (id == 0 || string.IsNullOrWhiteSpace(name)) return;
        var key = RetainerKey(id);
        if (Snapshot.Retainers.GetValueOrDefault(key) == name) return;
        Snapshot.Retainers[key] = name;
        Revision++;
        Dirty = true;
    }

    public void ReplaceRetainers(IReadOnlyDictionary<string, string> roster)
    {
        if (Snapshot.RetainersKnown && Snapshot.Retainers.Count == roster.Count &&
            Snapshot.Retainers.All(r => roster.TryGetValue(r.Key, out var name) && name == r.Value)) return;
        // An authoritative roster removes dismissed retainers, without conflating renamed retainers.
        foreach (var id in Snapshot.Retainers.Keys.Except(roster.Keys).ToArray())
        {
            Snapshot.Locations.Remove(id);
            Snapshot.RetainerCoverage.Remove(id);
        }
        Snapshot.Retainers = new(roster);
        Snapshot.RetainersKnown = true;
        Revision++;
        Dirty = true;
    }

    public IEnumerable<(string Id, string Label)> RetainerLocations()
    {
        foreach (var entry in Snapshot.Retainers.OrderBy(r => r.Value, StringComparer.Ordinal))
            yield return (entry.Key, "Retainer " + entry.Value);
        if (!Snapshot.RetainersKnown) yield return ("retainers-unobserved", "Retainers");
    }

    public static string RetainerKey(ulong id) => "retainer:" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string FcChestKey(ulong companyId, int tab)
    {
        if (companyId == 0 || tab is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(tab));
        return $"fc-chest:{companyId.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{tab}";
    }

    public void ReplaceFcChestTabs(ulong companyId, IEnumerable<int> tabs)
    {
        var available = tabs.Distinct().Order().ToArray();
        if (companyId == 0 || available.Length == 0 || available.Any(t => t is < 1 or > 5))
            throw new ArgumentException("A known FC chest must have valid available item tabs.");
        if (Snapshot.FcChestTabs.TryGetValue(companyId, out var old) && old.SequenceEqual(available)) return;
        Snapshot.FcChestTabs[companyId] = available;
        // Keep contents for temporarily unavailable tabs, but exclude them from results.
        Revision++;
        Dirty = true;
    }

    public IEnumerable<(string Id, string Label)> ItemStorageLocations(bool includeFcChest = false, ulong companyId = 0)
    {
        yield return ("inventory", "Inventory");
        yield return ("saddlebag", "Saddlebag");
        foreach (var location in RetainerLocations()) yield return location;
        if (!includeFcChest) yield break;
        if (companyId == 0) { yield return ("fc-chest-unobserved", "FC Chest"); yield break; }
        if (!Snapshot.FcChestTabs.TryGetValue(companyId, out var tabs))
        { yield return ($"fc-chest-tabs-unobserved:{companyId}", "FC Chest"); yield break; }
        foreach (var tab in tabs.Order()) yield return (FcChestKey(companyId, tab), $"FC Chest — Tab {tab}");
    }
}
