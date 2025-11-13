using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Helpers;
using Henchman.TaskManager;

namespace Henchman.Features.TestyTrader;

internal static class TestyTraderTasks
{
    internal static async Task<bool> OpenTrade(uint entityId, CancellationToken token = default)
    {
        if (Svc.Objects.OfType<IPlayerCharacter>()
               .TryGetFirst(x => x.EntityId == entityId, out var targetPlayer))
            Svc.Targets.Target = targetPlayer;
        else
            return false;

        await Task.Delay(2 * GeneralDelayMs, token);

        Chat.ExecuteCommand("/trade");

        await Task.Delay(2 * GeneralDelayMs, token);

        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon))
            {
                return true;
            }
        }

        return false;
    }

    // TODO: Needs more tests as it's randomly closing right after the request
    internal static async Task<bool> OpenTradeNative(uint entityId, CancellationToken token = default)
    {
        if (Svc.Objects.OfType<IPlayerCharacter>()
               .TryGetFirst(x => x.EntityId == entityId, out var targetPlayer))
            Svc.Targets.Target = targetPlayer;
        else
            return false;

        await Task.Delay(GeneralDelayMs, token);

        InventoryHelper.SendTradeRequest(entityId);
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon)) return true;
        }

        return false;
    }
    
    internal static bool SetNumericInput(uint num)
    {
        if (num > 1000000) throw new ArgumentOutOfRangeException(nameof(num));
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("InputNumeric", out var addon) && IsAddonReady(addon))
            {
                {
                    Callback.Fire(addon, true, num);
                    return true;
                }
            }
        }

        return false;
    }

    // TODO: Maybe needs to be an async function, so it can await a Delay for the case when the PartnerEntityId isn't set fast enough
    internal static unsafe bool CheckForTradePartner(ulong traderEID)
    {
        if (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon))
        {
            if (InventoryManager.Instance()->TradePartnerEntityId == traderEID) return true;

            InventoryManager.Instance()->RefuseTrade();
        }

        return false;
    }
    
    internal static unsafe bool CheckForTradeConfirmation()
    {
        if (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon))
        {
            var check = addon->UldManager.NodeList[31]->GetAsAtkComponentNode()->Component->UldManager.NodeList[0]->GetAsAtkImageNode();
            var ready = check->AtkResNode.Color.A == 0xFF;
            return ready;
        }

        return false;
    }

    internal static unsafe bool ConfirmTrade()
    {
        if (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon))
        {
            var tradeButton = (AtkComponentButton*)addon->UldManager.NodeList[3]->GetComponent();
            if (tradeButton->IsEnabled)
            {
                tradeButton->ClickAddonButton(addon);
                Verbose("Click Trade");
                return true;
            }
        }

        return false;
    }

    internal static async Task Trade(Dictionary<uint, uint> tradeDict, CancellationToken token = default)
    {
        using var        scope      = new TaskDescriptionScope("Trading");
        List<QueueEntry> tradeQueue = [];

        await WaitUntilAsync(() => Svc.Condition[ConditionFlag.TradeOpen], "Waiting for Trade to open", token);
        uint gilToTrade = 0;
        if (tradeDict.ContainsKey(1))
        {
            var inventoryGil = (uint)InventoryHelper.GetInventoryItemCount(1);
            var neededGil    = tradeDict[1];


            if (neededGil > inventoryGil)
                gilToTrade = inventoryGil;
            else if (neededGil > 1000000)
                gilToTrade = 1000000;
            else
                gilToTrade = neededGil;

            tradeDict[1] -= gilToTrade;
            if (tradeDict[1] == 0) tradeDict.Remove(1);
        }

        unsafe
        {
            List<uint> entriesToRemove = [];
            var        itemAmount      = 0;
            Verbose($"Total Itemtypes to trade: {tradeDict.Count}");

            foreach (var item in tradeDict)
            {
                Verbose($"Item: {item.Key} | Amount: {tradeDict[item.Key]}");
                if (item.Key == 1)
                    continue;
                if (itemAmount == 5)
                    break;
                var im = InventoryManager.Instance();
                foreach (var type in InventoryHelper.MainInventory)
                {
                    if (itemAmount == 5)
                        break;
                    var cont = im->GetInventoryContainer(type);
                    for (var i = 0; i < cont->Size; i++)
                    {
                        if (itemAmount == 5)
                            break;
                        var slot = *cont->GetInventorySlot(i);
                        if (slot.ItemId == item.Key && tradeDict[item.Key] > 0)
                        {
                            var quantity = tradeDict[item.Key] < slot.Quantity
                                                   ? tradeDict[item.Key]
                                                   : (uint)slot.Quantity;
                            Verbose($"Open trade amount: {tradeDict[item.Key]} | Quantity to pass: {quantity}");
                            tradeDict[item.Key] -= quantity;
                            Verbose($"Open trade amount: {tradeDict[item.Key]}");
                            if (tradeDict[item.Key] == 0)
                                entriesToRemove.Add(item.Key);
                            tradeQueue.Add(new QueueEntry(type, i, quantity));
                            itemAmount++;
                        }
                    }
                }
            }

            foreach (var itemId in entriesToRemove)
                tradeDict.Remove(itemId);
        }

        Verbose($"Current tradeQueue amount: {tradeQueue.Count}");
        Verbose($"Gil to trade: {gilToTrade}");

        if (gilToTrade > 0) InventoryHelper.SetTradeGilAmount(gilToTrade);
        await Task.Delay(4 * GeneralDelayMs, token);

        for (var i = 0; i < tradeQueue.Count; i++)
        {
            var entry = tradeQueue[i];
            await WaitUntilAsync(() => TryAddItemHandin(entry, i), $"Filling HandIn slot {i + 1}", token);
            await Task.Delay(GeneralDelayMs, token);
            if (InventoryHelper.GetItemSlot(entry.Type, entry.SlotID)
                               .Quantity >
                1)
            {
                var amount = Math.Min(InventoryHelper.GetItemSlot(entry.Type, entry.SlotID)
                                                     .Quantity, entry.Quantity);
                if (amount < 1) throw new ArgumentOutOfRangeException();
                await WaitUntilAsync(() => SetNumericInput((uint)amount), $"SetInputNumeric {amount}", token);
            }

            await Task.Delay(GeneralDelayMs * 2, token);
        }
    }

    private static unsafe bool TryAddItemHandin(QueueEntry entry, int slot)
    {
        if (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon))
        {
            if (InventoryHelper.GetItemSlot(InventoryType.HandIn, slot)
                               .ItemId !=
                0) return true;
            Memory.SafeOfferItemTrade(entry.Type, (ushort)entry.SlotID);
        }

        return false;
    }

    internal static unsafe Dictionary<uint, int> GetCurrentInventory()
    {
        Dictionary<uint, int> Items = [];
        var                   im    = InventoryManager.Instance();
        foreach (var type in InventoryHelper.MainInventory)
        {
            var cont = im->GetInventoryContainer(type);
            for (var i = 0; i < cont->Size; i++)
            {
                var slot                                                          = *cont->GetInventorySlot(i);
                if (!Items.TryAdd(slot.ItemId, slot.Quantity)) Items[slot.ItemId] += slot.Quantity;
            }
        }

        return Items;
    }

    internal static void CalculateInventoryDifference(Dictionary<uint, int> oldInventory, Dictionary<uint, int> diffLog, int oldGil)
    {
        var newInventory = GetCurrentInventory();
        foreach (var item in newInventory)
        {
            var newQty = item.Value;
            var oldQty = oldInventory.GetValueOrDefault(item.Key, 0);

            var difference = newQty - oldQty;
            if (difference > 0)
                if (!diffLog.TryAdd(item.Key, difference))
                    diffLog[item.Key] += difference;
        }

        var newGil        = InventoryHelper.GetInventoryItemCount(1);
        var gilDifference = newGil - oldGil;

        if (gilDifference > 0)
            if (!diffLog.TryAdd(1, gilDifference))
                diffLog[1] += gilDifference;
    }

    public record struct QueueEntry(InventoryType Type, int SlotID, uint Quantity)
    {
        public uint          Quantity = Quantity;
        public int           SlotID   = SlotID;
        public InventoryType Type     = Type;
    }
}
