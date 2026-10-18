using System.Numerics;
using Aetherworks.Crafting;
using Aetherworks.Ownership;
using Aetherworks.Tweaks;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Sheets = Lumina.Excel.Sheets;

namespace Aetherworks.Features;

public sealed class RecipeMaterialLocations(IDalamudPluginInterface pi, IGameGui gui, IDataManager data,
    OwnershipTracker tracker, IPluginLog log) : ITweak
{
    private bool enabled;
    private bool errorLogged;
    private (uint Recipe, OwnershipStore Store, long Revision, bool FcChest, ulong Company)? last;
    private IReadOnlyList<MaterialOwnership> materials = [];
    public string Id => "recipe-material-locations";
    public string Name => "Recipe Material Locations";
    public string Description => "Show recipe ingredients held in retainers or the saddlebag, even when inventory has enough. Hover for quantities and missing storage data.";
    public string Category => "Crafting";

    public void Enable()
    {
        if (enabled) return;
        pi.UiBuilder.Draw += Draw;
        enabled = true;
    }

    public void Disable()
    {
        pi.UiBuilder.Draw -= Draw;
        enabled = false;
        last = null;
        materials = [];
        errorLogged = false;
    }

    private unsafe void Draw()
    {
        if (!enabled || !tracker.HasCharacter) { last = null; return; }
        try
        {
            var addon = gui.GetAddonByName("RecipeNote");
            if (addon.IsNull || !addon.IsVisible) { last = null; return; }
            var note = RecipeNote.Instance();
            if (note == null || !note->IsRecipeListReady || note->RecipeList == null ||
                note->RecipeList->Recipes == null) { last = null; return; }
            var recipe = note->RecipeList->SelectedRecipe;
            if (recipe == null || recipe->RecipeId == 0) { last = null; return; }
            var key = ((uint)recipe->RecipeId, tracker.Store, tracker.Store.Revision, tracker.IncludeFcChest, tracker.CurrentFreeCompanyId);
            if (last != key)
            {
                materials = [];
                if (!data.GetExcelSheet<Sheets.Recipe>().TryGetRow(recipe->RecipeId, out var row)) return;
                var ingredients = new List<RecipeMaterial>();
                for (var i = 0; i < Math.Min(row.Ingredient.Count, row.AmountIngredient.Count); i++)
                    if (row.Ingredient[i].RowId != 0 && row.AmountIngredient[i] > 0)
                        ingredients.Add(new(row.Ingredient[i].RowId, row.Ingredient[i].Value.Name.ExtractText()));
                materials = RecipeMaterialOwnership.Find(ingredients, tracker.Store, tracker.IncludeFcChest, tracker.CurrentFreeCompanyId);
                last = key;
            }
            if (materials.Count == 0) return;
            var viewport = ImGui.GetMainViewport();
            var width = Math.Min(360 * ImGui.GetIO().FontGlobalScale, viewport.WorkSize.X);
            var height = Math.Min((materials.Count * 2 + 2) * ImGui.GetTextLineHeightWithSpacing() +
                ImGui.GetStyle().WindowPadding.Y * 2, viewport.WorkSize.Y * 0.6f);
            var position = viewport.Pos + new Vector2(addon.X + addon.Width * addon.Scale + 8, addon.Y);
            if (position.X + width > viewport.WorkPos.X + viewport.WorkSize.X)
                position.X = viewport.Pos.X + addon.X - width - 8;
            position = Vector2.Clamp(position, viewport.WorkPos,
                Vector2.Max(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize - new Vector2(width, height)));
            ImGui.SetNextWindowPos(position);
            ImGui.SetNextWindowSize(new(width, height));
            ImGui.SetNextWindowViewport(viewport.ID);
            var visible = ImGui.Begin("###AetherworksRecipeMaterials", ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav);
            try
            {
                if (!visible) return;
                ImGui.TextUnformatted("Materials in storage");
                foreach (var material in materials)
                {
                    ImGui.BeginGroup();
                    ImGui.TextWrapped(material.Name);
                    ImGui.TextUnformatted(material.Indicator);
                    ImGui.EndGroup();
                    if (!ImGui.IsItemHovered()) continue;
                    ImGui.BeginTooltip();
                    try
                    {
                        ImGui.TextUnformatted(material.Name);
                        foreach (var location in material.Locations)
                            ImGui.TextUnformatted($"{location.Label}: {location.Quantity?.ToString("N0") ?? "Missing"}");
                        ImGui.Separator();
                        ImGui.TextUnformatted($"{material.TotalLabel}: {material.Total:N0}");
                    }
                    finally { ImGui.EndTooltip(); }
                }
            }
            finally { ImGui.End(); }
        }
        catch (Exception ex)
        {
            last = null;
            if (!errorLogged) log.Error(ex, "Could not display recipe material ownership");
            errorLogged = true;
        }
    }
}
