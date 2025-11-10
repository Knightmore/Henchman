using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Henchman.Helpers;

internal static class InventoryHelper
{
    internal static readonly InventoryType[] MainInventory =
    [
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4
    ];

    internal static unsafe InventoryItem* GetItemInInventoryPtr(uint itemId)
    {
        InventoryContainer* container;
        var                 inventoryManager = InventoryManager.Instance();
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

    internal static unsafe List<InventoryItem>?  GetItemsInInventory(uint itemId)
    {
        var                 itemList = new List<InventoryItem>();
        InventoryContainer* container;
        var                 inventoryManager = InventoryManager.Instance();
        foreach (var inventory in MainInventory)
        {
            container = inventoryManager->GetInventoryContainer(inventory);
            for (var i = 0; i < container->Size; i++)
            {
                var item = container->GetInventorySlot(i);
                if (item->ItemId == itemId)
                    itemList.Add(*item);
            }
        }

        container = inventoryManager->GetInventoryContainer(InventoryType.KeyItems);
        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item->ItemId == itemId)
                itemList.Add(*item);
        }

        return null;
    }

    public static unsafe InventoryItem GetItemSlot(InventoryType type, int slot)
    {
        return *InventoryManager.Instance()->GetInventoryContainer(type)->GetInventorySlot(slot);
    }

    internal static unsafe void Discard(InventoryItem* item)
    {
        AgentInventoryContext.Instance()->DiscardItem(item, item->Container, item->Slot, 0);
    }

    internal static unsafe bool Discard(uint itemId)
    {
        var item = GetItemInInventoryPtr(itemId);
        if (item == null) return false;
        Discard(item);
        return true;
    }

    public static unsafe uint GetItemAmountInNeedOfRepair(int durability = 0)
    {
        var amount    = 0u;
        var  container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item is null) continue;
            if (Convert.ToInt32(Convert.ToDouble(item->Condition) / 30000.0 * 100.0) <= durability)
                amount++;
        }
        return amount;
    }

    internal static unsafe int GetInventoryItemCount(uint itemId) => InventoryManager.Instance()->GetInventoryItemCount(itemId);

    internal static unsafe uint GetGCSealAmount() => Player.GrandCompany == 0
                                                             ? 0
                                                             : InventoryManager.Instance()->GetCompanySeals((byte)Player.GrandCompany);

    internal static unsafe void SetTradeGilAmount(uint amount) => InventoryManager.Instance()->SetTradeGilAmount(amount);
    internal static unsafe void SendTradeRequest(uint entityId) => InventoryManager.Instance()->SendTradeRequest(entityId);
    internal static unsafe void RefuseTrade() => InventoryManager.Instance()->RefuseTrade();
}
