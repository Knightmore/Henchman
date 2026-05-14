using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using ECommons.GameHelpers;
using Henchman.Data;
using Henchman.Generated;
using Henchman.Helpers;
using Henchman.Multiboxing.Command;
using Henchman.Multiboxing.Server;
using Henchman.Multiboxing.Transport;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;

namespace Henchman.Features.TestyTrader;

public partial class TestyTrader
{
    internal static Dictionary<uint, int> ServerSideInventory = [];
    internal static int                   ServerSideGil;
    internal static Dictionary<uint, int> ServerSideTradingLog = [];
    internal static uint                  charsTraded;

    private readonly List<uint> accessibleTerritories = Svc.Data.GetExcelSheet<TerritoryType>()
                                                           .Where(x => Svc.Data.GetExcelSheet<Aetheryte>()
                                                                          .Any(y => y.Territory.RowId == x.RowId && y.Order > 0))
                                                           .Select(x => x.RowId)
                                                           .ToList();

    internal MultiboxServer server;

    internal async Task Server(CancellationToken token = default)
    {
        if (!accessibleTerritories.Contains(Player.Territory.RowId))
        {
            FullWarning("You can not start you boss in a territory that isn't accessible through an aetheryte!");
            return;
        }

        var listener = TransportFactory.CreateServerListener("TestyTrader", 8);

        server = new MultiboxServer(
                                    "TestyTrader",
                                    8,
                                    (clientSession, _) => ServerSessionHandler(clientSession, token),
                                    listener,
                                    token);
        try
        {
            await server.StartRoundRobinAsync();
        } finally
        {
            var result = $"Completed! You traded with {charsTraded} chars:" +
                         Environment.NewLine                                +
                         string.Join(Environment.NewLine, ServerSideTradingLog.Select(kvp => $"{Svc.Data.GetExcelSheet<Item>().GetRow(kvp.Key).Name.ExtractText()}: {kvp.Value:N0}"));
            ServerSideTradingLog = new Dictionary<uint, int>();
            charsTraded          = 0;

            Info(result);
            server.Dispose();
        }
    }

    internal async Task ServerSessionHandler(MultiboxServer.ClientSession client, CancellationToken token = default)
    {
        using var              scope   = new TaskDescriptionScope("Boss Trade Session");
        Dictionary<uint, uint> askDict = [];

        var statusMessage = new TestyTraderMessage
                            {
                                    Type = TestyTraderMessageType.ServerStatusCheck
                            };

        await MessageHandler.WriteMessageAsync(client.Pipe, CommandType.Feature, statusMessage.ToJson(), token);

        await foreach (var message in client.MessageChannel.Reader.ReadAllAsync(token))
        {
            switch (message.type)
            {
                case CommandType.RPC:
                    var result = await CommandProcessor.HandleRPCAsync(message.data, token);
                    if (result.returnValue is false) return;
                    break;
                case CommandType.Feature:
                {
                    var responseData = message.data.FromJson<TestyTraderMessage>();
                    switch (responseData.Type)
                    {
                        case TestyTraderMessageType.AskList:
                        {
                            askDict = responseData.TradeList;
                            ProcessAskDict(ref askDict);
                            var tradeDone = askDict.Count == 0;

                            if (tradeDone && responseData.IsTradeDone!.Value)
                            {
                                var finishedMessage = new TestyTraderMessage
                                                      {
                                                              Type = TestyTraderMessageType.ServerFinished
                                                      };
                                await MessageHandler.WriteMessageAsync(client.Pipe, CommandType.Feature, finishedMessage.ToJson(), token);
                                return;
                            }

                            if (Configuration!.MoveBossToHenchman && responseData.TradingWorld != null && !responseData.TradingWorld.Equals(Player.CurrentWorldName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                var currentPositon   = Player.Position;
                                var closestAetheryte = GetAetheryte(Player.Territory.RowId, Player.Position);
                                Lifestream.ChangeWorld(responseData.TradingWorld);
                                await WaitWhileAsync(() => Lifestream.IsBusy(), "Waiting for world change", token);
                                await TeleportTo(closestAetheryte, token);
                                await MoveTo(currentPositon, true, token);
                                await Dismount(token);
                            }

                            await MessageHandler.WriteMessageAsync(client.Pipe, CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.MovementRPC_GoToPlayer), [Player.Territory.RowId, Player.Position, Player.CurrentWorldName, Player.CID]), token);
                            break;
                        }
                        case TestyTraderMessageType.Arrived:
                        {
                            var  clientEntityId = responseData.EntityID;
                            uint entityId;
                            unsafe
                            {
                                entityId = Player.BattleChara->EntityId;
                            }

                            var readyMessage = new TestyTraderMessage
                                               {
                                                       Type     = TestyTraderMessageType.ReadyForTrade,
                                                       EntityID = entityId
                                               };
                            await MessageHandler.WriteMessageAsync(client.Pipe, CommandType.Feature, readyMessage.ToJson(), token);

                            await ProcessServerTrade(client, clientEntityId, askDict, token);
                            await MessageHandler.WriteMessageAsync(client.Pipe, CommandType.Feature, new TestyTraderMessage
                                                                                                     {
                                                                                                             Type = TestyTraderMessageType.ServerStatusCheck
                                                                                                     }.ToJson(), token);
                            break;
                        }
                        case TestyTraderMessageType.ClientFinished:
                            if (Configuration!.UseARItemSell)
                            {
                                Svc.Commands.ProcessCommand("/ays itemsell");
                                await WaitWhileAsync(() => AutoRetainer.IsBusy(), "Waiting for selling all items", token);
                            }

                            return;
                    }

                    break;
                }
            }
        }
    }

    internal static async Task ProcessServerTrade(MultiboxServer.ClientSession client, ulong henchmanEID, Dictionary<uint, uint> askDict, CancellationToken token = default)
    {
        using var scope = new TaskDescriptionScope("Processing Boss Trade");

        try
        {
            await foreach (var message in client.MessageChannel.Reader.ReadAllAsync(token))
            {
                Verbose("Processing message from queue.");
                if (message.type == CommandType.Feature)
                {
                    var data = message.data.FromJson<TestyTraderMessage>();
                    switch (data.Type)
                    {
                        case TestyTraderMessageType.ClientStatusCheck:
                        {
                            var henchmanDone = data.IsTradeDone!.Value;
                            var statusMessage = askDict.Count == 0
                                                        ? new TestyTraderMessage { Type = TestyTraderMessageType.ServerStatusCheck, IsTradeDone = true }
                                                        : new TestyTraderMessage { Type = TestyTraderMessageType.ServerStatusCheck, IsTradeDone = false, TradeList = askDict };

                            await MessageHandler.WriteMessageAsync(client.Pipe, CommandType.Feature, statusMessage.ToJson(), token);
                            if (henchmanDone && askDict.Count == 0)
                                continue;
                            break;
                        }
                        case TestyTraderMessageType.ConfirmTrade:
                            await WaitUntilAsync(() => TestyTraderTasks.ConfirmTrade(), "Waiting to confirm trade", token);
                            await Task.Delay(GeneralDelayMs, token);
                            await MessageHandler.WriteMessageAsync(client.Pipe, CommandType.Feature, new TestyTraderMessage
                                                                                                     {
                                                                                                             Type = TestyTraderMessageType.ConfirmTrade
                                                                                                     }.ToJson(), token);
                            await WaitUntilAsync(() => RegexYesNo(true, Lang.TradeText), "Confirm Trade", token);
                            await WaitUntilAsync(() => !Svc.Condition[ConditionFlag.TradeOpen], "Waiting for Trade to close", token);
                            TestyTraderTasks.CalculateInventoryDifference(ServerSideInventory, ServerSideTradingLog, ServerSideGil);
                            continue;
                        case TestyTraderMessageType.ClientFinished:
                            charsTraded++;
                            return;
                    }

                    ServerSideInventory = TestyTraderTasks.GetCurrentInventory();
                    ServerSideGil       = InventoryHelper.GetInventoryItemCount(1);
                    await WaitUntilAsync(() => Svc.Condition[ConditionFlag.TradeOpen] && TestyTraderTasks.CheckForTradePartner(henchmanEID), "Waiting for correct trade partner", token);
                    await Task.Delay(2 * GeneralDelayMs, token);
                    if (askDict.Count == 0) continue;

                    await TestyTraderTasks.Trade(askDict, token);
                }
            }
        }
        catch (Exception e)
        {
            InternalTaskError(e.ToString());
        }
    }

    internal static void ProcessAskDict(ref Dictionary<uint, uint> askDict)
    {
        var keysToRemove = new List<uint>();

        foreach (var kvp in askDict)
        {
            var (isHq, baseId) = (kvp.Key >= 1_000_000, kvp.Key % 1_000_000);
            var requestedAmount = kvp.Value;
            var currentAmount   = (uint)InventoryHelper.GetInventoryItemCount(baseId, isHq);

            if (currentAmount == 0)
                keysToRemove.Add(kvp.Key);
            else if (currentAmount < requestedAmount) askDict[kvp.Key] = currentAmount;
            Verbose($"Current: {currentAmount} | Requested: {requestedAmount}");
        }

        foreach (var key in keysToRemove) askDict.Remove(key);
    }
}
