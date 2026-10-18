using System.Text.Json;

namespace Aetherworks.Ownership;

public sealed class OwnershipSnapshot
{
    public int Version { get; set; } = 1;
    public Dictionary<string, Dictionary<uint, long>> Locations { get; set; } = new();
    public Dictionary<string, string> Retainers { get; set; } = new();
    public bool RetainersKnown { get; set; }
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
        if (snapshot.Version != 1 || snapshot.Locations is null || snapshot.Retainers is null ||
            snapshot.Retainers.Any(r => !r.Key.StartsWith("retainer:", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(r.Value)) ||
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
            if (!Snapshot.Locations.TryGetValue(id, out var items)) results.Add(new(label, null));
            else if (items.TryGetValue(itemId, out var quantity)) results.Add(new(label, quantity));
        }
        return results;
    }

    public string Serialize() => JsonSerializer.Serialize(Snapshot);
    public void MarkSaved() => Dirty = false;

    public void ReplaceRetainers(IReadOnlyDictionary<string, string> roster)
    {
        if (Snapshot.RetainersKnown && Snapshot.Retainers.Count == roster.Count &&
            Snapshot.Retainers.All(r => roster.TryGetValue(r.Key, out var name) && name == r.Value)) return;
        // An authoritative roster removes dismissed retainers, without conflating renamed retainers.
        foreach (var id in Snapshot.Retainers.Keys.Except(roster.Keys).ToArray()) Snapshot.Locations.Remove(id);
        Snapshot.Retainers = new(roster);
        Snapshot.RetainersKnown = true;
        Revision++;
        Dirty = true;
    }

    public IEnumerable<(string Id, string Label)> RetainerLocations()
        => Snapshot.RetainersKnown
            ? Snapshot.Retainers.OrderBy(r => r.Value, StringComparer.Ordinal).Select(r => (r.Key, "Retainer " + r.Value))
            : [("retainers-unobserved", "Retainers")];

    public static string RetainerKey(ulong id) => "retainer:" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string FcChestKey(ulong companyId, int tab)
    {
        if (companyId == 0 || tab is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(tab));
        return $"fc-chest:{companyId.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{tab}";
    }

    public IEnumerable<(string Id, string Label)> ItemStorageLocations(bool includeFcChest = false, ulong companyId = 0)
    {
        yield return ("inventory", "Inventory");
        yield return ("saddlebag", "Saddlebag");
        foreach (var location in RetainerLocations()) yield return location;
        if (!includeFcChest) yield break;
        if (companyId == 0) { yield return ("fc-chest-unobserved", "FC Chest"); yield break; }
        for (var tab = 1; tab <= 5; tab++) yield return (FcChestKey(companyId, tab), $"FC Chest — Tab {tab}");
    }
}
