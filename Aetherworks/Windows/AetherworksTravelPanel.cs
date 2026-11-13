using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherworks.Windows;

internal static class AetherworksTravelPanel
{
    internal static void Draw(string destination,uint fare,Action confirm,Action cancel,float scale)
    {
        AetherworksChrome.BackgroundFill();
        AetherworksChrome.Header("Travel confirmation",cancel,scale);
        ImGui.Spacing();
        ImGui.TextWrapped("Teleport home?");
        ImGui.PushStyleColor(ImGuiCol.Text,AetherworksChrome.Muted);
        ImGui.TextWrapped("Return is on cooldown. This trip uses paid Teleport.");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.TextWrapped(destination);
        ImGui.PushStyleColor(ImGuiCol.Text,AetherworksChrome.Tone(UiTone.Attention));
        ImGui.TextUnformatted($"Fare: {fare:N0} gil");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.TextWrapped("Normal aetheryte ticket settings apply. The trip is checked again when you confirm.");
        ImGui.Spacing();
        ImGui.Separator();
        float width=(ImGui.GetContentRegionAvail().X-ImGui.GetStyle().ItemSpacing.X)/2;
        ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.10f,.37f,.34f,1));
        if(ImGui.Button("Teleport home",new Vector2(width,0))) confirm();
        ImGui.PopStyleColor();
        ImGui.SameLine();
        if(ImGui.Button("Cancel",new Vector2(width,0))) cancel();
    }
}
