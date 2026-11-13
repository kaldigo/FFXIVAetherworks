using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

public sealed unsafe partial class PartyDamage
{
    private sealed record CastLabel(nint Node, string Share, Vector3 Color, byte[] Action, byte[] Text);
    private readonly CastLabel?[] castLabels = new CastLabel?[Slots];

    internal void ClearCastLabels() => Array.Clear(castLabels);

    internal void UpdateCastLabels(AddonPartyList* addon, bool restore = false)
    {
        if (addon == null) { ClearCastLabels(); return; }
        var numbers = PartyListNumberArray.Instance();
        var strings = PartyListStringArray.Instance();
        for (int i = 0; i < Slots; i++)
        {
            var name = NameNode(addon, i);
            if (name == null) { castLabels[i] = null; continue; }
            var cast = i < PlayerSlots ? addon->PartyMembers[i].CastingActionName
                : addon->TrustMembers[i - PlayerSlots].CastingActionName;
            if (cast == null) { castLabels[i] = null; continue; }

            var previous = castLabels[i];
            ReadOnlySpan<byte> action = strings != null ? strings->Members[i].CastingActionName.AsSpan() : [];
            bool showingCast = cast->IsVisible() && cast->Color.A != 0 &&
                (!name->IsVisible() || name->Color.A == 0);
            if (restore || !showingCast || action.IsEmpty || numbers == null || entityIds[i] == 0 ||
                RowId(numbers, i) != entityIds[i] || labels[i] is not { } share)
            {
                if (previous?.Node == (nint)cast && cast->NodeText.AsSpan().SequenceEqual(previous.Text.AsSpan(0, previous.Text.Length - 1)))
                {
                    if (strings == null) cast->SetText(previous.Action.AsSpan());
                    else cast->SetText((ReadOnlySpan<byte>)[..action, 0]);
                }
                castLabels[i] = null;
                continue;
            }
            if (previous?.Node != (nint)cast || previous.Share != share || previous.Color != config.PartyDamageColor ||
                !action.SequenceEqual(previous.Action.AsSpan(0, previous.Action.Length - 1)))
            {
                previous = castLabels[i] = new((nint)cast, share, config.PartyDamageColor, [..action, 0],
                    PartyLevelText.PrependPercentage(action, 0, 0, PercentageText(share)));
            }
            if (!cast->NodeText.AsSpan().SequenceEqual(previous.Text.AsSpan(0, previous.Text.Length - 1)))
                cast->SetText(previous.Text.AsSpan());
        }
    }
}
