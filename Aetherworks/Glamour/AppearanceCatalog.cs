namespace Aetherworks.Glamour;

public enum AppearanceMatch { None, Close, Exact }

// Raw model fields preserve variants and secondary components; applied stains are intentionally absent.
public sealed record Appearance(uint ItemId, string Name, uint Slot, uint WeaponKind,
    ulong ModelMain, ulong ModelSub, byte DyeChannels, bool ArmoireEligible = false);

public sealed class AppearanceCatalog
{
    private readonly Dictionary<uint, Appearance> items;
    private readonly Dictionary<(uint Slot, uint WeaponKind, ushort BaseModel), Appearance[]> groups;

    public AppearanceCatalog(IEnumerable<Appearance> entries)
    {
        items = entries.Where(a => a.Slot != 0 && (ushort)a.ModelMain != 0).ToDictionary(a => a.ItemId);
        groups = items.Values.GroupBy(Key).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Name, StringComparer.Ordinal).ThenBy(a => a.ItemId).ToArray());
    }

    private static (uint, uint, ushort) Key(Appearance item) => (item.Slot, item.WeaponKind, (ushort)item.ModelMain);
    public Appearance? Get(uint id) => items.GetValueOrDefault(id);
    public IReadOnlyList<Appearance> Candidates(uint id) => items.TryGetValue(id, out var item) ? groups[Key(item)] : [];

    public static AppearanceMatch Compare(Appearance a, Appearance b)
    {
        if (a.Slot == 0 || (ushort)a.ModelMain == 0 || Key(a) != Key(b)) return AppearanceMatch.None;
        return a.ModelMain == b.ModelMain && a.ModelSub == b.ModelSub && a.DyeChannels == b.DyeChannels
            ? AppearanceMatch.Exact : AppearanceMatch.Close;
    }

    public static string Difference(Appearance target, Appearance owned)
    {
        var differences = new List<string>();
        if (owned.DyeChannels != target.DyeChannels)
            differences.Add($"{owned.DyeChannels} dye {(owned.DyeChannels == 1 ? "channel" : "channels")} instead of {target.DyeChannels}");
        if (owned.ModelMain != target.ModelMain) differences.Add("Different variant");
        if (owned.ModelSub != target.ModelSub) differences.Add("Different secondary model");
        return string.Join("; ", differences);
    }
}
