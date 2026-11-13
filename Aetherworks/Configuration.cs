using Dalamud.Configuration;

namespace Aetherworks;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public HashSet<string> EnabledTweaks { get; set; } = new(StringComparer.Ordinal);
    public bool PartyDamageBeforeRequestedUpdate { get; set; }
    public System.Numerics.Vector3 PartyDamageColor { get; set; } = new(1f, 0.83f, 0.52f);
    public bool MeterShowJob { get; set; } = true;
    public bool MeterShowDamage { get; set; } = true;
    public bool MeterShowDps { get; set; } = true;
    public bool SkipPaidTeleportConfirmation { get; set; }
}
