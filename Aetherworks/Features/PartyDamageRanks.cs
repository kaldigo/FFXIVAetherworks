namespace Aetherworks.Features;

internal static class PartyDamageRanks
{
    public static void Calculate(string[] roster, int firstNpc, string? player,
        IReadOnlyDictionary<string, long> damage, string?[] labels)
    {
        Array.Clear(labels);
        var matched = new List<(int Row, long Damage)>();
        for (int row = 0; row < roster.Length; row++)
        {
            string name = roster[row];
            if (string.IsNullOrEmpty(name) || roster.Count(n => Same(n, name)) != 1) continue;
            if (!damage.TryGetValue(name, out long total) &&
                !(Same(name, player) && damage.TryGetValue("YOU", out total)))
            {
                var aliases = damage.Where(entry => Same(RowName(entry.Key, row >= firstNpc), name)).Take(2).ToArray();
                if (aliases.Length != 1) continue;
                total = aliases[0].Value;
            }
            matched.Add((row, total));
        }

        decimal sum = matched.Sum(member => (decimal)member.Damage);
        if (sum == 0) return;
        bool padRank = roster.Count(name => !string.IsNullOrEmpty(name)) > 10;
        foreach (var member in matched)
        {
            int rank = 1 + matched.Count(other => other.Damage > member.Damage);
            int percent = (int)Math.Round(100m * member.Damage / sum, MidpointRounding.AwayFromZero);
            // Use ordinary HUD spaces for blank digit positions.
            string rankSpace = padRank && rank < 10 ? "  " : "";
            string percentSpace = percent < 10 ? "  " : "";
            labels[member.Row] = $"{rankSpace}{rank}:{percentSpace}{percent}% ";
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
