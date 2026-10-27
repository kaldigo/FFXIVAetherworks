namespace Aetherworks.Transfers;

// Capture a value snapshot, never a pointer to a slot that the game can replace.
public sealed record LeaveOneWithdrawal(ulong Character, uint Container, int Slot, uint ItemId, byte Flags, int Quantity)
{
    public int Amount => Quantity > 1 ? Quantity - 1 : 0;

    public bool Matches(ulong character, uint itemId, byte flags, int quantity) =>
        Character != 0 && Character == character && ItemId != 0 && ItemId == itemId &&
        Flags == flags && Quantity == quantity && Amount > 0;
}
