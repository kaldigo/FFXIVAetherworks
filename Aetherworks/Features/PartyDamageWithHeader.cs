using Aetherworks.Tweaks;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Features;

// Keep Ember's title control independent of the party-member row implementation.
internal sealed unsafe class PartyDamageWithHeader(PartyDamage rows, IAddonLifecycle lifecycle,
    IAddonEventManager events, IGameGui gui, Configuration config, Action toggleEmber, Action<bool> enableEmber) : ITweak
{
    private const string Addon = "_PartyList";
    private bool enabled;
    private IAddonEventHandle? headerClick;
    private nint headerAddon;
    private nint headerNode;
    private byte[] headerOriginal = [];
    private byte[] headerApplied = [];
    private bool headerOpen;
    private NodeFlags addedHeaderFlags;

    public string Id => rows.Id;
    public string Name => rows.Name;
    public string Category => rows.Category;
    public string Description => rows.Description;

    public void Enable()
    {
        rows.Enable();
        enabled = true;
        enableEmber(true);
        lifecycle.RegisterListener(AddonEvent.PreDraw, Addon, BeforeDraw);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, Addon, BeforeFinalize);
    }

    public void Disable()
    {
        enabled = false;
        lifecycle.UnregisterListener(BeforeDraw);
        lifecycle.UnregisterListener(BeforeFinalize);
        var addon = (AddonPartyList*)gui.GetAddonByName(Addon).Address;
        DetachHeader(addon);
        enableEmber(false);
        rows.Disable();
    }

    private void BeforeDraw(AddonEvent _, AddonArgs args)
    {
        var addon = (AddonPartyList*)args.Addon.Address;
        AttachHeader(addon);
        if (headerClick != null) DrawHeaderIndicator(addon->PartyTypeTextNode);
    }

    private void BeforeFinalize(AddonEvent _, AddonArgs args)
    {
        DetachHeader((AddonPartyList*)args.Addon.Address);
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

}
