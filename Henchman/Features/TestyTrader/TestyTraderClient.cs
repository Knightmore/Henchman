using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Henchman.Data;
using Henchman.Generated;
using Henchman.Models;
using Henchman.Multiboxing.Client;
using Henchman.Multiboxing.Command;
using Henchman.Multiboxing.Transport;
using Lumina.Excel.Sheets;
using Underlings.GameHelpers;
using Underlings.TaskManager;
using Module = Underlings.Modules.Module;

namespace Henchman.Features.TestyTrader;

public partial class TestyTrader : Module
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

    private readonly Cached<List<OfflineCharacterData>> charactersCache = new(
                                                                              () => AutoRetainer.GetRegisteredCIDs.Invoke([])
                                                                                                .Select(cid => AutoRetainer.GetOfflineCharacterData.Invoke(cid))
                                                                                                .ToList(),
                                                                              TimeSpan.FromMilliseconds(500));

    internal       MultiboxClient client;
    private static Configuration? Configuration => GetFeatureConfig<TestyTraderUI, Configuration>();

    internal async Task Client(CancellationToken token = default)
    {
        List<MultiboxClient.CharacterData> characters;
        if (Configuration!.TestyTraderARSupport)
        {
            var enabledCharacters = GetCurrentARCharacterData()
                   .Where(x => Configuration!.EnableCharacterForTrade[x.CID]);
            characters = enabledCharacters
                        .Select(c => new MultiboxClient.CharacterData(c.Name, c.World))
                        .ToList();
        }
        else
        {
            characters = Configuration!.TestyTraderImportedCharacters.Where(x => x.Enabled)
                                       .Select(c => new MultiboxClient.CharacterData(c.Name, Svc.Data.GetExcelSheet<World>()
                                                                                                .GetRow(c.WorldId)
                                                                                                .Name.ExtractText()))
                                       .ToList();
        }

        var connection = TransportFactory.CreateClientConnection("TestyTrader");

        client = new MultiboxClient(connection, (stream, incomingMessageQueue, _) => ClientSessionHandler(stream, incomingMessageQueue, token), characters, token);


        await client.StartAsync();
    }

    internal async Task ClientSessionHandler(Stream serverSession, Channel<(CommandType type, string data)> incomingChannel, CancellationToken token = default)
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
                        TaskLog.Verbose("Got Feature request");
                        var responseData = message.data.FromJson<TestyTraderMessage>();
                        TaskLog.Verbose($"Got {responseData.Type.ToString()}");
                        if (responseData.Type == TestyTraderMessageType.ServerStatusCheck)
                        {
                            // TODO: FIX THIS! This is a quick hack because I designed this crap without thinking about cases like when you use AskUntil for gil and then teleport at the end... also AskFor has no tracking so it would loop endlessly
                            if (done)
                            {
                                TaskLog.Debug("Trades done");
                                var finishedMessage = new TestyTraderMessage
                                                      {
                                                              Type = TestyTraderMessageType.ClientFinished
                                                      };
                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, finishedMessage.ToJson(), token);
                                return;
                            }

                            GetTradingDictionaries(out tradeDict, out askDict);
                            TaskLog.Verbose($"Calculated trades - trade: {tradeDict.Count} | ask: {askDict.Count}");
                            var tradeDone = tradeDict.Count == 0;
                            var askDone   = askDict.Count   == 0;

                            if (!askDone || !tradeDone)
                            {
                                TaskLog.Verbose("Trades open");
                                var askListMessage = new TestyTraderMessage
                                                     {
                                                             Type         = TestyTraderMessageType.AskList,
                                                             TradeList    = askDict,
                                                             IsTradeDone  = tradeDone,
                                                             TradingWorld = Player.CurrentWorldName
                                                     };

                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, askListMessage.ToJson(), token);
                                TaskLog.Verbose("askList sent");
                            }
                            else
                            {
                                TaskLog.Debug("Trades done");
                                var finishedMessage = new TestyTraderMessage
                                                      {
                                                              Type = TestyTraderMessageType.ClientFinished
                                                      };
                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, finishedMessage.ToJson(), token);
                                TaskLog.Verbose("finished sent");
                                return;
                            }

                            break;
                        }

                        if (responseData.Type == TestyTraderMessageType.ReadyForTrade)
                        {
                            var bossEID = responseData.EntityID;
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
                            TaskLog.Debug("No items to trade!");
                            return;
                        }

                        var result = await CommandProcessor.HandleRPCAsync(message.data, token);
                        if (result.returnValue is false) return;

                        var rpcKey = Enum.Parse<CommandKey>(result.env.Key);
                        if (rpcKey == CommandKey.MovementRPC_GoToPlayer)
                        {
                            uint entityId;
                            unsafe
                            {
                                entityId = Player.BattleChara->EntityId;
                            }

                            var arrivedMessage = new TestyTraderMessage
                                                 {
                                                         Type     = TestyTraderMessageType.Arrived,
                                                         EntityID = entityId
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
                InternalTaskError(e.ToString());
                return;
            }
        }
    }

    internal static async Task ProcessClientTrade(Stream pipe, Channel<(CommandType type, string data)> incomingChannel, uint bossEID, Dictionary<uint, uint> tradeDict, Dictionary<uint, uint> askDict, CancellationToken token = default)
    {
        using var scope = new TaskDescriptionScope("Processing Henchman Trade");

        TaskLog.Verbose($"Cached BossEID: {bossEID}");
        var bossFound = Svc.Objects.OfType<IPlayerCharacter>()
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
            TaskLog.Verbose("Processing message from queue.");
            if (message.type == CommandType.Feature)
            {
                var statusData = message.data.FromJson<TestyTraderMessage>();
                switch (statusData.Type)
                {
                    case TestyTraderMessageType.ServerStatusCheck:
                    {
                        var serverDone = statusData.IsTradeDone;
                        TaskLog.Verbose($"TradeDict: {tradeDict.Count} | Server done: {(serverDone.HasValue ? serverDone.Value : "No Value")}");
                        switch (tradeDict.Count)
                        {
                            case 0 when serverDone!.Value:
                                var finishedMessage = new TestyTraderMessage
                                                      {
                                                              Type = TestyTraderMessageType.ClientFinished
                                                      };

                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, finishedMessage.ToJson(), token);
                                await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
                                return;
                            case 0 when !serverDone.Value:
                                await WaitUntilAsync(() => TestyTraderTasks.OpenTrade(bossEID, token), "Opening Trade", token);
                                await MessageHandler.WriteMessageAsync(pipe, CommandType.Feature, new TestyTraderMessage
                                                                                                  {
                                                                                                          Type = TestyTraderMessageType.ConfirmTrade
                                                                                                  }.ToJson(), token);
                                break;
                            case > 0:
                                await WaitUntilAsync(() => TestyTraderTasks.OpenTrade(bossEID, token), "Opening Trade", token);
                                TaskLog.Verbose($"Sent Trade Request with {tradeDict.Count.ToString()} open items to hand in!");
                                await TestyTraderTasks.Trade(tradeDict, Configuration!.IncludeArmory, token);
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
                        await WaitUntilAsync(() => RegexYesNo(true, Lang.TradeText), "Confirm Trade", token);
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

    private static void GetTradingDictionaries(out Dictionary<uint, uint> tradeList, out Dictionary<uint, uint> askList)
    {
        tradeList = [];
        askList   = [];

        var activePlan = Configuration!.GetActivePlan();
        if (activePlan == null)
            return;

        foreach (var entry in activePlan.Entries)
        {
            if (!entry.Enabled)
                continue;
            var (isHq, baseId) = (entry.Id >= 1_000_000, entry.Id % 1_000_000);
            switch (entry.Mode)
            {
                case TradeMode.Give:
                {
                    var possibleAmount = InventoryHelper.GetInventoryItemCount(baseId, isHq, Configuration.IncludeArmory);
                    if (possibleAmount == 0 || entry.Amount == 0) continue;
                    tradeList.Add(entry.Id, (uint)Math.Min(possibleAmount, entry.Amount));
                    break;
                }
                case TradeMode.Keep:
                {
                    var possibleAmount = InventoryHelper.GetInventoryItemCount(baseId, isHq, Configuration.IncludeArmory);
                    if (possibleAmount == 0) continue;
                    if (possibleAmount > entry.Amount) tradeList.Add(entry.Id, (uint)(possibleAmount - entry.Amount));
                    break;
                }
                case TradeMode.AskUntil:
                {
                    var currentAmount = InventoryHelper.GetInventoryItemCount(baseId, isHq, Configuration.IncludeArmory);
                    if (currentAmount >= entry.Amount || entry.Amount == 0) continue;
                    askList.Add(entry.Id, entry.Amount - (uint)currentAmount);
                    break;
                }
                case TradeMode.AskFor:
                {
                    askList.Add(entry.Id, entry.Amount);
                    break;
                }
                case TradeMode.PARLevel:
                {
                    var currentAmount = InventoryHelper.GetInventoryItemCount(baseId, isHq, Configuration.IncludeArmory);
                    if (currentAmount == entry.Amount) continue;
                    if (currentAmount      > entry.Amount) tradeList.Add(entry.Id, (uint)currentAmount - entry.Amount);
                    else if (currentAmount < entry.Amount) askList.Add(entry.Id, entry.Amount          - (uint)currentAmount);
                    break;
                }
            }
        }
    }

    internal List<OfflineCharacterData> GetCurrentARCharacterData() => charactersCache.Value;

    public record TestyTraderMessage
    {
        public uint                   EntityID     { get; init; }
        public bool?                  IsTradeDone  { get; init; }
        public Dictionary<uint, uint> TradeList    { get; init; }
        public TestyTraderMessageType Type         { get; init; }
        public string?                TradingWorld { get; init; }
    }
}
