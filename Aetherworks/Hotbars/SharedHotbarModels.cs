using System.Text.Json.Serialization;

namespace Aetherworks.Hotbars;

public readonly record struct HotbarCommand(byte Type, uint Id)
{
    [JsonIgnore] public HotbarCommand Normalized => Type == 0 ? default : this;
    [JsonIgnore] public bool IsSprint => this is { Type: 1, Id: 3 or 29057 } or { Type: 10, Id: 4 };
    [JsonIgnore] public HotbarCommand Canonical => IsSprint ? new(1, 3) : Normalized;
    public HotbarCommand ForMode(bool pvp) => IsSprint ? new(1, pvp ? 29057u : 3u) : Normalized;
    public override string ToString() => Type == 0 ? "Empty" : $"Type {Type}, ID {Id}";
}

public sealed record HotbarContext(string Character, uint Job, bool PvP, uint Territory, uint SharedMask);
public sealed record SharedBar(HotbarCommand[] PvE, HotbarCommand[] PvP);
public sealed record SharedHotbarSnapshot(HotbarContext Context, Dictionary<int, SharedBar> Bars)
{
    public static int SlotCount(int bar) => bar is >= 0 and < 10 ? 12 : bar is >= 10 and < 18 ? 16
        : throw new InvalidDataException("Invalid hotbar number.");
    public static string BarName(int bar) => bar < 10 ? $"Hotbar {bar + 1}" : $"Cross hotbar {bar - 9}";
    public HotbarCommand Read(int bar, int slot, bool pvp) => (pvp ? Bars[bar].PvP : Bars[bar].PvE)[slot];
    public bool SameAs(SharedHotbarSnapshot other) => Context == other.Context && Bars.Count == other.Bars.Count &&
        Bars.All(pair => other.Bars.TryGetValue(pair.Key, out var bar) &&
            pair.Value.PvE.SequenceEqual(bar.PvE) && pair.Value.PvP.SequenceEqual(bar.PvP));
    public void Validate()
    {
        if (Context.SharedMask >> 18 != 0 || Context.Character.Length != 16 ||
            Context.Character.Any(c => !Uri.IsHexDigit(c)) || Context.Job == 0)
            throw new InvalidDataException("Invalid hotbar context.");
        if (Bars.Count != System.Numerics.BitOperations.PopCount(Context.SharedMask))
            throw new InvalidDataException("Shared hotbar capture is incomplete.");
        foreach (var (bar, values) in Bars)
        {
            var count = SlotCount(bar);
            if ((Context.SharedMask & (1u << bar)) == 0 || values.PvE.Length != count || values.PvP.Length != count)
                throw new InvalidDataException("Hotbar capture includes an unshared or incomplete bar.");
        }
    }
}

public sealed class SharedHotbarState
{
    [JsonRequired] public int Version { get; set; } = 1;
    [JsonRequired] public Dictionary<int, HotbarCommand[]> Baselines { get; set; } = [];
    public void Validate()
    {
        if (Version != 1 || Baselines is null || Baselines.Count > 18)
            throw new InvalidDataException("Shared hotbar state is unreadable or from another version.");
        foreach (var (bar, slots) in Baselines)
            if (slots is null || slots.Length != SharedHotbarSnapshot.SlotCount(bar))
                throw new InvalidDataException("Shared hotbar baseline is incomplete.");
    }
    public bool SameAs(SharedHotbarState other) => Baselines.Count == other.Baselines.Count &&
        Baselines.All(pair => other.Baselines.TryGetValue(pair.Key, out var values) && pair.Value.SequenceEqual(values));
}

public sealed record SharedSlotEdit(int Bar, int Slot, bool PvP, HotbarCommand Before, HotbarCommand After);
public sealed record SharedSlotConflict(int Bar, int Slot, HotbarCommand PvE, HotbarCommand PvP)
{
    public string Label => $"{SharedHotbarSnapshot.BarName(Bar)}, slot {Slot + 1}";
}
public sealed record SharedHotbarPlan(SharedHotbarState State, List<SharedSlotEdit> Edits,
    List<SharedSlotConflict> Conflicts, List<int> InitialBars);
public sealed record HotbarConflictResolution(HotbarContext Context, List<SharedSlotConflict> Conflicts, bool UsePvP);

public interface ISharedHotbarAccess
{
    SharedHotbarSnapshot? Capture(out string waitingReason);
    void Write(HotbarContext context, SharedSlotEdit edit);
    void Flush(HotbarContext context);
}

public interface ISharedHotbarStore
{
    SharedHotbarState Load(string character);
    void Save(string character, SharedHotbarState state);
    void Backup(SharedHotbarSnapshot before, SharedHotbarPlan plan);
}
