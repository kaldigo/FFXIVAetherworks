using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using Aetherworks.Tweaks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;


using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

public sealed unsafe partial class PartyDamage(IDalamudPluginInterface plugin, IAddonLifecycle lifecycle, IFramework framework,
    IGameGui gui, IObjectTable objects, IClientState client, Configuration config, IPluginLog log) : ITweak
{
    public const string FeatureId = "party-damage";
    private const string Addon = "_PartyList";
    private const int PlayerSlots = 8;
    private const int Slots = PlayerSlots + 7;
    private readonly PartyShieldBar shields = new(lifecycle, gui);
    private readonly PartyDamageFeed feed = new(plugin, log);
    private readonly string?[] labels = new string?[Slots];
    private readonly int[] ranks = new int[Slots];
    private readonly IndicatorEdit?[] indicators = new IndicatorEdit?[Slots];
    private sealed record IndicatorEdit(nint Node, uint Entity, string Original, string Applied);
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
        feed.Start();
        framework.Update += Update;
        client.TerritoryChanged += TerritoryChanged;
        lifecycle.RegisterListener(AddonEvent.PreDraw, Addon, BeforeDraw);
        lifecycle.RegisterListener(AddonEvent.PostDraw, Addon, AfterDraw);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, Addon, BeforeFinalize);
        if (config.ShieldsOverHp) shields.Enable();
        nextPoll = 0;
    }

    public void Disable()
    {
        if (config.ShieldsOverHp) shields.Disable();
        RestoreRows(CurrentAddon());
        enabled = false;
        framework.Update -= Update;
        client.TerritoryChanged -= TerritoryChanged;
        lifecycle.UnregisterListener(BeforeDraw);
        lifecycle.UnregisterListener(AfterDraw);
        lifecycle.UnregisterListener(BeforeFinalize);
        feed.Stop();
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
    }

    public void SetShieldsOverHp(bool value)
    {
        if (config.ShieldsOverHp == value) return;
        config.ShieldsOverHp = value;
        if (!enabled) return;
        if (value) shields.Enable();
        else shields.Disable();
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
    }

    // Keep any SeString payloads in the displayed name byte-for-byte intact.
    private Edit? Replace(int row, nint target, ReadOnlySpan<byte> text, string label)
    {
        if (text.IsEmpty) return null;
        if (cache[row] is { } cached && cached.Target == target && cached.Member == roster[row] && cached.Label == label &&
            text.SequenceEqual(cached.Original.AsSpan(0, cached.Original.Length - 1))) return cached;
        var (start, end) = LevelRange(text);
        if (start < 0) return null;
        return cache[row] = new(target, roster[row], label, [..text, 0],
            PartyLevelText.PrependPercentage(text, start, end, PercentageText(label)));
    }

    private byte[] PercentageText(string label)
    {
        var rgb = config.PartyDamageColor;
        var prefix = new Lumina.Text.SeStringBuilder()
            .PushColorRgba(new System.Numerics.Vector4(rgb, 1f));
        foreach (char ch in label.AsSpan().TrimEnd())
        {
            // The level field uses the game's numeral icons, including their heavier strokes.
            prefix.Append(char.IsAsciiDigit(ch)
                ? (char)((int)Dalamud.Game.Text.SeIconChar.Number0 + ch - '0')
                : ch);
        }
        prefix.PopColor();
        return prefix.ToArray();
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

    private void ApplyRows(AddonPartyList* addon)
    {
        var numbers = PartyListNumberArray.Instance();
        var strings = PartyListStringArray.Instance();
        if (numbers == null || strings == null) return;
        for (int i = 0; i < Slots; i++)
        {
            var node = NameNode(addon, i);
            var previous = edits[i];
            uint id = RowId(numbers, i);
            UpdateIndicator(addon, strings, i, id, id != 0 && id == entityIds[i] && labels[i] != null ? ranks[i] : 0);
            if (node == null) { edits[i] = null; continue; }
            var original = strings->Members[i].Name.AsSpan();
            if (castShares[i] is { } casting && casting.Node == (nint)node && casting.Entity == id) continue;
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
        RestoreCastVisibility(addon);
        var strings = PartyListStringArray.Instance();
        for (int i = 0; i < Slots; i++)
        {
            var previous = edits[i];
            edits[i] = null;
            if (strings != null) UpdateIndicator(addon, strings, i, 0, 0);
            var node = NameNode(addon, i);
            var casting = castShares[i];
            castShares[i] = null;
            if (node == null || strings == null || (previous?.Target != (nint)node && casting?.Node != (nint)node)) continue;
            var original = strings->Members[i].Name.AsSpan();
            node->SetText((ReadOnlySpan<byte>)[..original, 0]);
        }
    }

    private void BeforeDraw(AddonEvent _, AddonArgs args)
    {
        var addon = (AddonPartyList*)args.Addon.Address;
        ApplyRows(addon);
        DrawCastShares(addon);
    }

    private void AfterDraw(AddonEvent _, AddonArgs args) => RestoreCastVisibility((AddonPartyList*)args.Addon.Address);

    private void BeforeFinalize(AddonEvent _, AddonArgs args)
    {
        RestoreCastVisibility((AddonPartyList*)args.Addon.Address);
        Array.Clear(castShares);
        Array.Clear(indicators);
        Array.Clear(edits);
        Array.Clear(cache);
    }
}
