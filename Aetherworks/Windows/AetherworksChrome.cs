using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherworks.Windows;

internal static class AetherworksChrome
{
    private static readonly Vector4 Mint = new(.19f,.84f,.73f,1), Sky = new(.40f,.72f,.99f,1), Violet = new(.75f,.60f,.99f,1);
    private static uint Ink(Vector4 color) => ImGui.GetColorU32(color);

    internal static void Mark(Vector2 p,float size)
    {
        var dl = ImGui.GetWindowDrawList();
        float s = size / 28;
        var c = p + new Vector2(size / 2);
        // Small cog and aether spark, derived from this plugin's mark.
        for (int i=0;i<8;i++)
        {
            float a = i*MathF.PI/4;
            var direction = new Vector2(MathF.Cos(a),MathF.Sin(a));
            dl.AddLine(c+direction*10*s,c+direction*14*s,Ink(i<3?Mint:i<6?Sky:Violet),4*s);
        }
        dl.AddCircle(c,9*s,Ink(Sky),24,3*s);
        dl.AddQuadFilled(c+new Vector2(0,-6)*s,c+new Vector2(3,0)*s,c+new Vector2(0,6)*s,c+new Vector2(-3,0)*s,Ink(Mint));
    }

    internal static void NavigationIcon(Workspace area,System.Numerics.Vector2 o,float size,uint ink)
    {
        var dl = ImGui.GetWindowDrawList();
        float s = size / 24;
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
    }
}
