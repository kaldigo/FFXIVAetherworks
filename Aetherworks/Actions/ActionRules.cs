namespace Aetherworks.Actions;

[Flags]
public enum ActionConvenience { Dismount = 1, CastCancel = 2, Target = 4, ChangeTarget = 8 }

public static class ActionRules
{
    public static bool DismountEligible(uint type, uint id, bool mountAction) =>
        type == 1 ? !mountAction && id != 5 && id != 6 : type == 5 && (id == 3 || id == 4);

    public static bool ShouldTarget(bool autoTarget, bool changeTarget, uint type, bool hasTarget,
        bool explicitOtherTarget, bool targetUsable, bool supportedHostileAction) =>
        autoTarget && type == 1 && (!hasTarget || changeTarget) && !explicitOtherTarget &&
        !targetUsable && supportedHostileAction;

    public static bool CancelEligible(uint type, bool supportedAction, bool groundTargeted) =>
        type == 1 && supportedAction && !groundTargeted;

    public static bool RetryDismountedAction(long elapsedMilliseconds) => elapsedMilliseconds is >= 0 and <= 2000;

    // ReAction uses a camera-facing 126-degree cone, inclusive edges, and distance < 30 yalms.
    public static bool InTargetCone(double cameraRotation, bool rotationOffset, double x, double z)
    {
        var facing = cameraRotation + Math.PI * 1.5 - (rotationOffset ? Math.PI : 0);
        var angle = Math.Atan2(-z, x) + Math.PI;
        var delta = Math.IEEERemainder(angle - facing, Math.PI * 2);
        return Math.Abs(delta) <= Math.PI * 0.35;
    }
}
