using Aetherworks.Ownership;
using Aetherworks.Tweaks;

namespace Aetherworks.Features;

public sealed class IncludeFcChestOwnership(OwnershipTracker tracker) : ITweak
{
    public string Id => "include-fc-chest-ownership";
    public string Name => "Include FC Chest";
    public string Category => "Items and storage";
    public string Description => "Include remembered FC chest item tabs in ownership tooltips and recipe material totals. Open each tab to record it. Snapshots keep updating with this option off; shared contents may change while the chest is closed.";
    public void Enable() => tracker.IncludeFcChest = true;
    public void Disable() => tracker.IncludeFcChest = false;
}
