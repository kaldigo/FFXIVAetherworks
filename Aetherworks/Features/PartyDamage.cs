using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using Aetherworks.Tweaks;
using Aetherworks.Windows;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

public sealed unsafe class PartyDamage(IDalamudPluginInterface plugin, IAddonLifecycle lifecycle, IAddonEventManager events, IFramework framework,
    IGameGui gui, IObjectTable objects, IClientState client, Configuration config, IPluginLog log, Action toggleEmber, Action<bool> enableEmber) : ITweak
{
    public const string FeatureId = "party-damage";
    private const string Addon = "_PartyList";
    private const int PlayerSlots = 8;
    private const int Slots = PlayerSlots + 7;
    private readonly PartyDamageFeed feed = new(plugin, log);
    private IAddonEventHandle? headerClick;
    private nint headerAddon;
    private nint headerNode;
    private byte[] headerOriginal = [];
    private byte[] headerApplied = [];
    private bool headerOpen;
    private NodeFlags addedHeaderFlags;
    private readonly string?[] labels = new string?[Slots];
    private readonly bool[] castingRows = new bool[Slots];
    private readonly int[] ranks = new int[Slots];
    private readonly IndicatorEdit?[] indicators = new IndicatorEdit?[Slots];
    private sealed record IndicatorEdit(nint Node, string Original, char Glyph, string Share, bool Casting,
        System.Numerics.Vector3 Color, byte[] Applied, ushort BaseWidth, ushort Width);
    private readonly RowSpace?[] castSpace = new RowSpace?[Slots];
    private sealed record RowSpace(nint Node, float X, ushort Width, int Shift, bool AddedEllipsis);
    private readonly string[] roster = new string[Slots];
    private readonly uint[] entityIds = new uint[Slots];
    private readonly Edit?[] cache = new Edit?[Slots];
    private readonly Edit?[] edits = new Edit?[Slots];
    private sealed record Edit(nint Target, string Member, string Label, byte[] Original, byte[] Replacement);
    private IReadOnlyDictionary<string, DamageEntry>? previousDamage;
    private bool enabled;
    private long nextPoll;
    private string? localName;
    public string Id => FeatureId;
    public string Name => "Party list damage";
    public string Description => "Show damage rank in the party-slot icon and IINACT damage share beside the name.";
    public string Category => "Party list";
    public string Status => !feed.Connected ? "Waiting for IINACT." : labels.All(label => label == null)
        ? "Connected. Waiting for matching party damage." : "Showing damage from the latest encounter.";

    public void Enable()
    {
        enabled = true;
        enableEmber(true);
        feed.Start();
        framework.Update += Update;
        client.TerritoryChanged += TerritoryChanged;
        lifecycle.RegisterListener(AddonEvent.PreDraw, Addon, BeforeDraw);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, Addon, BeforeFinalize);
        lifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, Addon, AfterUpdate);
        nextPoll = 0;
    }

    public void Disable()
    {
        DetachHeader(CurrentAddon());
        enableEmber(false);
        RestoreRows(CurrentAddon());
        enabled = false;
        framework.Update -= Update;
        client.TerritoryChanged -= TerritoryChanged;
        lifecycle.UnregisterListener(BeforeDraw);
        lifecycle.UnregisterListener(BeforeFinalize);
        lifecycle.UnregisterListener(AfterUpdate);
        feed.Stop();
        Array.Clear(castingRows);
        Array.Clear(castSpace);
        Array.Clear(indicators);
        Array.Clear(edits);
        Array.Clear(cache);
        Array.Clear(labels);
        previousDamage = null;
        RequestRefresh();
    }

    public void SetColor(System.Numerics.Vector3 color)
    {
        config.PartyDamageColor = color;
        Array.Clear(cache);
        if (enabled && config.PartyDamageBeforeRequestedUpdate) RequestRefresh();
    }

    public void SetEntryPoint(bool beforeUpdate)
    {
        config.PartyDamageBeforeRequestedUpdate = beforeUpdate;
        if (enabled) RequestRefresh();
    }

    private void TerritoryChanged(uint territory)
    {
        RestoreRows(CurrentAddon());
        feed.Clear();
        Array.Clear(labels);
        RequestRefresh();
    }

    private static void RequestRefresh()
    {
        var stage = AtkStage.Instance();
        var data = stage == null ? null : stage->GetStringArrayData(StringArrayType.PartyList);
        if (data != null) data->UpdateState = 2;
    }

    private void Update(IFramework _)
    {
        if (Environment.TickCount64 < nextPoll) return;
        nextPoll = Environment.TickCount64 + 100;
        feed.CheckConnection();
        var numbers = PartyListNumberArray.Instance();
        var player = objects.LocalPlayer?.Name.TextValue;
        var addon = CurrentAddon();
        AttachHeader(addon);
        var damage = feed.Damage;
        bool changed = damage != previousDamage || player != localName;
        localName = player;
        previousDamage = damage;
        for (int i = 0; i < Slots; i++)
        {
            var node = NameNode(addon, i);
            uint id = node == null || numbers == null ? 0 : RowId(numbers, i);
            string name = id == 0 ? "" : objects.SearchByEntityId(id)?.Name.TextValue ?? "";
            if (entityIds[i] == id && roster[i] == name) continue;
            changed = true;
            entityIds[i] = id;
            roster[i] = name;
        }
        if (!changed) return;
        PartyDamageRanks.Calculate(roster, PlayerSlots, player, damage, labels, ranks);
        if (config.PartyDamageBeforeRequestedUpdate) RequestRefresh();
    }

    // Keep any SeString payloads in the displayed name byte-for-byte intact.
    private Edit? Replace(int row, nint target, ReadOnlySpan<byte> text, string label)
    {
        if (text.IsEmpty) return null;
        if (cache[row] is { } cached && cached.Target == target && cached.Member == roster[row] && cached.Label == label &&
            text.SequenceEqual(cached.Original.AsSpan(0, cached.Original.Length - 1))) return cached;
        var (start, end) = LevelRange(text);
        if (start < 0) return null;
        var percentage = StyledShare(label);
        return cache[row] = new(target, roster[row], label, [..text, 0],
            PartyLevelText.PrependPercentage(text, start, end, percentage.ToArray()));
    }

    private Lumina.Text.SeStringBuilder StyledShare(string label)
    {
        var prefix = new Lumina.Text.SeStringBuilder()
            .PushColorRgba(new System.Numerics.Vector4(config.PartyDamageColor, 1f));
        foreach (char ch in label.AsSpan().TrimEnd())
        {
            // The level field uses the game's numeral icons, including their heavier strokes.
            prefix.Append(char.IsAsciiDigit(ch)
                ? (char)((int)Dalamud.Game.Text.SeIconChar.Number0 + ch - '0')
                : ch);
        }
        prefix.PopColor();
        return prefix;
    }

    private AddonPartyList* CurrentAddon() => (AddonPartyList*)gui.GetAddonByName(Addon).Address;

    private static AtkTextNode* NameNode(AddonPartyList* addon, int slot, bool indicator = false)
    {
        if (addon == null) return null;
        if (slot < PlayerSlots)
            return slot >= addon->MemberCount ? null : indicator ? addon->PartyMembers[slot].GroupSlotIndicator : addon->PartyMembers[slot].Name;
        int npc = slot - PlayerSlots;
        return npc >= addon->TrustCount ? null : indicator ? addon->TrustMembers[npc].GroupSlotIndicator : addon->TrustMembers[npc].Name;
    }

    private static bool IsApplied(AtkTextNode* node, Edit edit) => (nint)node == edit.Target &&
        node->NodeText.AsSpan().SequenceEqual(edit.Replacement.AsSpan(0, edit.Replacement.Length - 1));

    private static (int Start, int End) LevelRange(ReadOnlySpan<byte> text)
    {
        int offset = 0;
        foreach (var payload in new Lumina.Text.ReadOnly.ReadOnlySeStringSpan(text))
        {
            if (payload.Type == Lumina.Text.ReadOnly.ReadOnlySePayloadType.Text)
            {
                var (start, end) = PartyLevelText.Locate(payload.Body);
                if (start >= 0) return (offset + start, offset + end);
            }
            offset += payload.EnvelopeByteLength;
        }
        return (-1, -1);
    }

    private static uint RowId(PartyListNumberArray* numbers, int row) => row < PlayerSlots
        ? (uint)numbers->PartyMembers[row].EntityId : (uint)numbers->TrustMembers[row - PlayerSlots].EntityId;

    private void UpdateIndicator(AddonPartyList* addon, PartyListStringArray* strings, int row, int rank, string? share, bool casting = false)
    {
        var node = NameNode(addon, row, indicator: true);
        var saved = indicators[row];
        if (node == null) { indicators[row] = null; return; }
        if (saved?.Node != (nint)node) saved = null;
        var current = node->NodeText.AsSpan();
        bool ours = saved != null && current.SequenceEqual(saved.Applied.AsSpan(0, saved.Applied.Length - 1));
        string original = ours ? saved!.Original : node->NodeText.ToString();
        var source = strings->Members[row].GroupSlotIndicator;
        if (!source.AsSpan().IsEmpty) original = source.ToString();
        ushort width = saved != null && node->Width == saved.Width ? saved.BaseWidth : node->Width;
        bool applied = false;
        if (share != null && original.Length == 1)
        {
            char glyph = original[0];
            bool self = glyph is >= '\uE0E0' and <= '\uE0E9';
            if (self || glyph is >= '\uE08F' and <= '\uE0A2')
            {
                if (rank > 0) glyph = (char)((self && rank <= 9 ? 0xE0E0 : 0xE08F) + rank);
                if (saved == null || saved.Original != original || saved.Glyph != glyph || saved.Share != share || saved.Casting != casting ||
                    saved.Color != config.PartyDamageColor || saved.BaseWidth != width)
                {
                    byte[] bytes = casting
                        ? [..new Lumina.Text.SeStringBuilder().Append(glyph).Append(' ').ToArray(), ..StyledShare(share).ToArray(), 0]
                        : [..System.Text.Encoding.UTF8.GetBytes(glyph.ToString()), 0];
                    ushort measured = 0, height = 0;
                    fixed (byte* text = bytes) node->GetTextDrawSize(&measured, &height, text);
                    saved = new((nint)node, original, glyph, share, casting, config.PartyDamageColor, bytes, width,
                        casting ? (ushort)Math.Max(width, measured + 2) : width);
                    ours = false;
                }
                if (node->Width != saved.Width) node->SetWidth(saved.Width);
                if (!ours) node->SetText(saved.Applied.AsSpan());
                indicators[row] = saved;
                applied = true;
            }
        }
        if (!applied)
        {
            if (ours) node->SetText(original);
            if (node->Width != width) node->SetWidth(width);
            indicators[row] = null;
        }
        var cast = row < PlayerSlots ? addon->PartyMembers[row].CastingActionName
            : addon->TrustMembers[row - PlayerSlots].CastingActionName;
        // The cast label starts to the left of the badge and is right aligned.
        ReserveSpace(cast, casting && applied ? saved!.Width + 4 : 0, ref castSpace[row]);
    }

    private static void ReserveSpace(AtkTextNode* node, int shift, ref RowSpace? saved)
    {
        if (node == null || saved?.Node != (nint)node) saved = null;
        if (node == null || (saved == null && shift == 0)) return;
        float x = saved != null && node->X == saved.X + saved.Shift ? saved.X : node->X;
        ushort width = saved != null && node->Width == saved.Width - saved.Shift ? saved.Width : node->Width;
        shift = Math.Min(shift, Math.Max(0, width - 1));
        bool addEllipsis = saved?.AddedEllipsis ?? !node->TextFlags.HasFlag(TextFlags.Ellipsis);
        if (node->X != x + shift) node->SetXFloat(x + shift);
        if (node->Width != width - shift) node->SetWidth((ushort)(width - shift));
        if (addEllipsis)
        {
            if (shift > 0) node->TextFlags |= TextFlags.Ellipsis;
            else node->TextFlags &= ~TextFlags.Ellipsis;
        }
        if (shift == 0) saved = null;
        else if (saved == null || saved.X != x || saved.Width != width || saved.Shift != shift)
            saved = new((nint)node, x, width, shift, addEllipsis);
    }

    private void ApplyRows(AddonPartyList* addon, bool transitionsOnly = false)
    {
        var numbers = PartyListNumberArray.Instance();
        var strings = PartyListStringArray.Instance();
        if (numbers == null || strings == null) return;
        for (int i = 0; i < Slots; i++)
        {
            var node = NameNode(addon, i);
            var cast = node == null ? null : i < PlayerSlots ? addon->PartyMembers[i].CastingActionName
                : addon->TrustMembers[i - PlayerSlots].CastingActionName;
            bool casting = cast != null && cast->IsVisible() && cast->Color.A != 0 &&
                (!node->IsVisible() || node->Color.A == 0);
            if (transitionsOnly && castingRows[i] == casting) continue;
            castingRows[i] = casting;
            var previous = edits[i];
            uint id = RowId(numbers, i);
            var original = strings->Members[i].Name.AsSpan();
            UpdateIndicator(addon, strings, i, ranks[i], id != 0 && id == entityIds[i] ? labels[i] : null,
                casting);
            if (node == null) { edits[i] = null; continue; }
            if (original.IsEmpty) continue;
            if (labels[i] is not { } label || entityIds[i] == 0 || RowId(numbers, i) != entityIds[i])
            {
                if (previous?.Target == (nint)node) node->SetText((ReadOnlySpan<byte>)[..original, 0]);
                edits[i] = null;
                continue;
            }
            // NodeText may already contain ellipses or our styled prefix. The native input
            // remains complete across text flow and supplies every byte for the new label.
            var edit = Replace(i, (nint)node, original, label);
            if (edit == null || IsApplied(node, edit)) continue;
            edits[i] = edit;
            node->SetText(edit.Replacement.AsSpan());
        }
    }

    private void RestoreRows(AddonPartyList* addon)
    {
        var strings = PartyListStringArray.Instance();
        for (int i = 0; i < Slots; i++)
        {
            var previous = edits[i];
            edits[i] = null;
            if (strings != null) UpdateIndicator(addon, strings, i, 0, null);
            var node = NameNode(addon, i);
            if (node == null || previous?.Target != (nint)node || strings == null) continue;
            var original = strings->Members[i].Name.AsSpan();
            if (!original.IsEmpty) node->SetText((ReadOnlySpan<byte>)[..original, 0]);
        }
    }

    private void BeforeDraw(AddonEvent _, AddonArgs args)
    {
        var addon = (AddonPartyList*)args.Addon.Address;
        if ((nint)addon == headerAddon && addon != null && (nint)addon->PartyTypeTextNode == headerNode && headerNode != 0)
            DrawHeaderIndicator(addon->PartyTypeTextNode);
        ApplyRows(addon, transitionsOnly: config.PartyDamageBeforeRequestedUpdate);
    }

    private void DrawHeaderIndicator(AtkTextNode* title)
    {
        var current = title->NodeText.AsSpan();
        if (current.IsEmpty) return;
        bool ours = headerApplied.Length > 0 && current.SequenceEqual(headerApplied.AsSpan(0, headerApplied.Length - 1));
        if (ours && headerOpen == config.Ember.Open) return;
        if (!ours) headerOriginal = [..current, 0];
        headerOpen = config.Ember.Open;
        ReadOnlySpan<byte> suffix = headerOpen ? " - Hide"u8 : " - Show"u8;
        headerApplied = [..headerOriginal.AsSpan(0, headerOriginal.Length - 1), ..suffix, 0];
        title->SetText(headerApplied.AsSpan());
    }

    private void AttachHeader(AddonPartyList* addon)
    {
        var title = addon == null ? null : addon->PartyTypeTextNode;
        if (headerClick != null && headerAddon == (nint)addon && headerNode == (nint)title) return;
        DetachHeader(addon);
        if (title == null) return;
        headerClick = events.AddEvent((nint)addon, (nint)title, AddonEventType.MouseClick, OnHeaderClick);
        if (headerClick == null) return;
        headerAddon = (nint)addon;
        headerNode = (nint)title;
        addedHeaderFlags = (NodeFlags.HasCollision | NodeFlags.RespondToMouse | NodeFlags.EmitsEvents) & ~title->NodeFlags;
        title->NodeFlags |= addedHeaderFlags;
        addon->UpdateCollisionNodeList(false);
    }

    private void OnHeaderClick(AddonEventType type, AddonEventData data)
    {
        if (!enabled || type != AddonEventType.MouseClick || data.AtkEventDataPointer == 0) return;
        var mouse = ((AtkEventData*)data.AtkEventDataPointer)->MouseData;
        if (mouse.ButtonId == 0 && !mouse.Modifier.HasFlag(ModifierFlag.Dragging))
            toggleEmber();
    }

    private void DetachHeader(AddonPartyList* addon)
    {
        if (headerClick == null) return;
        events.RemoveEvent(headerClick);
        if (addon != null && (nint)addon == headerAddon && (nint)addon->PartyTypeTextNode == headerNode)
        {
            var title = addon->PartyTypeTextNode;
            if (headerApplied.Length > 0 && title->NodeText.AsSpan().SequenceEqual(headerApplied.AsSpan(0, headerApplied.Length - 1)))
                title->SetText(headerOriginal.AsSpan());
            title->NodeFlags &= ~addedHeaderFlags;
            addon->UpdateCollisionNodeList(false);
        }
        headerApplied = headerOriginal = [];
        headerClick = null;
        headerAddon = headerNode = 0;
    }

    private void BeforeFinalize(AddonEvent _, AddonArgs args)
    {
        DetachHeader((AddonPartyList*)args.Addon.Address);
        Array.Clear(castingRows);
        Array.Clear(castSpace);
        Array.Clear(indicators);
        Array.Clear(edits);
        Array.Clear(cache);
    }

    private void AfterUpdate(AddonEvent _, AddonArgs args)
    {
        if (config.PartyDamageBeforeRequestedUpdate) ApplyRows((AddonPartyList*)args.Addon.Address);
    }
}
