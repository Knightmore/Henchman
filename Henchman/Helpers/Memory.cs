using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Henchman.Helpers;

internal static class Memory
{
    // https://github.com/Limiana/Dropbox/blob/main/Dropbox/Memory.cs#L18
    public delegate void OfferItemTradeDelegate(nint tradeAddress, ushort slot, InventoryType type);

    // Client::UI::Agent::AgentTrade___Client::UI::Agent::AgentInventoryContext::InventoryContextEvent_vf0
    public static readonly OfferItemTradeDelegate? OfferItemTrade = EzDelegate.Get<OfferItemTradeDelegate>("48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 83 B9 ?? ?? ?? ?? ?? 41 8B F0");

    public static unsafe void SafeOfferItemTrade(InventoryType type, ushort slot)
    {
        var TradeAddress = (nint)UIModule.Instance()->GetAgentModule()->GetAgentByInternalId(AgentId.Trade) + 40;
        if (InventoryHelper.GetItemSlot(type, slot)
                           .ItemId ==
            0) throw new InvalidOperationException($"Attempted to use trade on empty slot {type}, {slot}");
        OfferItemTrade?.Invoke(TradeAddress, slot, type);
    }
}
