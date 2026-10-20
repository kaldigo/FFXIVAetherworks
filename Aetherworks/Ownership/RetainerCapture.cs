namespace Aetherworks.Ownership;

/// <summary>Settles identity and each storage part independently, including confirmed empty bags.</summary>
public sealed class RetainerCapture
{
    private readonly StableObservation<List<(uint ItemId, long Quantity)>> bags = new();
    private readonly StableObservation<List<(uint ItemId, long Quantity)>> crystals = new();
    public string Status { get; private set; } = "Open a retainer inventory to refresh its contents.";

    public void Reset(string status = "Open a retainer inventory to refresh its contents.")
    {
        bags.Reset();
        crystals.Reset();
        Status = status;
    }

    public void Observe(OwnershipStore store, ulong selected, ulong inventoryOwner, bool visible,
        int itemCount, int loadedBagCapacity, List<(uint ItemId, long Quantity)> observedBags,
        bool slotsReadable, List<(uint ItemId, long Quantity)>? observedCrystals, long now)
    {
        if (!visible || selected == 0 || inventoryOwner != selected)
        {
            Reset(visible ? "Waiting for the inventory to match the selected retainer." : "Open a retainer inventory to refresh its contents.");
            return;
        }
        var key = OwnershipStore.RetainerKey(selected);
        if (!store.Snapshot.Retainers.ContainsKey(key)) { Reset("Waiting for the selected retainer's name."); return; }

        // ItemCount is occupied bag slots, not quantities or crystal stacks. A settled zero
        // can prove empty bags even if unused containers have not been supplied by the game.
        // Contradictory non-empty reads must never be interpreted as empty.
        bool empty = itemCount == 0 && observedBags.Count == 0;
        bool ready = slotsReadable && itemCount is >= 0 and <= 175 && loadedBagCapacity is >= 0 and <= 175 &&
            observedBags.Count == itemCount && (empty || loadedBagCapacity >= itemCount);
        bool bagSettled = false, crystalSettled = false;
        if (!ready) bags.Reset();
        else if (bags.Accept(key, observedBags, now, (a,b)=>a.SequenceEqual(b)))
        {
            store.ReplaceRetainerPart(key, RetainerParts.Bags, observedBags);
            bagSettled = true;
        }
        if (observedCrystals is null) crystals.Reset();
        else if (crystals.Accept(key, observedCrystals, now, (a,b)=>a.SequenceEqual(b)))
        {
            store.ReplaceRetainerPart(key, RetainerParts.Crystals, observedCrystals);
            crystalSettled = true;
        }
        Status = $"Bags: {(bagSettled ? empty ? "recorded empty" : "recorded" : ready ? "settling" : $"waiting for contents ({observedBags.Count}/{itemCount} occupied slots)")}; " +
                 $"crystals: {(crystalSettled ? "recorded" : observedCrystals is null ? "waiting for loaded crystal storage" : "settling")}.";
    }
}
