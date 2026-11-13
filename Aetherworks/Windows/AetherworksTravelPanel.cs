using AetherglassUI;

namespace Aetherworks.Windows;

internal static class AetherworksTravelPanel
{
    internal static void Draw(string destination, uint fare, Action confirm, Action cancel) =>
        Controls.ConfirmationBody($"Teleport home to {destination} for {fare:N0} gil?",
            "Return is on cooldown. Normal ticket settings apply. The destination and fare are checked again when you confirm.",
            "Teleport home", confirm, cancel);
}
