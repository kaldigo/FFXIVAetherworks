using System.Numerics;
using Aetherworks.Windows;
using Glasswork;
using Glasswork.Backend.Preview;
using Dalamud.Bindings.ImGui;

namespace AetherworksPreview;

public abstract class Screen : IPreviewScreen
{
    private readonly AetherworksShell shell = new();
    private AetherworksUiState state;
    private int frames;
    private protected SettingsNavigation Navigation => shell.Navigation;
    private protected AetherworksUiState State { get=>state; set=>state=value; }
    private readonly AetherworksUiActions actions;
    protected Screen(WorkspaceStart start=WorkspaceStart.Overview,string? feature=null,bool problems=false,bool allOff=false)
    {
        state = Sample(problems,allOff);
        shell.Navigation.OpenArea((Workspace)start);
        if(feature!=null) shell.Navigation.OpenFeature(feature);
        actions = new((id,value)=>state=state with { Features=state.Features.Select(f=>f.Id==id
            ? f with { Enabled=value,Status=value?"Enabled. Ready when needed.":"This convenience is switched off.",Tone=value?UiTone.Good:UiTone.Neutral } : f).ToArray() },
            SetConfirmationPreference,
            _=>state=state with { Conflicts=[] },
            ()=>state=state with { SaveError=null },()=>{});
    }
    public abstract string Name { get; }
    public virtual Vector2 WindowSize => new(780,700);
    public Look Look => Look.NeutralDark();
    public virtual ReadOnlySpan<PreviewInput> Script => [];
    public virtual float UiScale=>1;
    protected virtual void Verify(int frame) { }
    protected void Search(string value) => shell.Navigation.Search=value;
    protected void SetConfirmationPreference(bool skip)
    {
        state=state with { SkipConfirmation=skip,Features=state.Features.Select(f=>f.Id==AetherworksShell.ReturnId && f.Enabled
            ? f with { Status=skip?"Paid fallback enabled. Teleport starts without a fare confirmation.":"Paid fallback enabled. Review the fare before travelling." }:f).ToArray() };
    }
    public void Draw()
    {
        ImGui.GetIO().FontGlobalScale=UiScale;
        ImGui.SetWindowFontScale(1);
        AetherworksChrome.PushTheme(UiScale);
        try { shell.Draw(state,actions,UiScale); Verify(++frames); }
        finally { AetherworksChrome.PopTheme(); }
    }
    private static AetherworksUiState Sample(bool problems,bool allOff)
    {
        FeatureView[] features = [
            new(AetherworksShell.ReturnId,"Return via Teleport","Actions","Teleport home while Return is on cooldown",!allOff,false,null,
                allOff?"This convenience is switched off.":"Paid fallback enabled. Review the fare before travelling.",allOff?UiTone.Neutral:UiTone.Good),
            new(AetherworksShell.HotbarId,"Shared Hotbar Sync","Hotbars","Sync shared PvE and PvP keyboard and cross hotbar assignments",!allOff,false,null,
                allOff?"This convenience is switched off.":problems?"2 conflicting slots need a choice. Other shared slots continue syncing.":"Syncing 3 shared hotbars in both directions.",allOff?UiTone.Neutral:problems?UiTone.Attention:UiTone.Good),
            new(AetherworksShell.SaddlebagId,"Saddlebag Withdraw All but One","Items and storage","Leave one in the selected stack for duplicate entrusting",false,false,null,"This convenience is switched off.")
        ];
        return new(features,false,false,allOff?[]:["Hotbar 1","Hotbar 2","Cross hotbar 1"],
            problems?["Hotbar 1, slot 4","Cross hotbar 1, slot 12"]:[],problems?"Your changes may not survive a reload. Retry saving to keep them.":null);
    }
}
public enum WorkspaceStart { Overview, Features, Recovery }
public sealed class Overview() : Screen { public override string Name=>"overview"; public override Vector2 WindowSize=>new(780,610); }
public sealed class OverviewCompact() : Screen { public override string Name=>"overview-compact"; public override Vector2 WindowSize=>new(360,640); }
public sealed class FirstRun() : Screen(allOff:true) { public override string Name=>"first-run"; }
public sealed class Features() : Screen(WorkspaceStart.Features) { public override string Name=>"features"; }
public sealed class Return() : Screen(WorkspaceStart.Features,"return-via-teleport") { public override string Name=>"return"; }
public sealed class ReturnCompact() : Screen(WorkspaceStart.Features,"return-via-teleport") { public override string Name=>"return-compact"; public override Vector2 WindowSize=>new(360,720); }
public sealed class Hotbars() : Screen(WorkspaceStart.Features,"shared-hotbar-sync") { public override string Name=>"hotbars"; }
public sealed class HotbarsOff() : Screen(WorkspaceStart.Features,"shared-hotbar-sync",allOff:true) { public override string Name=>"hotbars-off"; }
public sealed class Saddlebag() : Screen(WorkspaceStart.Features,"saddlebag-withdraw-leave-one") { public override string Name=>"saddlebag"; }
public sealed class Conflicts() : Screen(WorkspaceStart.Features,"shared-hotbar-sync",problems:true) { public override string Name=>"conflicts"; public override Vector2 WindowSize=>new(780,840); }
public sealed class Recovery() : Screen(WorkspaceStart.Recovery,problems:true) { public override string Name=>"recovery"; }
public sealed class RecoveryCompact() : Screen(WorkspaceStart.Recovery,problems:true) { public override string Name=>"recovery-compact"; public override Vector2 WindowSize=>new(360,720); }
public sealed class Minimum() : Screen(WorkspaceStart.Features,"shared-hotbar-sync",problems:true) { public override string Name=>"minimum"; public override Vector2 WindowSize=>new(360,320); }
public sealed class LargeScale() : Screen(WorkspaceStart.Features,"return-via-teleport")
{
    public override string Name=>"scale-150";
    public override float UiScale=>1.5f;
    public override Vector2 WindowSize=>new(600,1050);
}
public sealed class SkipConfirmation : Screen
{
    public SkipConfirmation():base(WorkspaceStart.Features,"return-via-teleport")=>SetConfirmationPreference(true);
    public override string Name=>"return-immediate";
}
public sealed class Faulted : Screen
{
    public Faulted():base(WorkspaceStart.Recovery)
    {
        State=State with { SyncFaulted=true, Features=State.Features.Select(f=>f.Id==AetherworksShell.HotbarId
            ? f with { Status="Sync paused: the saved history could not be read. Disable and re-enable the feature to retry.",Tone=UiTone.Error }:f).ToArray() };
    }
    public override string Name=>"sync-paused";
}
public sealed class Cleanup : Screen
{
    public Cleanup():base(WorkspaceStart.Features,"saddlebag-withdraw-leave-one")
    {
        State=State with { Features=State.Features.Select(f=>f.Id==AetherworksShell.SaddlebagId
            ? f with { NeedsCleanup=true, Error="Cleanup failed; some behavior may still be active",Status="Cleanup is incomplete.",Tone=UiTone.Error }:f).ToArray() };
    }
    public override string Name=>"cleanup";
    public override Vector2 WindowSize=>new(360,760);
}
public sealed class Navigate() : Screen
{
    private static readonly PreviewInput[] Steps=[new(3,PreviewInputKind.Press,new(36,138)),new(4,PreviewInputKind.Release,new(36,138)),new(10,PreviewInputKind.Move,new(300,40))];
    public override ReadOnlySpan<PreviewInput> Script=>Steps;
    public override string Name=>"navigate-features";
    protected override void Verify(int frame)
    {
        if(frame==9 && Navigation.Area!=Workspace.Features) throw new InvalidOperationException("Rail navigation did not open Features.");
    }
}
public sealed class BackToSearch : Screen
{
    public BackToSearch():base(WorkspaceStart.Features,"shared-hotbar-sync")=>Search("hotbar");
    private static readonly PreviewInput[] Steps=[new(3,PreviewInputKind.Press,new(100,82)),new(4,PreviewInputKind.Release,new(100,82)),new(10,PreviewInputKind.Move,new(300,40))];
    public override ReadOnlySpan<PreviewInput> Script=>Steps;
    public override string Name=>"back-to-search";
    protected override void Verify(int frame)
    {
        if(frame==9 && (Navigation.Area!=Workspace.Features || Navigation.Feature!=null || Navigation.Search!="hotbar"))
            throw new InvalidOperationException("Back did not preserve the originating feature search.");
    }
}
public sealed class OpenDetail : Screen
{
    public OpenDetail():base(WorkspaceStart.Features)=>Search("hotbar");
    private static readonly PreviewInput[] Steps=[new(3,PreviewInputKind.Press,new(710,225)),new(4,PreviewInputKind.Release,new(710,225)),new(10,PreviewInputKind.Move,new(300,40))];
    public override ReadOnlySpan<PreviewInput> Script=>Steps;
    public override string Name=>"open-detail";
    protected override void Verify(int frame)
    {
        if(frame==9 && Navigation.Feature!=AetherworksShell.HotbarId)
            throw new InvalidOperationException("Configure did not open the Shared Hotbar Sync detail page.");
    }
}
public sealed class Waiting : Screen
{
    public Waiting():base()
    {
        State=State with { SharedBars=[], Features=State.Features.Select(f=>f.Id==AetherworksShell.HotbarId
            ? f with { Status="Waiting for character, job and hotbar changes to settle.",Tone=UiTone.Neutral }:f).ToArray() };
    }
    public override string Name=>"waiting";
}
public sealed class ToggleFeature() : Screen(WorkspaceStart.Features,"return-via-teleport")
{
    private static readonly PreviewInput[] Steps=[new(3,PreviewInputKind.Press,new(714,200)),new(4,PreviewInputKind.Release,new(714,200)),new(10,PreviewInputKind.Move,new(300,40))];
    public override ReadOnlySpan<PreviewInput> Script=>Steps;
    public override string Name=>"toggle-feature";
    protected override void Verify(int frame)
    {
        if(frame==9 && State.Features.First(f=>f.Id==AetherworksShell.ReturnId).Enabled)
            throw new InvalidOperationException("The feature switch did not invoke its enable callback.");
    }
}
public sealed class ToggleConfirmation() : Screen(WorkspaceStart.Features,"return-via-teleport")
{
    private static readonly PreviewInput[] Steps=[new(3,PreviewInputKind.Press,new(714,376)),new(4,PreviewInputKind.Release,new(714,376)),new(10,PreviewInputKind.Move,new(300,40))];
    public override ReadOnlySpan<PreviewInput> Script=>Steps;
    public override string Name=>"toggle-confirmation";
    protected override void Verify(int frame)
    {
        if(frame==9 && !State.SkipConfirmation)
            throw new InvalidOperationException("Turning off Ask before paid travel must enable the existing skip-confirmation preference.");
    }
}
public sealed class MinimumScrolled() : Screen(WorkspaceStart.Features,"shared-hotbar-sync",problems:true)
{
    public override string Name=>"minimum-scrolled";
    public override Vector2 WindowSize=>new(360,320);
    private static readonly PreviewInput[] Steps=[new(4,PreviewInputKind.Wheel,new(220,270),Wheel:-4),new(12,PreviewInputKind.Move,new(220,270))];
    public override ReadOnlySpan<PreviewInput> Script=>Steps;
}
public sealed class Travel : IPreviewScreen
{
    public string Name=>"travel-confirmation";
    public Vector2 WindowSize=>new(420,370);
    public Look Look=>Look.NeutralDark();
    public ReadOnlySpan<PreviewInput> Script=>[];
    public void Draw()
    {
        AetherworksChrome.PushTheme(1);
        try { AetherworksTravelPanel.Draw("Old Sharlayan",999,()=>{},()=>{},1); }
        finally { AetherworksChrome.PopTheme(); }
    }
}
public sealed class EmptySearch : Screen
{
    public EmptySearch():base(WorkspaceStart.Features) => Search("glamour");
    public override string Name=>"empty-search";
    public override Vector2 WindowSize=>new(360,480);
}
public sealed class Hover() : Screen
{
    public override string Name=>"rail-hover";
    private static readonly PreviewInput[] HoverScript=[new(3,PreviewInputKind.Move,new(32,130)),new(65,PreviewInputKind.Move,new(32,130))];
    public override ReadOnlySpan<PreviewInput> Script=>HoverScript;
}
