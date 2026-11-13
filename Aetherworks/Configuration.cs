using Dalamud.Configuration;

namespace Aetherworks;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public HashSet<string> EnabledTweaks { get; set; } = new(StringComparer.Ordinal);
    public bool PartyDamageBeforeRequestedUpdate { get; set; }
    public System.Numerics.Vector3 PartyDamageColor { get; set; } = new(1f, 0.83f, 0.52f);
    public EmberOptions Ember { get; set; } = new();
    public bool SkipPaidTeleportConfirmation { get; set; }
}

public sealed class EmberOptions
{
    public bool Open;
    public float Zoom = 100;
    public float Opacity = 100;
    public bool Locked;
    public bool HideOutOfCombat;
    public bool HideInPvP = true;
    public int HideDelay;
}
