namespace Aetherworks.Ownership;

/// <summary>Rejects a first/transitional read. Readiness and identity must be checked by the collector.</summary>
public sealed class StableObservation<T>
{
    private string? context;
    private T? candidate;
    private long since;
    public void Reset() { context = null; candidate = default; }

    public bool Accept(string identity, T value, long now, Func<T, T, bool> equal)
    {
        if (context != identity || candidate is null || !equal(candidate, value))
        {
            context = identity;
            candidate = value;
            since = now;
            return false;
        }
        return now - since >= 1000;
    }
}

public static class DresserContents
{
    // The callback uses the physical dresser slot and original outfit column, including empty columns.
    public static List<(uint ItemId, long Quantity)> Expand(IReadOnlyList<uint> slots,
        IReadOnlyDictionary<uint, uint[]> outfits, Func<int, int, bool> unlocked)
    {
        var items = new List<(uint, long)>();
        for (var slot = 0; slot < slots.Count; slot++)
        {
            var id = slots[slot];
            if (id == 0) continue;
            if (!outfits.TryGetValue(id, out var pieces)) { items.Add((id, 1)); continue; }
            for (var part = 0; part < pieces.Length; part++)
                if (pieces[part] != 0 && unlocked(slot, part)) items.Add((pieces[part], 1));
        }
        return items;
    }
}
