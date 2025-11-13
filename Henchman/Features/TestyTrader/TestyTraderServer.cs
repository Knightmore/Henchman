using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using ECommons.GameHelpers;
using Henchman.Data;
using Henchman.Generated;
using Henchman.Helpers;
using Henchman.Models;
using Henchman.Multibox;
using Henchman.Multibox.Command;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;

namespace Henchman.Features.TestyTrader;

internal partial class TestyTrader
{
    internal static Dictionary<uint, int> ServerSideInventory = [];
    internal static int                   ServerSideGil;
    internal static Dictionary<uint, int> ServerSideTradingLog = [];
    internal static uint                  charsTraded;
    internal        MultiboxServer        server;

    internal async Task Server(CancellationToken token = default)
    {
        server = new MultiboxServer("TestyTrader", 8, (clientSession, _) => ServerSessionHandler(clientSession, token), token);
        try
        {
            await server.StartRoundRobinAsync();
        } finally
        {
            var result = $"Completed! You traded with {charsTraded} chars:" +
                         Environment.NewLine                                +
                         string.Join(Environment.NewLine, ServerSideTradingLog.Select(kvp => $"{Svc.Data.GetExcelSheet<Item>().GetRow(kvp.Key).Name.ExtractText()}: {kvp.Value:N0}"));

            Info(result);
            server.Disconnect();
            server.Dispose();
        }
    }

    internal async Task ServerSessionHandler(MultiboxServer.ClientSession client, CancellationToken token = default)
    {
        using var scope = new TaskDescriptionScope("Boss Trade Session");
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

                            await MessageHandler.WriteMessageAsync(client.Pipe, CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.MovementRPCs_GoToPlayer), [Player.Territory, Player.Position, Player.CurrentWorld, Player.CID]), token);
                            break;
                        }
                        case TestyTraderMessageType.Arrived:
                        {
                            var  clientEntityId = responseData.EID;
                            uint entityId;
                            unsafe
                            {
                                entityId = Player.BattleChara->EntityId;
                            }

                            var readyMessage = new TestyTraderMessage
                                               {
                                                       Type = TestyTraderMessageType.ReadyForTrade,
                                                       EID  = entityId
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
                            await WaitUntilAsync(() => ProcessYesNo(true, Lang.TradeText), "Confirm Trade", token);
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
                    if (askDict.Count == 0)
                    {
                        continue;
                    }

                    await TestyTraderTasks.Trade(askDict, token);
                }
            }
        }
        catch (Exception e)
        {
            InternalError(e.ToString());
        }
    }
    
    internal static void ProcessAskDict(ref Dictionary<uint, uint> askDict)
    {
        var keysToRemove = new List<uint>();

        foreach (var kvp in askDict)
        {
            var itemId          = kvp.Key;
            var requestedAmount = kvp.Value;
            var currentAmount   = (uint)InventoryHelper.GetInventoryItemCount(itemId);

            if (currentAmount == 0)
                keysToRemove.Add(itemId);
            else if (currentAmount < requestedAmount) askDict[itemId] = currentAmount;
            Verbose($"Current: {currentAmount} | Requested: {requestedAmount}");
        }

        foreach (var key in keysToRemove) askDict.Remove(key);
    }
}
