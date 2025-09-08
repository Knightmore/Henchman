using System.Linq;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Henchman.Helpers;

internal static unsafe class ShopUtils
{
    internal static bool OpenShop(GameObject* vendor, uint shopId)
    {
        TargetSystem.Instance()->InteractWithObject(vendor, false);
        var selector = EventHandlerSelector.Instance();
        if (selector->Target == null)
            return true;

        if (selector->Target != vendor)
        {
            FullError($"Unexpected selector target {(ulong)selector->Target->GetGameObjectId():X} when trying to interact with {(ulong)vendor->GetGameObjectId():X}");
            return false;
        }

        for (var i = 0; i < selector->OptionsCount; ++i)
        {
            if (selector->Options[i].Handler->Info.EventId.Id == shopId)
            {
                Log($"Selecting selector option {i} for shop {shopId:X}");
                EventFramework.Instance()->InteractWithHandlerFromSelector(i);
                return true;
            }
        }

        FullError($"Failed to find shop {shopId:X} in selector for {(ulong)vendor->GetGameObjectId():X}");
        return false;
    }

    internal static bool OpenShop(uint dataId, uint shopId)
    {
        if (Svc.Targets.Target != null && Svc.Targets.Target.DataId == dataId)
            return OpenShop(TargetSystem.Instance()->Target, shopId);
        var vendor = Svc.Objects.Where(x => x.DataId == dataId && x.IsTargetable)
                        .OrderBy(x => Player.DistanceTo(x))
                        .FirstOrDefault();

        if (vendor == null)
        {
            FullError($"Failed to find vendor {dataId:X}");
            return false;
        }

        return OpenShop(vendor.Struct(), shopId);
    }

    internal static bool CloseShop()
    {
        var agent = AgentShop.Instance();
        if (agent == null || agent->EventReceiver == null)
            return false;
        AtkValue res   = default, arg = default;
        var      proxy = (ShopEventHandler.AgentProxy*)agent->EventReceiver;
        proxy->Handler->CancelInteraction();
        arg.SetInt(-1);
        agent->ReceiveEvent(&res, &arg, 1, 0);
        return true;
    }

    public static bool IsShopOpen(uint shopId = 0)
    {
        var agent = AgentShop.Instance();
        if (agent == null || !agent->IsAgentActive() || agent->EventReceiver == null || !agent->IsAddonReady())
            return false;
        if (shopId == 0)
            return true;
        if (!EventFramework.Instance()->EventHandlerModule.EventHandlerMap.TryGetValuePointer(shopId, out var eh) || eh == null || eh->Value == null)
            return false;
        var proxy = (ShopEventHandler.AgentProxy*)agent->EventReceiver;
        return proxy->Handler == eh->Value;
    }

    public static bool ShopTransactionInProgress(uint shopId)
    {
        if (!EventFramework.Instance()->EventHandlerModule.EventHandlerMap.TryGetValuePointer(shopId, out var eh) || eh == null || eh->Value == null)
        {
            FullError($"Event handler for shop {shopId:X} not found");
            return false;
        }

        if (eh->Value->Info.EventId.ContentId != EventHandlerContent.Shop)
        {
            FullError($"{shopId:X} is not a shop");
            return false;
        }

        var shop = (ShopEventHandler*)eh->Value;
        return shop->WaitingForTransactionToFinish;
    }

    internal static bool BuyItemFromShop(uint shopId, uint itemId, int count)
    {
        if (!EventFramework.Instance()->EventHandlerModule.EventHandlerMap.TryGetValuePointer(shopId, out var eh) || eh == null || eh->Value == null)
        {
            FullError($"Event handler for shop {shopId:X} not found");
            return false;
        }

        if (eh->Value->Info.EventId.ContentId != EventHandlerContent.Shop)
        {
            FullError($"{shopId:X} is not a shop");
            return false;
        }

        var shop = (ShopEventHandler*)eh->Value;
        for (var i = 0; i < shop->VisibleItemsCount; ++i)
        {
            var index = shop->VisibleItems[i];
            if (shop->Items[index].ItemId == itemId)
            {
                Log($"Buying {count}x {itemId} from {shopId:X}");
                shop->BuyItemIndex = index;
                shop->ExecuteBuy(count);
                return true;
            }
        }

        FullError($"Did not find item {itemId} in shop {shopId:X}");
        return false;
    }
}
