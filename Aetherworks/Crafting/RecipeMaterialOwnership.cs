using Aetherworks.Ownership;

namespace Aetherworks.Crafting;

public sealed record RecipeMaterial(uint ItemId, string Name);
public sealed record MaterialOwnership(string Name, long OutsideQuantity, long Total,
    bool HasMissingData, IReadOnlyList<LocationResult> Locations)
{
    public string Indicator => OutsideQuantity > 0 ? $"{OutsideQuantity:N0} elsewhere" : "Missing";
    public string TotalLabel => HasMissingData ? "Total known" : "Total owned";
}

public static class RecipeMaterialOwnership
{
    public static IReadOnlyList<MaterialOwnership> Find(IEnumerable<RecipeMaterial> ingredients, OwnershipStore store,
        bool includeFcChest = false, ulong companyId = 0)
    {
        // Dresser/armoire appearances cannot be consumed as recipe materials.
        var locations = store.ItemStorageLocations(includeFcChest, companyId).ToArray();
        var result = new List<MaterialOwnership>();
        foreach (var ingredient in ingredients.Where(i => i.ItemId != 0).DistinctBy(i => i.ItemId))
        {
            long outside = 0, total = 0;
            var missing = false;
            var rows = new List<LocationResult>();
            foreach (var (id, label) in locations)
            {
                if (!store.TryGetQuantity(id, ingredient.ItemId, out var quantity))
                {
                    missing = true;
                    rows.Add(new(label, null));
                    continue;
                }
                total = checked(total + quantity);
                if (id != "inventory") outside = checked(outside + quantity);
                if (quantity > 0) rows.Add(new(label, quantity));
            }
            // There is deliberately no comparison with the quantity needed for one craft.
            if (outside > 0 || missing)
                result.Add(new(ingredient.Name, outside, total, missing, rows));
        }
        return result;
    }
}
