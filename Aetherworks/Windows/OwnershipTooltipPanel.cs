using System.Numerics;
using Aetherworks.Glamour;
using Aetherworks.Ownership;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

namespace Aetherworks.Windows;

public sealed class OwnershipTooltipPanel(IGameGui gui, OwnershipTracker tracker, AppearanceCatalog catalog)
{
    public bool ShowItems { get; set; }
    public bool ShowGlamour { get; set; }
    private (uint Item, OwnershipStore Store, long Revision, bool Items, bool Glamour, bool FcChest, ulong Company)? last;
    private IReadOnlyList<string> lines = [];

    public void Draw()
    {
        if ((!ShowItems && !ShowGlamour) || !tracker.HasCharacter || gui.HoveredItem is 0 or >= 2_000_000) return;
        var addon = gui.GetAddonByName("ItemDetail");
        if (addon.IsNull || !addon.IsVisible) return;
        var id = ItemUtil.GetBaseId((uint)gui.HoveredItem).ItemId;
        var key = (id, tracker.Store, tracker.Store.Revision, ShowItems, ShowGlamour, tracker.IncludeFcChest, tracker.CurrentFreeCompanyId);
        if (last != key)
        {
            lines = OwnershipTooltip.Compose(id, tracker.Store, catalog, tracker.LocationsFor(id), ShowItems, ShowGlamour);
            last = key;
        }
        if (lines.Count == 0) return;
        var viewport = ImGui.GetMainViewport();
        var padding = ImGui.GetStyle().WindowPadding;
        var width = Math.Min(lines.Max(l => ImGui.CalcTextSize(l).X) + padding.X * 2, viewport.WorkSize.X * 0.45f);
        width = Math.Max(1, width);
        var height = Math.Min(lines.Sum(l => ImGui.CalcTextSize(l, false, Math.Max(1, width - padding.X * 2)).Y + ImGui.GetStyle().ItemSpacing.Y)
            + padding.Y * 2, viewport.WorkSize.Y);
        var position = viewport.Pos + new Vector2(addon.X + addon.Width * addon.Scale + 8, addon.Y);
        if (position.X + width > viewport.WorkPos.X + viewport.WorkSize.X)
            position.X = viewport.Pos.X + addon.X - width - 8;
        position = Vector2.Clamp(position, viewport.WorkPos,
            Vector2.Max(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize - new Vector2(width, height)));
        ImGui.SetNextWindowPos(position);
        ImGui.SetNextWindowSize(new Vector2(width, height));
        ImGui.SetNextWindowViewport(viewport.ID);
        if (ImGui.Begin("###AetherworksItemLocations", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing))
        {
            foreach (var line in lines) ImGui.TextWrapped(line);
        }
        ImGui.End();
    }
}
