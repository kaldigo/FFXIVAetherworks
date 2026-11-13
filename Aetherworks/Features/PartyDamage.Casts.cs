using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

namespace Aetherworks.Features;

public sealed unsafe partial class PartyDamage
{
    private sealed record CastShare(string Label, Vector3 Color, byte[] Text);
    private struct CastDraw
    {
        public nint Node;
        public byte[] Original;
        public byte Alpha;
        public bool Visible;
    }

    private readonly CastShare?[] castShares = new CastShare?[Slots];
    private readonly CastDraw[] castDraws = new CastDraw[Slots];

    internal void DrawCastShares(AddonPartyList* addon)
    {
        var numbers = PartyListNumberArray.Instance();
        if (addon == null || numbers == null) return;
        for (int i = 0; i < Slots; i++)
        {
            var name = NameNode(addon, i);
            if (name == null || entityIds[i] == 0 || RowId(numbers, i) != entityIds[i] ||
                labels[i] is not { } label || castDraws[i].Node != 0) continue;

            bool visible = name->IsVisible();
            byte alpha = name->Color.A;
            if (visible && alpha != 0) continue;
            var cast = i < PlayerSlots ? addon->PartyMembers[i].CastingActionName
                : addon->TrustMembers[i - PlayerSlots].CastingActionName;
            if (cast == null || !cast->IsVisible() || cast->Color.A == 0 ||
                cast->NodeText.AsSpan().IsEmpty) continue;

            var share = castShares[i];
            if (share == null || share.Label != label || share.Color != config.PartyDamageColor)
                share = castShares[i] = new(label, config.PartyDamageColor, [..PercentageText(label), 0]);

            // Draw through the idle name node for this draw call only. This uses
            // the same font, row transform, text origin and padded percentage.
            var current = name->NodeText.AsSpan();
            var row = edits[i];
            byte[] original = row != null && IsApplied(name, row) ? row.Replacement : [..current, 0];
            castDraws[i] = new() { Node = (nint)name, Original = original, Visible = visible, Alpha = alpha };
            name->SetText(share.Text.AsSpan());
            name->Color.A = cast->Color.A;
            if (!visible) name->ToggleVisibility(true);
        }
    }

    internal void RestoreCastDraw(AddonPartyList* addon)
    {
        for (int i = 0; i < Slots; i++)
        {
            ref var draw = ref castDraws[i];
            if (draw.Node == 0) continue;
            var name = NameNode(addon, i);
            if (name != null && (nint)name == draw.Node)
            {
                if (!draw.Visible) name->ToggleVisibility(false);
                name->Color.A = draw.Alpha;
                name->SetText(draw.Original.AsSpan());
            }
            draw = default;
        }
    }
}
