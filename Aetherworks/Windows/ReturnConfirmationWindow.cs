using System.Numerics;
using Aetherworks.Features;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Aetherworks.Windows;

public sealed class ReturnConfirmationWindow : Window
{
    private readonly ReturnViaTeleport fallback;
    public ReturnConfirmationWindow(ReturnViaTeleport fallback)
        : base("Teleport home?###AetherworksReturnConfirmation",ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse)
    {
        this.fallback=fallback;
        SizeConstraints=new WindowSizeConstraints { MinimumSize=new Vector2(360,0),MaximumSize=new Vector2(480,float.MaxValue) };
    }
    public void Open() { IsOpen=true; BringToFront(); }
    public override void OnClose() => fallback.Cancel();
    public override void PreDraw() => AetherworksChrome.PushTheme(ImGuiHelpers.GlobalScale);
    public override void PostDraw() => AetherworksChrome.PopTheme();
    public override void Draw()
    {
        if(fallback.GetPending() is not {} request) { IsOpen=false; return; }
        AetherworksTravelPanel.Draw(request.Destination,request.Fare,
            ()=> { fallback.Confirm(); IsOpen=false; },()=> { fallback.Cancel(); IsOpen=false; },ImGuiHelpers.GlobalScale);
    }
}
