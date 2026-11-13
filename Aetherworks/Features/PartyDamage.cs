using System.Text;
using Aetherworks.Tweaks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

public sealed unsafe class PartyDamage(IDalamudPluginInterface plugin, IAddonLifecycle lifecycle, IFramework framework,
    IObjectTable objects, IClientState client, Configuration config, IPluginLog log) : ITweak
{
    public const string FeatureId = "party-damage";
    private const string Addon = "_PartyList";
    private const int Slots = 8;
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

    public void SetEntryPoint(bool beforeUpdate)
    {
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
        var count = agent == null || player == null ? 0 : Math.Clamp((int)agent->PartyMemberCount, 0, Slots);
        var damage = feed.Damage;
        bool changed = damage != previousDamage || player != localName;
        localName = player;
        previousDamage = damage;
        for (int i = 0; i < Slots; i++)
        {
            string name = i < count ? agent->PartyMembers[i].Name.ToString() : "";
            if (roster[i] == name) continue;
            changed = true;
            roster[i] = name;
            rosterBytes[i] = Encoding.UTF8.GetBytes(name);
        }
        if (!changed) return;

        var matched = new List<(int Row, long Damage)>();
        for (int i = 0; i < count; i++)
        {
            string name = roster[i];
            if (name.Length == 0 || roster.Count(n => n == name) != 1) continue;
            if (damage.TryGetValue(name, out long total) || (name == player && damage.TryGetValue("YOU", out total)))
                matched.Add((i, total));
        }
        Array.Clear(labels);
        decimal sum = matched.Sum(member => (decimal)member.Damage);
        if (sum > 0)
            foreach (var member in matched)
            {
                int rank = 1 + matched.Count(other => other.Damage > member.Damage);
                labels[member.Row] = $"{rank}:{100m * member.Damage / sum:0}% ";
            }
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
        return cache[row] = new(target, label, [..text, 0], [..Encoding.UTF8.GetBytes(label), ..text[start..], 0]);
    }

    private bool RowMatches(int i, AgentHUD* agent) => labels[i] != null && agent != null &&
        i < agent->PartyMemberCount && agent->PartyMembers[i].Name.AsSpan().SequenceEqual(rosterBytes[i]);

    private void BeforeDraw(AddonEvent _, AddonArgs args)
    {
        if (config.PartyDamageBeforeRequestedUpdate) return;
        var addon = (AddonPartyList*)args.Addon.Address;
        var agent = AgentHUD.Instance();
        for (int i = 0; i < Math.Min(Slots, (int)addon->MemberCount); i++)
        {
            var node = addon->PartyMembers[i].Name;
            if (node == null || !RowMatches(i, agent)) continue;
            if (Replace(i, (nint)node, node->NodeText.AsSpan(), labels[i]!) is not { } edit) continue;
            edits[i] = edit;
            node->SetText(edit.Replacement.AsSpan());
        }
    }

    private void AfterDraw(AddonEvent _, AddonArgs args)
    {
        if (config.PartyDamageBeforeRequestedUpdate) return;
        var addon = (AddonPartyList*)args.Addon.Address;
        for (int i = 0; i < Slots; i++)
        {
            if (edits[i] is not { } edit) continue;
            edits[i] = null;
            var node = addon->PartyMembers[i].Name;
            if ((nint)node == edit.Target && node->NodeText.AsSpan().SequenceEqual(edit.Replacement.AsSpan(0, edit.Replacement.Length-1)))
                node->SetText(edit.Original.AsSpan());
        }
    }

    private static StringArrayData* Names(AddonArgs args)
    {
        var arrays = (StringArrayData**)((AddonRequestedUpdateArgs)args).StringArrayData;
        return arrays == null ? null : arrays[(int)StringArrayType.PartyList];
    }

    private static int NameIndex(StringArrayData* names, int row)
    {
        var layout = (PartyListStringArray*)names->StringArray;
        fixed (InteropGenerator.Runtime.CStringPointer* name = &layout->Members[row].Name)
            return (int)(name - names->StringArray);
    }

    private void BeforeUpdate(AddonEvent _, AddonArgs args)
    {
        if (!config.PartyDamageBeforeRequestedUpdate) return;
        var names = Names(args);
        if (names == null || names->StringArray == null || names->Size < sizeof(PartyListStringArray)/sizeof(nint)) return;
        var agent = AgentHUD.Instance();
        for (int i = 0; i < Slots; i++)
        {
            if (!RowMatches(i, agent)) continue;
            int index = NameIndex(names,i);
            if (Replace(i, (nint)names, names->StringArray[index].AsSpan(), labels[i]!) is not { } edit) continue;
            edits[i] = edit;
            names->SetValue(index, edit.Replacement.AsSpan(), false, true, true);
        }
    }

    private void AfterUpdate(AddonEvent _, AddonArgs args)
    {
        if (!config.PartyDamageBeforeRequestedUpdate) return;
        var names = Names(args);
        for (int i = 0; i < Slots; i++)
        {
            if (edits[i] is not { } edit) continue;
            edits[i] = null;
            if (edit.Target != (nint)names) continue;
            int index = NameIndex(names,i);
            if (names->StringArray[index].AsSpan().SequenceEqual(edit.Replacement.AsSpan(0,edit.Replacement.Length-1)))
                names->SetValue(index, edit.Original.AsSpan(), false, true, true);
        }
    }
}
