using Dalamud.Configuration;

namespace Aetherworks;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public HashSet<string> EnabledTweaks { get; set; } = new(StringComparer.Ordinal);
}
