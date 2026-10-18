using Aetherworks.Tweaks;
using Aetherworks.Windows;

namespace Aetherworks.Features;

public sealed class GlamourOwnershipTooltips(OwnershipTooltipPanel panel) : ITweak
{
    public string Id => "glamour-ownership-tooltips";
    public string Name => "Glamour Ownership Tooltips";
    public string Description => "Show collected appearances and matching gear held elsewhere. Exact/close matches use model, variant and dye-channel count; applied dyes are ignored.";
    public string Category => "Items and storage";
    public void Enable() => panel.ShowGlamour = true;
    public void Disable() => panel.ShowGlamour = false;
}
