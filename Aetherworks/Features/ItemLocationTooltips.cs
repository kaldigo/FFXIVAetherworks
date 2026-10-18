using Aetherworks.Tweaks;
using Aetherworks.Windows;

namespace Aetherworks.Features;

public sealed class ItemLocationTooltips(OwnershipTooltipPanel panel) : ITweak
{
    public string Id => "item-location-tooltips";
    public string Name => "Item Location Tooltips";
    public string Description => "Add item quantities in inventory, saddlebag, named retainers, glamour dresser and armoire to the game's item tooltip.";
    public string Category => "Items and storage";
    public void Enable() => panel.ShowItems = true;
    public void Disable() => panel.ShowItems = false;
}
