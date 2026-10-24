using System.Numerics;
using Aetherworks.Features;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherworks.Windows;

public sealed class ReturnConfirmationWindow(ReturnViaTeleport fallback)
    : Window("Teleport home?###AetherworksReturnConfirmation", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
{
    public void Open() { IsOpen = true; BringToFront(); }
    public override void OnClose() => fallback.Cancel();
    public override void Draw()
    {
        if (fallback.GetPending() is not { } request) { IsOpen = false; return; }
        ImGui.TextUnformatted("Return is on cooldown.");
        ImGui.TextUnformatted($"Teleport to {request.Destination}?");
        ImGui.TextUnformatted($"Fare: {request.Fare:N0} gil");
        ImGui.TextDisabled("Normal aetheryte ticket settings apply.");
        ImGui.Spacing();
        if (ImGui.Button("Teleport", new Vector2(130, 0))) { fallback.Confirm(); IsOpen = false; }
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(130, 0))) { fallback.Cancel(); IsOpen = false; }
    }
}
