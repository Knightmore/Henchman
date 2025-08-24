using Dalamud.Game.ClientState.Conditions;
using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Henchman.Helpers;

internal static class Memory
{
    public delegate nint ExecuteCommandDelegate(int command, int a1 = 0, int a2 = 0, int a3 = 0, int a4 = 0);

    public static readonly ExecuteCommandDelegate? ExecuteCommand = EzDelegate.Get<ExecuteCommandDelegate>("E8 ?? ?? ?? ?? 8D 46 0A");

    public static unsafe bool ChangeBait(int baitItemId)
    {
        if (PlayerState.Instance()->FishingBait == baitItemId)
            return true;

        if (InventoryHelper.GetInventoryItemCount((uint)baitItemId) == 0)
        {
            FullError($"Bait Id {baitItemId} not found in inventory.");
            return false;
        }

        if (Svc.Condition[ConditionFlag.Fishing])
        {
            FullWarning("Can't change bait while fishing.");
            return false;
        }

        ExecuteCommand?.Invoke(701, 4, baitItemId);
        return true;
    }

    // https://github.com/Limiana/Dropbox/blob/main/Dropbox/Memory.cs#L18
    public delegate void OfferItemTradeDelegate(nint tradeAddress, ushort slot, InventoryType type);
    public static readonly OfferItemTradeDelegate? OfferItemTrade = EzDelegate.Get<OfferItemTradeDelegate>("48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 83 B9 ?? ?? ?? ?? ?? 41 8B F0");
    public static unsafe void SafeOfferItemTrade(InventoryType type, ushort slot)
    {
        nint TradeAddress = ((nint)UIModule.Instance()->GetAgentModule()->GetAgentByInternalId(AgentId.Trade)) + 40;
        /*if (Utils.GetSlot(type, slot).ItemId == 0)
        {
            throw new InvalidOperationException($"Attempted to use trade on empty slot {type}, {slot}");
        }*/
        OfferItemTrade?.Invoke(TradeAddress, slot, type);
    }
}
