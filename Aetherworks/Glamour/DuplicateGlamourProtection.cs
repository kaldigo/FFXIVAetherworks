using Aetherworks.Ownership;

namespace Aetherworks.Glamour;

public sealed record GlamourConflict(uint ItemId, string Location);

public static class DuplicateGlamourProtection
{
    public static GlamourConflict? FindConflict(uint itemId, OwnershipStore store, AppearanceCatalog catalog)
    {
        if (itemId == 0) return null;
        var target = catalog.Get(itemId);
        foreach (var (id, label) in new[] { ("dresser", "Glamour Dresser"), ("armoire", "Armoire") })
        {
            if (!store.Snapshot.Locations.TryGetValue(id, out var items)) continue;
            // The identical item remains a duplicate even if its model data is unavailable.
            if (items.GetValueOrDefault(itemId) > 0) return new(itemId, label);
            if (target is null) continue;
            foreach (var candidate in catalog.Candidates(itemId))
                if (items.GetValueOrDefault(candidate.ItemId) > 0 &&
                    AppearanceCatalog.Compare(target, candidate) == AppearanceMatch.Exact)
                    return new(candidate.ItemId, label);
        }
        return null;
    }

    public static GlamourConflict? FindDepositConflict(IEnumerable<(uint ItemId, bool AlreadyStored)> pieces,
        OwnershipStore store, AppearanceCatalog catalog)
    {
        foreach (var piece in pieces)
        {
            if (piece.AlreadyStored) continue; // Consolidation does not introduce another appearance.
            var conflict = FindConflict(piece.ItemId, store, catalog);
            if (conflict is not null) return conflict;
        }
        return null;
    }
}
