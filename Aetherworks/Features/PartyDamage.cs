using System.Text;
using Aetherworks.Tweaks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

public sealed unsafe class PartyDamage(IDalamudPluginInterface plugin, IAddonLifecycle lifecycle, IFramework framework,
    IGameGui gui, IObjectTable objects, IClientState client, Configuration config, IPluginLog log) : ITweak
{
    public const string FeatureId = "party-damage";
    private const string Addon = "_PartyList";
    private const int PlayerSlots = 8;
    private const int Slots = PlayerSlots + 7;
    private readonly PartyDamageFeed feed = new(plugin, log);
    private readonly string?[] labels = new string?[Slots];
    private readonly string[] roster = new string[Slots];
    private readonly byte[][] rosterBytes = new byte[Slots][];
    private readonly Edit?[] cache = new Edit?[Slots];
    private readonly Edit?[] edits = new Edit?[Slots];
    private sealed record Edit(nint Target, string Label, byte[] Original, byte[] Replacement);
    private IReadOnlyDictionary<string, long>? previousDamage;
    private bool enabled;
    private long nextPoll;
    private string? localName;
    public string Id => FeatureId;
    public string Name => "Party list damage";
    public string Description => "Replace party levels with encounter damage rank and party damage share from IINACT.";
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
        lifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, Addon, BeforeUpdate);
        lifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, Addon, AfterUpdate);
        nextPoll = 0;
    }

    public void Disable()
    {
        RestoreRows(CurrentAddon());
        enabled = false;
        framework.Update -= Update;
        client.TerritoryChanged -= TerritoryChanged;
        lifecycle.UnregisterListener(BeforeDraw);
        lifecycle.UnregisterListener(AfterDraw);
        lifecycle.UnregisterListener(BeforeUpdate);
        lifecycle.UnregisterListener(AfterUpdate);
        feed.Stop();
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
        if (enabled) RestoreRows(CurrentAddon());
        config.PartyDamageBeforeRequestedUpdate = beforeUpdate;
        if (enabled) RequestRefresh();
    }

    private void TerritoryChanged(uint territory)
    {
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
        var agent = AgentHUD.Instance();
        var player = objects.LocalPlayer?.Name.TextValue;
        var addon = CurrentAddon();
        var damage = feed.Damage;
        bool changed = damage != previousDamage || player != localName;
        localName = player;
        previousDamage = damage;
        for (int i = 0; i < Slots; i++)
        {
            var node = NameNode(addon, i);
            string name = "";
            if (player != null && node != null)
            {
                if (i < PlayerSlots && agent != null && i < agent->PartyMemberCount)
                    name = agent->PartyMembers[i].Name.ToString();
                else if (i >= PlayerSlots)
                    name = NpcName(node, i);
            }
            if (roster[i] == name) continue;
            changed = true;
            roster[i] = name;
            rosterBytes[i] = Encoding.UTF8.GetBytes(name);
        }
        if (!changed) return;
        PartyDamageRanks.Calculate(roster, PlayerSlots, player, damage, labels);
        if (config.PartyDamageBeforeRequestedUpdate) RequestRefresh();
    }

    // Keep any SeString payloads in the displayed name byte-for-byte intact.
    private Edit? Replace(int row, nint target, ReadOnlySpan<byte> text, string label)
    {
        if (text.IsEmpty) return null;
        if (cache[row] is { } cached && cached.Target == target && cached.Label == label &&
            text.SequenceEqual(cached.Original.AsSpan(0, cached.Original.Length - 1))) return cached;
        int start = 0;
        while (text.Length - start >= 3 && text[start] == 0xEE && text[start + 1] == 0x81 &&
               text[start + 2] is >= 0xA0 and <= 0xAF) start += 3; // U+E060..U+E06F
        if (start > 0)
            while (start < text.Length && text[start] == (byte)' ') start++;
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
        return cache[row] = new(target, label, [..text, 0], [..prefix.ToArray(), (byte)' ', ..text[start..], 0]);
    }

    private AddonPartyList* CurrentAddon() => (AddonPartyList*)gui.GetAddonByName(Addon).Address;

    private static AtkTextNode* NameNode(AddonPartyList* addon, int slot)
    {
        if (addon == null) return null;
        if (slot < PlayerSlots)
            return slot < addon->MemberCount ? addon->PartyMembers[slot].Name : null;
        int npc = slot - PlayerSlots;
        return npc < addon->TrustCount ? addon->TrustMembers[npc].Name : null;
    }

    private static bool IsApplied(AtkTextNode* node, Edit edit) => (nint)node == edit.Target &&
        node->NodeText.AsSpan().SequenceEqual(edit.Replacement.AsSpan(0, edit.Replacement.Length - 1));

    private string NpcName(AtkTextNode* node, int row)
    {
        var text = node->NodeText.AsSpan();
        if (edits[row] is { } edit && IsApplied(node, edit))
            text = edit.Original.AsSpan(0, edit.Original.Length - 1);
        string name = SeString.Parse(text).TextValue.Trim();
        int prefix = 0;
        while (prefix < name.Length && name[prefix] is >= '\uE060' and <= '\uE06F') prefix++;
        return name[prefix..].TrimStart();
    }

    private void ApplyRows(AddonPartyList* addon)
    {
        var agent = AgentHUD.Instance();
        for (int i = 0; i < Slots; i++)
        {
            var node = NameNode(addon, i);
            if (node == null || labels[i] is not { } label) continue;
            if (i < PlayerSlots && (agent == null || i >= agent->PartyMemberCount ||
                !agent->PartyMembers[i].Name.AsSpan().SequenceEqual(rosterBytes[i]))) continue;
            var text = node->NodeText.AsSpan();
            if (i >= PlayerSlots && !text.EndsWith(rosterBytes[i].AsSpan()) && NpcName(node, i) != roster[i]) continue;
            if (Replace(i, (nint)node, text, label) is not { } edit) continue;
            edits[i] = edit;
            node->SetText(edit.Replacement.AsSpan());
        }
    }

    private void RestoreRows(AddonPartyList* addon)
    {
        for (int i = 0; i < Slots; i++)
        {
            if (edits[i] is not { } edit) continue;
            edits[i] = null;
            var node = NameNode(addon, i);
            if (node != null && IsApplied(node, edit)) node->SetText(edit.Original.AsSpan());
        }
    }

    private void BeforeDraw(AddonEvent _, AddonArgs args)
    {
        if (!config.PartyDamageBeforeRequestedUpdate) ApplyRows((AddonPartyList*)args.Addon.Address);
    }

    private void AfterDraw(AddonEvent _, AddonArgs args)
    {
        if (!config.PartyDamageBeforeRequestedUpdate) RestoreRows((AddonPartyList*)args.Addon.Address);
    }

    private void BeforeUpdate(AddonEvent _, AddonArgs args)
    {
        if (config.PartyDamageBeforeRequestedUpdate) RestoreRows((AddonPartyList*)args.Addon.Address);
    }

    private void AfterUpdate(AddonEvent _, AddonArgs args)
    {
        if (config.PartyDamageBeforeRequestedUpdate) ApplyRows((AddonPartyList*)args.Addon.Address);
    }
}
