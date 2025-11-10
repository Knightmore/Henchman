using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AutoRetainerAPI.Configuration;
using Dalamud.Game.ClientState.Objects.SubKinds;
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
    public enum TestyTraderMessageType : ushort
    {
        Arrived,
        ReadyForTrade,
        AskList,
        ClientStatusCheck,
        ServerStatusCheck,
        ConfirmTrade,
        ClientFinished,
        ServerFinished
    }

    internal MultiboxClient client;

    internal async Task Client(CancellationToken token = default)
    {
        List<MultiboxClient.CharacterData> characters;
        if (C.TestyTraderARSupport)
        {
            var enabledCharacters = GetCurrentARCharacterData()
                   .Where(x => C.EnableCharacterForTrade[x.CID]);
            characters = enabledCharacters
                        .Select(c => new MultiboxClient.CharacterData(c.Name, c.World))
                        .ToList();
        }
        else
        {
            characters = C.TestyTraderImportedCharacters.Where(x => x.Enabled)
                          .Select(c => new MultiboxClient.CharacterData(c.Name, Svc.Data.GetExcelSheet<World>().GetRow(c.WorldId).Name.ExtractText()))
                          .ToList();
        }

        client = new MultiboxClient("TestyTrader", (pipe, incomingMessageQueue, _) => ClientSessionHandler(pipe, incomingMessageQueue, token), characters, token);
        await client.StartAsync();
    }

    internal async Task ClientSessionHandler(PipeStream serverSession, Channel<(CommandType type, string data)> incomingChannel, CancellationToken token = default)
    {
        using var              scope     = new TaskDescriptionScope("Henchman Trade Session");
        var                    pipe      = serverSession;
        Dictionary<uint, uint> tradeDict = [];
        Dictionary<uint, uint> askDict   = [];
        var                    done      = false;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var message = await incomingChannel.Reader.ReadAsync(token);
                switch (message.type)
                {
                    case CommandType.Feature:
                        Verbose("Got Feature request");
                        var responseData = message.data.FromJson<TestyTraderMessage>();
                        Verbose($"Got {responseData.Type.ToString()}");
                        if (responseData.Type == TestyTraderMessageType.ServerStatusCheck)
                        {
                            // TODO: FIX THIS! This is a quick hack because I designed this crap without thinking about cases like when you use AskUntil for gil and then teleport at the end... also AskFor has no tracking so it would loop endlessly
                            if (done)
                            {
                                Verbose("Trades done");
                                var finishedMessage = new TestyTraderMessage
                                                      {
                                                              Type = TestyTraderMessageType.ClientFinished
                                                      };
                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, finishedMessage.ToJson(), token);
                                return;
                            }

                            GetTradingDictionaries(out tradeDict, out askDict);
                            Verbose($"Calculated trades - trade: {tradeDict.Count} | ask: {askDict.Count}");
                            var tradeDone = tradeDict.Count == 0;
                            var askDone   = askDict.Count   == 0;

                            if (!askDone || !tradeDone)
                            {
                                Verbose("Trades open");
                                var askListMessage = new TestyTraderMessage
                                                     {
                                                             Type        = TestyTraderMessageType.AskList,
                                                             TradeList   = askDict,
                                                             IsTradeDone = tradeDone
                                                     };

                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, askListMessage.ToJson(), token);
                                Verbose("askList sent");
                            }
                            else
                            {
                                Verbose("Trades done");
                                var finishedMessage = new TestyTraderMessage
                                                      {
                                                              Type = TestyTraderMessageType.ClientFinished
                                                      };
                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, finishedMessage.ToJson(), token);
                                Verbose("finished sent");
                                return;
                            }

                            break;
                        }

                        if (responseData.Type == TestyTraderMessageType.ReadyForTrade)
                        {
                            var bossEID = responseData.EID;
                            await ProcessClientTrade(pipe, incomingChannel, bossEID, tradeDict, askDict, token);
                            done = true;
                            break;
                        }

                        if (responseData.Type == TestyTraderMessageType.ServerFinished)
                            return;
                        break;
                    case CommandType.RPC:
                        if (tradeDict.Count == 0 && askDict.Count == 0)
                        {
                            var finishedMessage = new TestyTraderMessage
                                                  {
                                                          Type = TestyTraderMessageType.ClientFinished
                                                  };
                            await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, finishedMessage.ToJson(), token);
                            Verbose("No items to trade!");
                            return;
                        }

                        var result = await CommandProcessor.HandleRPCAsync(message.data, token);
                        if (result.returnValue is false) return;

                        var rpcKey = Enum.Parse<CommandKey>(result.env.Key);
                        if (rpcKey == CommandKey.MovementRPCs_GoToPlayer)
                        {
                            uint entityId;
                            unsafe
                            {
                                entityId = Player.BattleChara->EntityId;
                            }

                            var arrivedMessage = new TestyTraderMessage
                                                 {
                                                         Type = TestyTraderMessageType.Arrived,
                                                         EID  = entityId
                                                 };
                            await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, arrivedMessage.ToJson(), token);
                        }
                        else
                            throw new InvalidOperationException($"Unexpected RPC {result.env.Key} received!");

                        break;
                }
            }
            catch (Exception e)
            {
                InternalError(e.ToString());
                return;
            }
        }
    }

    internal static async Task ProcessClientTrade(PipeStream pipe, Channel<(CommandType type, string data)> incomingChannel, uint bossEID, Dictionary<uint, uint> tradeDict, Dictionary<uint, uint> askDict, CancellationToken token = default)
    {
        using var scope = new TaskDescriptionScope("Processing Henchman Trade");

        Verbose($"Cached BossEID: {bossEID}");
        bool bossFound;
        bossFound =
                Svc.Objects.OfType<IPlayerCharacter>()
                   .TryGetFirst(x => x.EntityId == bossEID, out _);

        if (!bossFound)
        {
            FullError("Boss not found!");
            var finishedMessage = new TestyTraderMessage
                                  {
                                          Type = TestyTraderMessageType.ClientFinished
                                  };

            await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, finishedMessage.ToJson(), token);
            return;
        }

        var statusCheckMessage = new TestyTraderMessage
                                 {
                                         Type        = TestyTraderMessageType.ClientStatusCheck,
                                         IsTradeDone = tradeDict.Count == 0
                                 };

        await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, statusCheckMessage.ToJson(), token);

        await foreach (var message in incomingChannel.Reader.ReadAllAsync(token))
        {
            Verbose("Processing message from queue.");
            if (message.type == CommandType.Feature)
            {
                var statusData = message.data.FromJson<TestyTraderMessage>();
                switch (statusData.Type)
                {
                    case TestyTraderMessageType.ServerStatusCheck:
                    {
                        var serverDone = statusData.IsTradeDone;
                        Verbose($"TradeDict: {tradeDict.Count} | Server done: {serverDone!.Value}");
                        IPlayerCharacter targetPlayer;
                        if (Svc.Objects.OfType<IPlayerCharacter>()
                               .TryGetFirst(x => x.EntityId == bossEID, out targetPlayer)) ;
                        switch (tradeDict.Count)
                        {
                            case 0 when serverDone!.Value:
                                var finishedMessage = new TestyTraderMessage
                                                      {
                                                              Type = TestyTraderMessageType.ClientFinished
                                                      };

                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, finishedMessage.ToJson(), token);
                                await Lifestream.LifestreamReturn(C.ReturnTo, true, token);
                                return;
                            case 0 when !serverDone.Value:
                                await WaitUntilAsync(() => TestyTraderTasks.OpenTrade(bossEID, token), "Opening Trade", token);
                                // TODO: Check why the direct TradeRequest is unreliable. Sometime it will open the trade window and immediately close it again on the client side... (don't forget next case too)

                                // InventoryHelper.SendTradeRequest(targetPlayer.EntityId);
                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, new TestyTraderMessage
                                                                                                  {
                                                                                                          Type = TestyTraderMessageType.ConfirmTrade
                                                                                                  }.ToJson(), token);
                                break;
                            case > 0:
                                await WaitUntilAsync(() => TestyTraderTasks.OpenTrade(bossEID, token), "Opening Trade", token);
                                //InventoryHelper.SendTradeRequest(targetPlayer.EntityId);
                                Verbose($"Sent Trade Request with {tradeDict.Count.ToString()} open items to hand in!");
                                await TestyTraderTasks.Trade(tradeDict, token);
                                await Task.Delay(GeneralDelayMs, token);
                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, new TestyTraderMessage
                                                                                                  {
                                                                                                          Type = TestyTraderMessageType.ConfirmTrade
                                                                                                  }.ToJson(), token);
                                break;
                        }

                        break;
                    }
                    case TestyTraderMessageType.ConfirmTrade:
                        await WaitUntilAsync(() => TestyTraderTasks.CheckForTradeConfirmation(), "Waiting for Boss to finish", token);
                        await WaitUntilAsync(() => TestyTraderTasks.ConfirmTrade(), "Waiting to confirm trade", token);
                        await WaitUntilAsync(() => ProcessYesNo(true, Lang.TradeText), "Confirm Trade", token);
                        await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, new TestyTraderMessage
                                                                                          {
                                                                                                  Type        = TestyTraderMessageType.ClientStatusCheck,
                                                                                                  IsTradeDone = tradeDict.Count == 0
                                                                                          }.ToJson(), token);
                        break;
                }
            }
        }
    }

    private static void GetTradingLists(out List<TradeEntry> tradeList, out List<TradeEntry> askList)
    {
        tradeList = [];
        askList   = [];

        foreach (var entry in C.TradeEntries)
        {
            switch (entry.Mode)
            {
                case TradeMode.Give:
                {
                    var possibleAmount = InventoryHelper.GetInventoryItemCount(entry.Id);
                    if (possibleAmount == 0) break;
                    tradeList.Add(new TradeEntry
                                  {
                                          Amount = (uint)Math.Min(possibleAmount, entry.Amount),
                                          Id     = entry.Id
                                  });
                    break;
                }
                case TradeMode.Keep:
                {
                    var possibleAmount = InventoryHelper.GetInventoryItemCount(entry.Id);
                    if (possibleAmount == 0) break;
                    if (possibleAmount > entry.Amount)
                    {
                        tradeList.Add(new TradeEntry
                                      {
                                              Amount = (uint)(possibleAmount - entry.Amount),
                                              Id     = entry.Id
                                      });
                    }

                    break;
                }
                case TradeMode.AskUntil:
                {
                    var currentAmount = InventoryHelper.GetInventoryItemCount(entry.Id);
                    if (currentAmount >= entry.Amount) break;
                    askList.Add(new TradeEntry
                                {
                                        Amount = entry.Amount - (uint)currentAmount,
                                        Id     = entry.Id
                                });
                    break;
                }
                case TradeMode.AskFor:
                {
                    askList.Add(new TradeEntry
                                {
                                        Amount = entry.Amount,
                                        Id     = entry.Id
                                });
                    break;
                }
            }
        }
    }

    private static void GetTradingDictionaries(out Dictionary<uint, uint> tradeList, out Dictionary<uint, uint> askList)
    {
        tradeList = [];
        askList   = [];

        foreach (var entry in C.TradeEntries)
        {
            if (!entry.Enabled)
                continue;
            switch (entry.Mode)
            {
                case TradeMode.Give:
                {
                    var possibleAmount = InventoryHelper.GetInventoryItemCount(entry.Id);
                    if (possibleAmount == 0 || entry.Amount == 0) continue;
                    tradeList.Add(entry.Id, (uint)Math.Min(possibleAmount, entry.Amount));
                    break;
                }
                case TradeMode.Keep:
                {
                    var possibleAmount = InventoryHelper.GetInventoryItemCount(entry.Id);
                    if (possibleAmount == 0 || entry.Amount == 0) continue;
                    if (possibleAmount > entry.Amount) tradeList.Add(entry.Id, (uint)(possibleAmount - entry.Amount));
                    break;
                }
                case TradeMode.AskUntil:
                {
                    var currentAmount = InventoryHelper.GetInventoryItemCount(entry.Id);
                    if (currentAmount >= entry.Amount || entry.Amount == 0) continue;
                    askList.Add(entry.Id, entry.Amount - (uint)currentAmount);
                    break;
                }
                case TradeMode.AskFor:
                {
                    askList.Add(entry.Id, entry.Amount);
                    break;
                }
            }
        }
    }

    internal List<OfflineCharacterData> GetCurrentARCharacterData()
    {
        List<OfflineCharacterData> characters = [];
        var                        cids       = IPC.AutoRetainer.GetRegisteredCIDs();
        foreach (var cid in cids) characters.Add(IPC.AutoRetainer.GetOfflineCharacterData(cid));

        return characters;
    }

    public record TestyTraderMessage
    {
        public bool?                  AllCharsDone { get; init; }
        public uint                   EID          { get; init; }
        public bool?                  IsTradeDone  { get; init; }
        public Dictionary<uint, uint> TradeList    { get; init; }
        public TestyTraderMessageType Type         { get; init; }
    }
}
