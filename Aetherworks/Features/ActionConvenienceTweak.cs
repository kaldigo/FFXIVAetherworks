using Aetherworks.Actions;
using Aetherworks.Tweaks;

namespace Aetherworks.Features;

public sealed class ActionConvenienceTweak(ActionConveniences runtime, ActionConvenience feature) : ITweak
{
    public string Id => feature switch
    {
        ActionConvenience.Dismount => "auto-dismount", ActionConvenience.CastCancel => "auto-cast-cancel",
        ActionConvenience.Target => "auto-target", ActionConvenience.ChangeTarget => "auto-change-target",
        _ => throw new ArgumentOutOfRangeException(nameof(feature)),
    };
    public string Name => feature switch
    {
        ActionConvenience.Dismount => "Auto Dismount", ActionConvenience.CastCancel => "Auto Cast Cancel",
        ActionConvenience.Target => "Auto Target", _ => "Auto Change Target",
    };
    public string Description => feature switch
    {
        ActionConvenience.Dismount => "Dismount for eligible actions and retry within two seconds. Excludes mount actions, Teleport and Return; includes Sprint and Limit Break.",
        ActionConvenience.CastCancel => "Cancel eligible casts when their target can no longer receive the spell. Ground-targeted casts are excluded.",
        ActionConvenience.Target => "For hostile actions without a usable target, select the nearest living attackable target in the camera-facing cone within 30 yalms.",
        _ => "Allow Auto Target to replace an unsuitable selected target. Requires Auto Target to be enabled; explicit alternative targets are preserved.",
    };
    public string Category => "Actions";
    public void Enable() => runtime.Set(feature, true);
    public void Disable() => runtime.Set(feature, false);
}
