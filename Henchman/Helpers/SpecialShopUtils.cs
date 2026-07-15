using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace Henchman.Helpers;

internal static unsafe class SpecialShopUtils
{
    internal static bool BuyItemFromSpecialShop(uint shopId, uint itemId, int count)
    {
        if (!EventFramework.Instance()->EventHandlerModule.EventHandlerMap.TryGetValuePointer(shopId, out var eh) || eh == null || eh->Value == null)
        {
            FullError($"Event handler for shop {shopId:X} not found");
            return false;
        }

        if (eh->Value->Info.EventId.ContentId != EventHandlerContent.SpecialShop)
        {
            FullError($"{shopId:X} is not a special shop");
            return false;
        }

        /*var shop = (SpecialShopEventHandler*)eh->Value;
        for (uint i = 0; i < shop->ItemCount; ++i)
        {
            if (shop->Items[i].ItemReceive.Contains(itemId))
            {
                TaskLog.Info($"Buying {count}x {itemId} from {shopId:X}");
                shop->BuyItemIndex = i;
                shop->BuyItemAmount = count;
                shop->ExecuteBuy(i, count);
                return true;
            }
        }*/

        FullError($"Did not find item {itemId} in shop {shopId:X}");
        return false;
    }
}
