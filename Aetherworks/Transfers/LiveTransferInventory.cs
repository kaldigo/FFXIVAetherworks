using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Sheets = Lumina.Excel.Sheets;

namespace Aetherworks.Transfers;

public sealed class LiveTransferInventory(IDataManager data, bool requireTradable = false)
{
    private readonly Dictionary<uint, (int Size, bool Allowed)> itemData = [];

    public unsafe TransferView? Read(params InventoryType[] destinations)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return null;
        TransferSlot[]? ReadSlots(InventoryType[] types)
        {
            var slots = new List<TransferSlot>();
            foreach (var type in types)
            {
                var container = manager->GetInventoryContainer(type);
                if (container == null || !container->IsLoaded || container->Size <= 0 || container->Size > 140) return null;
                for (ushort index = 0; index < container->Size; index++)
                {
                    var item = container->GetInventorySlot(index);
                    if (item == null || item->IsSymbolic || item->Quantity < 0) return null;
                    var size = 0;
                    if (item->ItemId != 0)
                    {
                        if (!itemData.TryGetValue(item->ItemId, out var metadata))
                        {
                            if (!data.GetExcelSheet<Sheets.Item>().TryGetRow(item->ItemId, out var row)) return null;
                            metadata = (checked((int)row.StackSize), !row.IsUnique && (!requireTradable || !row.IsUntradable));
                            itemData[item->ItemId] = metadata;
                        }
                        if (item->Quantity == 0 || item->Quantity > metadata.Size) return null;
                        size = metadata.Allowed && (!requireTradable || item->SpiritbondOrCollectability == 0) ? metadata.Size : 1;
                    }
                    else if (item->Quantity != 0) return null;
                    slots.Add(new((uint)type, index, item->ItemId == 0 ? default : new(item->ItemId, (byte)item->Flags), item->Quantity, size));
                }
            }
            return slots.ToArray();
        }
        var inventory = ReadSlots([InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4]);
        var destination = ReadSlots(destinations);
        return inventory is null || destination is null ? null : new(inventory, destination);
    }
}
