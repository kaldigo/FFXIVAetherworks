using Aetherworks.Ownership;

namespace Aetherworks.Glamour;

public static class OwnershipTooltip
{
    public static IReadOnlyList<string> Compose(uint id, OwnershipStore store, AppearanceCatalog catalog,
        IEnumerable<(string Id, string Label)> itemLocations, bool showItems, bool showGlamour)
    {
        var lines = new List<string>();
        var locations = itemLocations.ToList();
        var target = showGlamour ? catalog.Get(id) : null;
        if (target is not null)
        {
            // Armoire eligibility of an alternative also matters, even if the hovered item cannot be stored there.
            if (!locations.Any(l => l.Id == "dresser")) locations.Add(("dresser", "Glamour Dresser"));
            if (catalog.Candidates(id).Any(a => a.ArmoireEligible) && !locations.Any(l => l.Id == "armoire"))
                locations.Add(("armoire", "Armoire"));
            var matches = (from candidate in catalog.Candidates(id)
                           from location in locations
                           where store.Snapshot.Locations.TryGetValue(location.Id, out var inventory) && inventory.ContainsKey(candidate.ItemId)
                           select (Item: candidate, Location: location, Match: AppearanceCatalog.Compare(target, candidate))).ToList();
            var collected = matches.Where(m => IsCollected(m.Location.Id) && m.Match == AppearanceMatch.Exact).ToList();
            if (collected.Count > 0)
                lines.Add("Glamour: Exact match — " + string.Join(" · ", locations
                    .Where(l => collected.Any(m => m.Location.Id == l.Id)).Select(l => l.Label).Distinct()));
            foreach (var match in matches.Where(m => !IsCollected(m.Location.Id) && m.Match == AppearanceMatch.Exact))
            {
                // The item-location feature already reports the hovered item itself.
                if (showItems && match.Item.ItemId == id) continue;
                lines.Add($"Appearance owned: {match.Item.Name} — {match.Location.Label}");
            }
            if (collected.Count == 0)
                foreach (var match in matches.Where(m => m.Match == AppearanceMatch.Close))
                    lines.Add($"Close match: {match.Item.Name} — {match.Location.Label} · {AppearanceCatalog.Difference(target, match.Item)}");
        }

        foreach (var location in locations)
        {
            if (!showItems && target is null) break;
            if (!store.Snapshot.Locations.TryGetValue(location.Id, out var inventory))
            {
                lines.Add($"{location.Label}: Missing");
                continue;
            }
            if (showItems && (target is null || !IsCollected(location.Id)) && inventory.TryGetValue(id, out var quantity))
                lines.Add($"{location.Label}: {quantity}");
        }
        return lines;
    }

    private static bool IsCollected(string location) => location is "dresser" or "armoire";
}
