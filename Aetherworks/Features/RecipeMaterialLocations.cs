using Aetherworks.Crafting;
using Aetherworks.Ownership;
using Aetherworks.Tweaks;
using Aetherworks.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Sheets = Lumina.Excel.Sheets;

namespace Aetherworks.Features;

public sealed unsafe class RecipeMaterialLocations(IAddonLifecycle lifecycle, IGameGui gui, IDataManager data,
    OwnershipTracker tracker, OwnershipTooltipPanel tooltip, IPluginLog log) : ITweak
{
    private bool enabled, errorLogged;
    private nint modifiedAddon;
    private readonly Dictionary<nint, (byte[] Original, byte[] Applied)> edits = new();
    public string Id => "recipe-material-locations";
    public string Name => "Recipe Material Locations";
    public string Description => "Mark ingredients held elsewhere in the crafting window, even when inventory has enough. Hover the material for locations and totals.";
    public string Category => "Crafting";

    public void Enable()
    {
        if (enabled) return;
        tooltip.ShowRecipe = true;
        lifecycle.RegisterListener(AddonEvent.PreDraw, "RecipeNote", Draw);
        lifecycle.RegisterListener(AddonEvent.PreRefresh, "RecipeNote", Reset);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, "RecipeNote", Reset);
        enabled = true;
    }
    public void Disable()
    {
        enabled = false;
        lifecycle.UnregisterListener(Draw);
        lifecycle.UnregisterListener(Reset);
        var addon = gui.GetAddonByName("RecipeNote");
        if (!addon.IsNull && addon.Address == modifiedAddon) Restore();
        edits.Clear(); modifiedAddon = 0;
        tooltip.RecipeMaterials.Clear(); tooltip.ShowRecipe = false;
        errorLogged = false;
    }
    private void Restore()
    {
        foreach (var (address, edit) in edits)
        {
            var node = (AtkTextNode*)address;
            if (node->NodeText.AsSpan().SequenceEqual(edit.Applied.AsSpan(0, edit.Applied.Length - 1)))
                node->SetText(edit.Original);
        }
        edits.Clear();
    }
    private void Reset(AddonEvent _, AddonArgs args)
    {
        if (modifiedAddon == args.Addon.Address) Restore();
        edits.Clear(); modifiedAddon = 0; tooltip.RecipeMaterials.Clear();
    }
    private void Mark(AtkTextNode* node, string prefix)
    {
        if (node == null) return;
        var original = node->NodeText.AsSpan().ToArray().Append((byte)0).ToArray();
        var applied = System.Text.Encoding.UTF8.GetBytes(prefix).Concat(original).ToArray();
        node->SetText(applied);
        edits[(nint)node] = (original, applied);
    }
    private void Draw(AddonEvent _, AddonArgs args)
    {
        if (!enabled) return;
        try
        {
            if (modifiedAddon == args.Addon.Address) Restore(); else edits.Clear();
            modifiedAddon = args.Addon.Address;
            tooltip.RecipeMaterials.Clear();
            if (!tracker.HasCharacter) return;
            var note = RecipeNote.Instance();
            if (note == null || !note->IsRecipeListReady || note->RecipeList == null || note->RecipeList->Recipes == null) return;
            var selected = note->RecipeList->SelectedRecipe;
            if (selected == null || !data.GetExcelSheet<Sheets.Recipe>().TryGetRow(selected->RecipeId, out var row)) return;
            var addon = (AddonRecipeNote*)args.Addon.Address;
            for (var i = 0; i < Math.Min(row.Ingredient.Count, row.AmountIngredient.Count); i++)
            {
                var id = row.Ingredient[i].RowId;
                if (id == 0 || row.AmountIngredient[i] == 0) continue;
                var material = RecipeMaterialOwnership.Find([new RecipeMaterial(id, row.Ingredient[i].Value.Name.ExtractText())],
                    tracker.Store, tracker.IncludeFcChest, tracker.CurrentFreeCompanyId).FirstOrDefault();
                if (material == null) continue;
                tooltip.RecipeMaterials[id] = material;
                // Recipe sheet slots 0–5 are materials; slots 6–7 are crystals.
                // Prefix a compact marker, preserving the game's actual inventory counts.
                var marker = material.OutsideQuantity > 0 ? "+ " : "? ";
                if (i < addon->Ingredients.Length) Mark(addon->Ingredients[i].Name, marker);
                else if (i - 6 < addon->Crystals.Length) Mark(addon->Crystals[i - 6].QuantityInInventory, marker);
            }
        }
        catch (Exception ex)
        {
            if (!errorLogged) log.Error(ex, "Could not mark crafting ingredients");
            errorLogged = true;
        }
    }
}
