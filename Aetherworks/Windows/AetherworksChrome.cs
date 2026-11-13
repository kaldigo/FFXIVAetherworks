using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherworks.Windows;

internal static class AetherworksChrome
{
    internal static readonly Vector4 Background = new(.055f,.059f,.075f,1), Surface = new(.083f,.087f,.11f,1),
        Muted = new(.65f,.69f,.76f,1), Mint = new(.19f,.84f,.73f,1), Sky = new(.40f,.72f,.99f,1), Violet = new(.75f,.60f,.99f,1);
    internal static Vector4 Tone(UiTone tone) => tone switch
    {
        UiTone.Good => Mint, UiTone.Attention => new(1,.76f,.40f,1), UiTone.Error => new(1,.48f,.53f,1), _ => Muted
    };
    private static uint Ink(Vector4 color) => ImGui.GetColorU32(color);
    internal static void PushTheme(float s)
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg,Background);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(.92f,.94f,.97f,1));
        ImGui.PushStyleColor(ImGuiCol.TextDisabled,Muted);
        ImGui.PushStyleColor(ImGuiCol.Border,new Vector4(.21f,.24f,.29f,1));
        ImGui.PushStyleColor(ImGuiCol.Separator,new Vector4(.17f,.20f,.25f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,new Vector4(.12f,.14f,.18f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,new Vector4(.18f,.22f,.27f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,new Vector4(.16f,.29f,.31f,1));
        ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.14f,.20f,.24f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,new Vector4(.18f,.32f,.35f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,new Vector4(.14f,.38f,.36f,1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark,Mint);
        ImGui.PushStyleColor(ImGuiCol.Header,new Vector4(.14f,.25f,.27f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,new Vector4(.18f,.30f,.32f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,new Vector4(.18f,.36f,.36f,1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(12)*s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(10,6)*s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(8)*s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,9*s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,5*s);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,7*s);
    }
    internal static void PopTheme() { ImGui.PopStyleVar(6); ImGui.PopStyleColor(16); }
    internal static void BackgroundFill()
    {
        var p=ImGui.GetWindowPos();
        var size=ImGui.GetWindowSize();
        var dl=ImGui.GetWindowDrawList();
        dl.AddRectFilled(p,p+size,Ink(Background),ImGui.GetStyle().WindowRounding);
        dl.PushClipRect(p,p+size,true);
        // Quiet atmosphere inspired by the reference windows; controls remain opaque.
        dl.AddCircleFilled(p+new Vector2(size.X*.08f,size.Y*.10f),size.X*.34f,Ink(new(.18f,.65f,.59f,.035f)),64);
        dl.AddCircleFilled(p+new Vector2(size.X*.92f,size.Y*.42f),size.X*.32f,Ink(new(.29f,.45f,.79f,.028f)),64);
        dl.AddCircleFilled(p+new Vector2(size.X*.76f,size.Y*1.05f),size.X*.43f,Ink(new(.52f,.26f,.69f,.035f)),64);
        dl.PopClipRect();
    }

    internal static void Badge(Vector2 p,string text,UiTone tone,float s)
    {
        var dl=ImGui.GetWindowDrawList();
        var color=Tone(tone);
        var fill=color; fill.W=.13f;
        var border=color; border.W=.32f;
        float height=ImGui.GetTextLineHeight()+8*s;
        float width=ImGui.CalcTextSize(text).X+28*s;
        dl.AddRectFilled(p,p+new Vector2(width,height),Ink(fill),height/2);
        dl.AddRect(p,p+new Vector2(width,height),Ink(border),height/2,ImDrawFlags.None,1*s);
        dl.AddCircleFilled(p+new Vector2(10*s,height/2),2.5f*s,Ink(color),12);
        dl.AddText(p+new Vector2(18*s,4*s),Ink(color),text);
    }

    internal static bool Switch(string label,ref bool value,float s)
    {
        var p=ImGui.GetCursorScreenPos();
        var size=new Vector2(44,24)*s;
        ImGui.PushStyleColor(ImGuiCol.Button,Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,Vector4.Zero);
        bool changed=ImGui.Button("###"+label,size);
        ImGui.PopStyleColor(3);
        if(changed) value=!value;
        bool hover=ImGui.IsItemHovered();
        var dl=ImGui.GetWindowDrawList();
        var fill=value?new Vector4(.10f,.59f,.52f,1):new Vector4(.19f,.21f,.26f,1);
        if(hover) { fill.X+=.04f; fill.Y+=.04f; fill.Z+=.04f; }
        dl.AddRectFilled(p+new Vector2(s),p+size-new Vector2(s),Ink(fill),12*s);
        dl.AddCircleFilled(p+new Vector2(value?32:12,12)*s,8.5f*s,Ink(new(.94f,.96f,.98f,1)),24);
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) || ImGui.IsItemFocused())
            ImGui.SetTooltip(label+(value?": On":": Off"));
        return changed;
    }

    internal static void Header(string context,Action close,float s,UiTone tone=UiTone.Neutral)
    {
        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var window=ImGui.GetWindowPos();
        dl.AddRectFilled(window,new Vector2(window.X+ImGui.GetWindowSize().X,p.Y+44*s),Ink(new(.073f,.083f,.102f,1)),8*s,ImDrawFlags.RoundCornersTop);
        var c = p+new Vector2(17,18)*s;
        // Small cog and aether spark, derived from this plugin's mark.
        for (int i=0;i<8;i++)
        {
            float a = i*MathF.PI/4;
            var direction = new Vector2(MathF.Cos(a),MathF.Sin(a));
            dl.AddLine(c+direction*10*s,c+direction*14*s,Ink(i<3?Mint:i<6?Sky:Violet),4*s);
        }
        dl.AddCircle(c,9*s,Ink(Sky),24,3*s);
        dl.AddQuadFilled(c+new Vector2(0,-6)*s,c+new Vector2(3,0)*s,c+new Vector2(0,6)*s,c+new Vector2(-3,0)*s,Ink(Mint));
        float y = (36*s-ImGui.GetTextLineHeight())/2;
        dl.AddText(p+new Vector2(42*s,y),Ink(new(.96f,.97f,.99f,1)),"Aetherworks");
        float contextX = 42*s+ImGui.CalcTextSize("Aetherworks").X+16*s;
        if (ImGui.GetContentRegionAvail().X>contextX+ImGui.CalcTextSize(context).X+68*s)
            Badge(p+new Vector2(contextX,(36*s-ImGui.GetTextLineHeight()-8*s)/2),context,tone,s);
        ImGui.InvisibleButton("Move Aetherworks window",new Vector2(Math.Max(1,ImGui.GetContentRegionAvail().X-40*s),36*s));
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(ImGui.GetWindowPos()+ImGui.GetIO().MouseDelta);
        ImGui.SameLine(0,4*s);
        var button = ImGui.GetCursorScreenPos();
        ImGui.PushStyleColor(ImGuiCol.Button,Vector4.Zero);
        if (ImGui.Button("###Close Aetherworks",new Vector2(36*s))) close();
        ImGui.PopStyleColor();
        var center = button+new Vector2(18*s);
        dl.AddLine(center-new Vector2(4*s),center+new Vector2(4*s),Ink(Muted),1.6f*s);
        dl.AddLine(center+new Vector2(-4,4)*s,center+new Vector2(4,-4)*s,Ink(Muted),1.6f*s);
        if (ImGui.IsItemHovered() || ImGui.IsItemFocused()) ImGui.SetTooltip("Close Aetherworks");
        ImGui.Separator();
    }
    internal static bool RailItem(Workspace area,bool active,float s)
    {
        var p = ImGui.GetCursorScreenPos();
        var size = new Vector2(42,41)*s;
        ImGui.PushStyleColor(ImGuiCol.Button,Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,Vector4.Zero);
        bool clicked = ImGui.Button("###Rail "+area,size);
        ImGui.PopStyleColor(3);
        bool hover = ImGui.IsItemHovered(), focus = ImGui.IsItemFocused();
        var color = area==Workspace.Overview?Mint:area==Workspace.Features?Sky:Violet;
        var dl = ImGui.GetWindowDrawList();
        if (active || hover)
        {
            var fill = color; fill.W = active?.16f:.08f;
            dl.AddRectFilled(p+new Vector2(2*s),p+size-new Vector2(2*s),Ink(fill),6*s);
        }
        if (active) dl.AddLine(p+new Vector2(2,12)*s,p+new Vector2(2,29)*s,Ink(color),3*s);
        uint ink = Ink(active||hover||focus?color:Muted);
        var o = p+(size-new Vector2(24*s))/2;
        Vector2 P(float x,float y) => o+new Vector2(x,y)*s;
        void L(float x,float y,float x2,float y2) => dl.AddLine(P(x,y),P(x2,y2),ink,1.7f*s);
        switch (area)
        {
            case Workspace.Overview:
                for(int y=0;y<2;y++) for(int x=0;x<2;x++)
                    dl.AddRect(P(3+x*11,3+y*11),P(10+x*11,10+y*11),ink,1.4f*s,ImDrawFlags.None,1.6f*s);
                break;
            case Workspace.Features:
                for(int i=0;i<3;i++) { float y=5+i*7,x=7+i*4; L(2,y,22,y); dl.AddRectFilled(P(x-2,y-2),P(x+2,y+2),ink,1*s); }
                break;
            default:
                L(1,13,6,13); L(6,13,10,5); L(10,5,14,20); L(14,20,18,11); L(18,11,23,11);
                break;
        }
        if (hover || focus) ImGui.SetTooltip(SettingsNavigation.Name(area));
        return clicked;
    }
}
