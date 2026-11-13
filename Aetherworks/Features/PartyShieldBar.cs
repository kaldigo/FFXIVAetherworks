using Aetherworks.Tweaks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

internal sealed unsafe class PartyShieldBar(IAddonLifecycle lifecycle, IGameGui gui) : ITweak
{
    internal const string FeatureId = "party-shield-bar";
    private const string Addon = "_PartyList";
    private struct Placement
    {
        public nint Node;
        public float Original;
        public float Applied;
    }

    // Eight party slots, eight Trust slots and the companion/pet variants.
    private readonly Placement[,] placements = new Placement[19, 4];
    public string Id => FeatureId;
    public string Name => "Shield over party HP";
    public string Category => "Party list";
    public string Description => "Draw the overflow shield on the HP bar in party-list rows.";

    public void Enable()
    {
        lifecycle.RegisterListener(AddonEvent.PreDraw, Addon, BeforeDraw);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, Addon, BeforeFinalize);
    }

    public void Disable()
    {
        lifecycle.UnregisterListener(BeforeDraw);
        lifecycle.UnregisterListener(BeforeFinalize);
        Update((AddonPartyList*)gui.GetAddonByName(Addon).Address, true);
        Array.Clear(placements);
    }

    private void BeforeDraw(AddonEvent _, AddonArgs args) => Update((AddonPartyList*)args.Addon.Address, false);
    private void BeforeFinalize(AddonEvent _, AddonArgs args) => Array.Clear(placements);

    private void Update(AddonPartyList* addon, bool restore)
    {
        if (addon == null) return;
        for (int i = 0; i < 8; i++)
        {
            Align(i, addon->PartyMembers[i].HPGaugeBar, restore);
            Align(i + 8, addon->TrustMembers[i].HPGaugeBar, restore);
        }
        Align(16, addon->Chocobo.HPGaugeBar, restore);
        Align(17, addon->Pet.HPGaugeBar, restore);
        Align(18, addon->SpecialPet.HPGaugeBar, restore);
    }

    private void Align(int row, AtkComponentGaugeBar* gauge, bool restore)
    {
        if (gauge == null) return;
        var hp = gauge->PrimaryFill.MainFillNode;
        var overflow = gauge->SecondaryOverflow;
        var bar = overflow.MainFillNode;
        if (bar == null) return;

        ref var saved = ref placements[row, 0];
        float baseY = OriginalY((AtkResNode*)bar, saved);
        bool align = !restore && hp != null && hp->ParentNode == bar->ParentNode;
        float shift = align ? hp->Y + (hp->Height - bar->Height) / 2f - baseY : 0;
        Place(row, 0, (AtkResNode*)bar, shift);
        PlaceSibling(row, 1, (AtkResNode*)overflow.IncreaseFillNode, bar->ParentNode, shift);
        PlaceSibling(row, 2, (AtkResNode*)overflow.DecreaseFillNode, bar->ParentNode, shift);
        PlaceSibling(row, 3, (AtkResNode*)gauge->SecondaryOverflowMaxIcon, bar->ParentNode, shift);
    }

    private void PlaceSibling(int row, int layer, AtkResNode* node, AtkResNode* parent, float shift)
    {
        if (node != null) Place(row, layer, node, node->ParentNode == parent ? shift : 0);
    }

    private static float OriginalY(AtkResNode* node, Placement saved) =>
        saved.Node == (nint)node && node->Y == saved.Applied ? saved.Original : node->Y;

    private void Place(int row, int layer, AtkResNode* node, float shift)
    {
        ref var saved = ref placements[row, layer];
        float original = OriginalY(node, saved);
        float target = original + shift;
        if (node->Y != target) node->SetYFloat(target);
        saved = shift == 0 ? default : new Placement { Node = (nint)node, Original = original, Applied = target };
    }
}
