using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

namespace Aetherworks.Features;

public sealed unsafe partial class PartyDamage
{
    private sealed record CastShare(nint Node, uint Entity, string Label, Vector3 Color, byte[] Text);
    private readonly CastShare?[] castShares = new CastShare?[Slots];
    private readonly (nint Node, bool Visible, byte Alpha)[] castVisibility = new (nint, bool, byte)[Slots];

    private void DrawCastShares(AddonPartyList* addon)
    {
        var numbers = PartyListNumberArray.Instance();
        var strings = PartyListStringArray.Instance();
        if (addon == null || numbers == null || strings == null) return;
        for (int i = 0; i < Slots; i++)
        {
            var name = NameNode(addon, i);
            var share = castShares[i];
            if (name == null) { castShares[i] = null; continue; }

            uint id = RowId(numbers, i);
            var cast = i < PlayerSlots ? addon->PartyMembers[i].CastingActionName
                : addon->TrustMembers[i - PlayerSlots].CastingActionName;
            bool casting = cast != null && cast->IsVisible() && cast->Color.A != 0 &&
                !cast->NodeText.AsSpan().IsEmpty;
            if (!casting || id == 0 || id != entityIds[i] || labels[i] is not { } label)
            {
                if (share?.Node == (nint)name)
                {
                    var original = strings->Members[i].Name.AsSpan();
                    var idle = id != 0 && id == entityIds[i] && labels[i] is { } idleLabel
                        ? Replace(i, (nint)name, original, idleLabel) : null;
                    if (idle != null) name->SetText(idle.Replacement.AsSpan());
                    else name->SetText((ReadOnlySpan<byte>)[..original, 0]);
                    edits[i] = idle;
                }
                castShares[i] = null;
                continue;
            }

            if (share?.Node != (nint)name || share.Entity != id || share.Color != config.PartyDamageColor || share.Label != label)
                share = castShares[i] = new((nint)name, id, label, config.PartyDamageColor, [..PercentageText(label), 0]);

            if (!name->NodeText.AsSpan().SequenceEqual(share.Text.AsSpan(0, share.Text.Length - 1)))
                name->SetText(share.Text.AsSpan());

            bool visible = name->IsVisible();
            byte alpha = name->Color.A;
            if (visible && alpha != 0) continue;
            castVisibility[i] = ((nint)name, visible, alpha);
            name->Color.A = cast->Color.A;
            if (!visible) name->ToggleVisibility(true);
        }
    }

    private void RestoreCastVisibility(AddonPartyList* addon)
    {
        for (int i = 0; i < Slots; i++)
        {
            var draw = castVisibility[i];
            if (draw.Node == 0) continue;
            var name = NameNode(addon, i);
            if (name != null && (nint)name == draw.Node)
            {
                if (!draw.Visible) name->ToggleVisibility(false);
                name->Color.A = draw.Alpha;
            }
            castVisibility[i] = default;
        }
    }
}
