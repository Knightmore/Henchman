using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Henchman.Helpers;

internal static class InventoryHelper
{
    private static readonly InventoryType[] MainInventory =
    [
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4
    ];

    internal static unsafe InventoryItem* GetItemInInventory(uint itemId)
    {
        InventoryContainer* container;
        var inventoryManager = InventoryManager.Instance();
        foreach (var inventory in MainInventory)
        {
            container = inventoryManager->GetInventoryContainer(inventory);
            for (var i = 0; i < container->Size; i++)
            {
                var item = container->GetInventorySlot(i);
                if (item->ItemId == itemId)
                    return item;
            }
        }

        container = inventoryManager->GetInventoryContainer(InventoryType.KeyItems);
        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item->ItemId == itemId)
                return item;
        }

        return null;
    }

    internal static unsafe void Discard(InventoryItem* item)
    {
        AgentInventoryContext.Instance()->DiscardItem(item, item->Container, item->Slot, 0);
    }

    internal static unsafe bool Discard(uint itemId)
    {
        var item = GetItemInInventory(itemId);
        if (item == null) return false;
        Discard(item);
        return true;
    }

    internal static unsafe int GetInventoryItemCount(uint itemId)
    {
        return InventoryManager.Instance()->GetInventoryItemCount(itemId);
    }
}
