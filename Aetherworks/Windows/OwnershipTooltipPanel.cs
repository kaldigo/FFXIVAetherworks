using Aetherworks.Glamour;
using Aetherworks.Ownership;
using Aetherworks.Crafting;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherworks.Windows;

// Uses the same pre-layout string-array extension point as Allagan Tools/CriticalCommonLib.
public sealed unsafe class OwnershipTooltipPanel(IGameGui gui, OwnershipTracker tracker,
    AppearanceCatalog catalog, IGameInteropProvider interop, ISigScanner scanner, IPluginLog log) : IDisposable
{
    private delegate void* Generate(AtkUnitBase* addon, NumberArrayData* numbers, StringArrayData* strings);
    private Hook<Generate>? hook;
    private bool showItems, showGlamour, showRecipe;
    private bool errorLogged;
    public Dictionary<uint, MaterialOwnership> RecipeMaterials { get; } = new();
    public bool ShowItems { get => showItems; set { showItems = value; Sync(); } }
    public bool ShowGlamour { get => showGlamour; set { showGlamour = value; Sync(); } }
    public bool ShowRecipe { get => showRecipe; set { showRecipe = value; Sync(); } }

    private void Sync()
    {
        if (showItems || showGlamour || showRecipe)
        {
            if (hook != null) return;
            var address = scanner.ScanText("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 8B 42 ?? 4C 8B EA");
            hook = interop.HookFromAddress<Generate>(address, OnGenerate);
            hook.Enable();
        }
        else { hook?.Dispose(); hook = null; }
    }

    private void* OnGenerate(AtkUnitBase* addon, NumberArrayData* numbers, StringArrayData* strings)
    {
        byte[]? original = null;
        var field = -1;
        try
        {
            if (tracker.HasCharacter && gui.HoveredItem is > 0 and < 2_000_000 && numbers != null &&
                strings != null && numbers->Size > 5)
            {
                var id = ItemUtil.GetBaseId((uint)gui.HoveredItem).ItemId;
                var lines = OwnershipTooltip.Compose(id, tracker.Store, catalog, tracker.LocationsFor(id), showItems, showGlamour).ToList();
                var recipe = gui.GetAddonByName("RecipeNote");
                if (showRecipe && !recipe.IsNull && recipe.IsVisible && RecipeMaterials.TryGetValue(id, out var material))
                {
                    lines.Add("Crafting materials: " + material.Indicator);
                    foreach (var location in material.Locations)
                    {
                        var line = $"{location.Label}: {location.Quantity?.ToString("N0") ?? "Missing"}";
                        if (!lines.Contains(line)) lines.Add(line);
                    }
                    lines.Add($"{material.TotalLabel}: {material.Total:N0}");
                }
                if (lines.Count > 0)
                {
                    var flags = numbers->IntArray[5];
                    field = (flags & 2) != 0 ? 13 : (flags & 256) != 0 ? 16 : (flags & 32768) != 0 ? 23 : -1;
                    if (field >= 0 && field < strings->Size && strings->StringArray[field].Value != null)
                    {
                        var text = MemoryHelper.ReadSeStringNullTerminated((nint)strings->StringArray[field].Value);
                        original = text.Encode().Append((byte)0).ToArray();
                        var extended = new SeStringBuilder().Append(text).AddText("\n\n" + string.Join("\n", lines)).Build().Encode().Append((byte)0).ToArray();
                        strings->SetValue(field, extended, false, true, true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (!errorLogged) log.Error(ex, "Could not extend native item tooltip");
            errorLogged = true;
        }
        try { return hook!.Original(addon, numbers, strings); }
        finally
        {
            // Keep shared input strings untouched after layout: no repeated suffixes,
            // and other tooltip plugins retain their own content.
            if (original != null) strings->SetValue(field, original, false, true, true);
        }
    }

    public void Dispose() { hook?.Dispose(); hook = null; RecipeMaterials.Clear(); }
}
