namespace Aetherworks.Transfers;

public readonly record struct StackKey(uint ItemId, byte Flags);
public sealed record TransferSlot(uint Container, ushort Slot, StackKey Key, int Quantity, int StackSize)
{
    public bool Eligible => Key.ItemId != 0 && Key.Flags <= 1 && Quantity > 0 && StackSize > 1;
}
public sealed record TransferView(TransferSlot[] Inventory, TransferSlot[] Destination);

/// <summary>A queue of inventory slots for the game's Entrust action. The game chooses placement.</summary>
public sealed class DuplicateTransfer(string destinationName = "saddlebag")
{
    private readonly Queue<TransferSlot> queue = new();
    private TransferSlot? pending;
    private long sentAt;
    public bool Running { get; private set; }
    public bool Completed { get; private set; }
    public long Moved { get; private set; }
    public string Status { get; private set; } = "";

    public void Start(TransferView view)
    {
        if (Running) return;
        queue.Clear();
        Completed = false;
        var duplicates = view.Destination.Where(s => s.Eligible).Select(s => s.Key).ToHashSet();
        foreach (var slot in view.Inventory.Where(s => s.Eligible && duplicates.Contains(s.Key))) queue.Enqueue(slot);
        pending = null;
        Moved = 0;
        Running = true;
        Status = "Entrusting duplicates…";
    }

    public void Stop(string message)
    {
        Running = false;
        Completed = false;
        pending = null;
        queue.Clear();
        Status = message;
    }

    public TransferSlot? Advance(TransferView view, long now)
    {
        if (!Running) return null;
        if (pending is not null)
        {
            var current = Find(view, pending);
            if (current is null || (current.Key.ItemId != 0 && current.Key != pending.Key) || current.Quantity > pending.Quantity)
            {
                Stop("Stopped: the selected inventory slot changed.");
                return null;
            }
            if (current.Quantity < pending.Quantity)
            {
                Moved += pending.Quantity - current.Quantity;
                if (current.Eligible) queue.Enqueue(current);
                pending = null;
                Status = $"Entrusted {Moved:N0}; continuing…";
            }
            else if (now - sentAt >= 5000)
                Stop($"Entrusted {Moved:N0}. Stopped: no move confirmed; check {destinationName} space and access.");
            return null;
        }
        while (queue.TryDequeue(out var requested))
        {
            var current = Find(view, requested);
            // Do not act on a replacement item or a stack the player has changed.
            if (current != requested || !view.Destination.Any(s => s.Eligible && s.Key == current.Key)) continue;
            pending = current;
            sentAt = now;
            return current;
        }
        Stop(Moved > 0 ? $"Done: entrusted {Moved:N0} items." : "No stackable duplicates to move.");
        Completed = true;
        return null;
    }

    /// <summary>Discard the returned request only when no native move was sent.</summary>
    public void SkipUnsent() => pending = null;

    private static TransferSlot? Find(TransferView view, TransferSlot requested) =>
        view.Inventory.FirstOrDefault(s => s.Container == requested.Container && s.Slot == requested.Slot);
}

