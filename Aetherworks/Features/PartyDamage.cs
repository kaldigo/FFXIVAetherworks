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
    private sealed record IndicatorEdit(nint Node, uint Entity, string Original, string Applied);
    private readonly Edit?[] castEdits = new Edit?[Slots];
    private readonly string[] roster = new string[Slots];
    private readonly uint[] entityIds = new uint[Slots];
    private readonly Edit?[] cache = new Edit?[Slots];
    private readonly Edit?[] edits = new Edit?[Slots];
    private sealed record Edit(nint Target, string Member, string Label, byte[] Original, byte[] Replacement, System.Numerics.Vector3 Color = default);
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
        Array.Clear(castEdits);
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
        if (cache[row] is { } cached && cached.Target == target && cached.Member == roster[row] && cached.Label == label &&
            text.SequenceEqual(cached.Original.AsSpan(0, cached.Original.Length - 1))) return cached;
        var (start, end) = LevelRange(text);
        if (start < 0) return null;
        return cache[row] = new(target, roster[row], label, [..text, 0],
            PartyLevelText.PrependPercentage(text, start, end, StyledShare(label).ToArray()));
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

    private void UpdateIndicator(AddonPartyList* addon, PartyListStringArray* strings, int row, uint id, int rank)
    {
        var node = NameNode(addon, row, indicator: true);
        var saved = indicators[row];
        if (node == null) { indicators[row] = null; return; }
        string current = node->NodeText.ToString();
        bool ours = saved?.Node == (nint)node && current == saved.Applied;
        string original = ours ? saved!.Original : current;
        var source = strings->Members[row].GroupSlotIndicator;
        if (!source.AsSpan().IsEmpty) original = source.ToString();
        if (rank == 0)
        {
            if (ours && current != original) node->SetText(original);
            indicators[row] = null;
            return;
        }
        if (original.Length != 1) return;
        char glyph = original[0];
        bool self = glyph is >= '\uE0E0' and <= '\uE0E9';
        if (!self && (glyph < '\uE08F' || glyph > '\uE0A2')) return;
        string icon = ((char)((self && rank <= 9 ? 0xE0E0 : 0xE08F) + rank)).ToString();
        if (saved?.Entity != id || saved.Node != (nint)node || saved.Original != original || saved.Applied != icon)
            indicators[row] = new((nint)node, id, original, icon);
        if (current != icon) node->SetText(icon);
    }

    private static AtkTextNode* CastNode(AddonPartyList* addon, int row) => row < PlayerSlots
        ? addon->PartyMembers[row].CastingActionName : addon->TrustMembers[row - PlayerSlots].CastingActionName;

    private void UpdateCastText(int row, AtkTextNode* cast, ReadOnlySpan<byte> original, string? label)
    {
        var previous = castEdits[row];
        if (cast == null || label == null || original.IsEmpty)
        {
            if (cast != null && previous != null && IsApplied(cast, previous))
                cast->SetText(original.IsEmpty ? previous.Original : (ReadOnlySpan<byte>)[..original, 0]);
            castEdits[row] = null;
            return;
        }
        if (previous == null || previous.Target != (nint)cast || previous.Member != roster[row] ||
            previous.Label != label || !original.SequenceEqual(previous.Original.AsSpan(0, previous.Original.Length - 1)) ||
            previous.Color != config.PartyDamageColor)
        {
            previous = castEdits[row] = new((nint)cast, roster[row], label, [..original, 0],
                PartyLevelText.PrependPercentage(original, 0, 0, StyledShare(label).ToArray()), config.PartyDamageColor);
        }
        if (!IsApplied(cast, previous)) cast->SetText(previous.Replacement.AsSpan());
    }

    private void ApplyRows(AddonPartyList* addon, bool transitionsOnly = false)
    {
        var numbers = PartyListNumberArray.Instance();
        var strings = PartyListStringArray.Instance();
        if (numbers == null || strings == null) return;
        for (int i = 0; i < Slots; i++)
        {
            var node = NameNode(addon, i);
            var cast = node == null ? null : CastNode(addon, i);
            bool casting = cast != null && cast->IsVisible() && cast->Color.A != 0 &&
                (!node->IsVisible() || node->Color.A == 0);
            if (transitionsOnly && castingRows[i] == casting) continue;
            castingRows[i] = casting;
            var previous = edits[i];
            uint id = RowId(numbers, i);
            var original = strings->Members[i].Name.AsSpan();
            var label = id != 0 && id == entityIds[i] ? labels[i] : null;
            UpdateIndicator(addon, strings, i, id, label != null ? ranks[i] : 0);
            UpdateCastText(i, cast, strings->Members[i].CastingActionName.AsSpan(), casting ? label : null);
            if (node == null) { edits[i] = null; continue; }
            if (original.IsEmpty) continue;
            if (label == null)
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
            if (strings != null)
            {
                UpdateIndicator(addon, strings, i, 0, 0);
                UpdateCastText(i, NameNode(addon, i) == null ? null : CastNode(addon, i), strings->Members[i].CastingActionName.AsSpan(), null);
            }
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
        Array.Clear(castEdits);
        Array.Clear(indicators);
        Array.Clear(edits);
        Array.Clear(cache);
    }

    private void AfterUpdate(AddonEvent _, AddonArgs args)
    {
        if (config.PartyDamageBeforeRequestedUpdate) ApplyRows((AddonPartyList*)args.Addon.Address);
    }
}
