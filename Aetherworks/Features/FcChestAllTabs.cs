using Aetherworks.Tweaks;

namespace Aetherworks.Features;

public sealed class FcChestAllTabs(FcChestEntrustDuplicates chest) : ITweak
{
    public string Id => "fc-chest-all-tabs";
    public string Name => "Show FC Chest All-Tabs Deposit Button";
    public string Description => "Add Entrust Duplicates — All Tabs alongside the current-tab button. Requires FC Chest Entrust Duplicates.";
    public string Category => "Items and storage";
    public void Enable() => chest.ShowAllTabs = true;
    public void Disable() => chest.ShowAllTabs = false;
}
