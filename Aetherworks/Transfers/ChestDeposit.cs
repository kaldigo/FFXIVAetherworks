namespace Aetherworks.Transfers;

public static class ChestDeposit
{
    // Pandora's chest quick-deposit policy: use a compatible stack that fits the entire
    // source, otherwise an empty slot. Avoid quantity dialogs and automated stack splitting.
    public static TransferSlot? Destination(TransferSlot source, IEnumerable<TransferSlot> slots, uint page)
    {
        if (!source.Eligible || page is < 20000 or > 20004) return null;
        var currentTab = slots.Where(s => s.Container == page).ToArray();
        return currentTab.FirstOrDefault(s => s.Eligible && s.Key == source.Key &&
                   (long)s.Quantity + source.Quantity <= s.StackSize)
            ?? currentTab.FirstOrDefault(s => s.Key.ItemId == 0 && s.Quantity == 0);
    }
}
