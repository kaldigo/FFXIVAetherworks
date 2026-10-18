namespace Aetherworks.Transfers;

/// <summary>Tracks only tab order and transitions; inventory is always read live.</summary>
public sealed class ChestTabRun
{
    private readonly Queue<uint> pages = new();
    private uint previous;
    private long requestedAt;
    public bool Running { get; private set; }
    public bool Waiting { get; private set; }
    public uint Page { get; private set; }
    public ulong Character { get; private set; }
    public long Moved { get; private set; }

    public void Start(ulong character, IEnumerable<uint> accessiblePages)
    {
        pages.Clear();
        foreach (var page in accessiblePages.Where(p => p is >= 20000 and <= 20004).Distinct().Order()) pages.Enqueue(page);
        Character = character;
        Moved = 0;
        Running = pages.Count > 0;
        Waiting = false;
    }

    public bool Next(uint current, long now, long moved = 0)
    {
        Moved += moved;
        if (!Running || !pages.TryDequeue(out var page)) { Stop(); return false; }
        Page = page;
        previous = current;
        requestedAt = now;
        Waiting = true;
        return true;
    }

    public bool Observe(ulong character, uint? current, long now)
    {
        if (!Running) return false;
        if (character != Character || current is null ||
            (current != Page && (!Waiting || current != previous)) ||
            (Waiting && now - requestedAt >= 5000)) { Stop(); return false; }
        if (current == Page) Waiting = false;
        return true;
    }

    public void Stop() { Running = false; Waiting = false; pages.Clear(); }
}
