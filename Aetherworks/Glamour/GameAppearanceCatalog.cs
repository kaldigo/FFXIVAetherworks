using Dalamud.Plugin.Services;
using Sheets = Lumina.Excel.Sheets;

namespace Aetherworks.Glamour;

public static class GameAppearanceCatalog
{
    public static AppearanceCatalog Create(IDataManager data)
    {
        var cabinet = data.GetExcelSheet<Sheets.Cabinet>().Select(r => r.Item.RowId).ToHashSet();
        return new AppearanceCatalog(data.GetExcelSheet<Sheets.Item>()
            .Where(i => i.EquipSlotCategory.RowId != 0 && i.ModelMain != 0)
            .Select(i => new Appearance(i.RowId, i.Name.ExtractText(), i.EquipSlotCategory.RowId,
                i.EquipSlotCategory.Value.MainHand != 0 || i.EquipSlotCategory.Value.OffHand != 0 ? i.ItemUICategory.RowId : 0,
                i.ModelMain, i.ModelSub, i.DyeCount, cabinet.Contains(i.RowId))));
    }
}
