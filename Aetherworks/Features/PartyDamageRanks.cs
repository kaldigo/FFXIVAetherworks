namespace Aetherworks.Features;

internal readonly record struct DamageEntry(long Total, string? Percent);

internal static class PartyDamageRanks
{
    public static void Calculate(string[] roster, int firstNpc, string? player,
        IReadOnlyDictionary<string, DamageEntry> damage, string?[] labels, int[] ranks)
    {
        Array.Clear(labels);
        Array.Clear(ranks);
        var matched = new List<(int Row, DamageEntry Damage)>();
        for (int row = 0; row < roster.Length; row++)
        {
            string name = roster[row];
            if (string.IsNullOrEmpty(name) || roster.Count(n => Same(n, name)) != 1) continue;
            if (!damage.TryGetValue(name, out var total) &&
                !(Same(name, player) && damage.TryGetValue("YOU", out total)))
            {
                var aliases = damage.Where(entry => Same(RowName(entry.Key, row >= firstNpc), name)).Take(2).ToArray();
                if (aliases.Length != 1) continue;
                total = aliases[0].Value;
            }
            matched.Add((row, total));
        }

        if (damage.Count == 0) return;
        // During an encounter every occupied row gets a label. Missing or ambiguous
        // parser entries have no known rank or share yet.
        for (int row = 0; row < roster.Length; row++)
            if (!string.IsNullOrWhiteSpace(roster[row])) labels[row] = "  -% ";
        foreach (var member in matched)
        {
            int rank = 1 + matched.Count(other => other.Damage.Total > member.Damage.Total);
            string percent = member.Damage.Percent ?? "-%";
            // Use ordinary HUD spaces for blank digit positions.
            ranks[member.Row] = rank;
            string percentSpace = percent.Length == 2 ? "  " : "";
            labels[member.Row] = $"{percentSpace}{percent} ";
        }
    }

    private static bool Same(string? first, string? second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static string RowName(string key, bool npc)
    {
        if (npc && key.EndsWith(')') && key.LastIndexOf(" (", StringComparison.Ordinal) is var owner && owner > 0)
            key = key[..owner];
        int world = key.IndexOf('@');
        return world > 0 ? key[..world] : key;
    }
}
