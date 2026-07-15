using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game;
using Henchman.Generated;
using Henchman.Helpers;
using Henchman.Models;
using Henchman.Multiboxing.Client;
using Henchman.Multiboxing.Command;
using Henchman.Multiboxing.Transport;

namespace Henchman.Features.BumpOnALog;

public partial class BumpOnALog
{
    internal MultiboxClient? client;

    internal async Task Client(CancellationToken token = default)
    {
        var connection = TransportFactory.CreateClientConnection("BumpOnALog");

        client = new MultiboxClient(connection, (stream, incomingMessageQueue, _) => ClientSessionHandler(stream, incomingMessageQueue, token));


        await client.StartAsync();
    }

    internal async Task ClientSessionHandler(Stream serverSession, Channel<(CommandType type, string data)> incomingChannel, CancellationToken token = default)
    {
        unsafe
        {
            statusMessage = new BumpOnALogMessage
                            {
                                    Type      = BumpOnALogMessageType.FirstStatus,
                                    ContentId = Svc.Objects.LocalPlayer!.BattleChara()->ContentId,
                                    WorldId   = (ushort)Svc.Objects.LocalPlayer!.HomeWorld.RowId
                            };
        }

        await MessageHandler.WriteMessageAsync(serverSession, CommandType.Feature, statusMessage.ToJson(), token);

        while (!token.IsCancellationRequested)
        {
            try
            {
                var message = await incomingChannel.Reader.ReadAsync(token);

                switch (message.type)
                {
                    case CommandType.RPC:
                        var result = await CommandProcessor.HandleRPCAsync(message.data, token);
                        if (result.returnValue is false) return;
                        if (result.env.Key == nameof(CommandKey.QuestingRPC_UnlockDuty))
                        {
                            statusMessage = new BumpOnALogMessage
                                            {
                                                    Type = BumpOnALogMessageType.DutyQuest
                                            };
                            await MessageHandler.WriteMessageAsync(serverSession, CommandType.Feature, statusMessage.ToJson(), token);
                        }

                        break;
                    case CommandType.Feature:
                    {
                        TaskLog.Verbose("Got Feature request");
                        var responseData = message.data.FromJson<BumpOnALogMessage>();
                        TaskLog.Verbose($"Got {responseData.Type.ToString()}");
                        switch (responseData.Type)
                        {
                            case BumpOnALogMessageType.HunkMark:
                            {
                                var huntMarks =
                                        GetHuntMarks(true, GetRankInfo(true))
                                               .Where(x => x.GetOpenMonsterNoteKills > 0)
                                               .Select(x =>
                                                       {
                                                           var copy = new HuntMark(x);
                                                           copy.NeededKills = x.GetOpenMonsterNoteKills;
                                                           return copy;
                                                       })
                                               .OrderBy(x => x.TerritoryId)
                                               .ToList();

                                await MessageHandler.WriteMessageAsync(serverSession, CommandType.Feature, new BumpOnALogMessage
                                                                                                           {
                                                                                                                   Type      = BumpOnALogMessageType.HunkMark,
                                                                                                                   HuntMarks = huntMarks,
                                                                                                                   GCRank    = GetGrandCompanyRank()
                                                                                                           }.ToJson(), token);
                                break;
                            }
                            case BumpOnALogMessageType.GCProgress:
                            {
                                var gcRank = GetGrandCompanyRank();
                                var gcQuest = GetGcQuest()
                                       .questId;
                                if (gcQuest > 0 && IsQuestAccepted(gcQuest) && QuestManager.GetQuestSequence(gcQuest) == 255) await Questionable.CompleteQuest(gcQuest, token);

                                while ((gcRank is 4 && HuntLogHelper.GetGrandCompanyRankInfo() is 1) || gcRank is 5 or 6)
                                {
                                    if (CanRankUp())
                                    {
                                        await RankUp(token);
                                        gcRank = GetGrandCompanyRank();
                                    }
                                    else
                                    {
                                        await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
                                        await MessageHandler.WriteMessageAsync(serverSession, CommandType.ServerRequest, ServerRequest.Disconnect.ToJson(), token);
                                        return;
                                    }
                                }

                                while (gcRank is 7 or 8)
                                {
                                    gcQuest = GetGcQuest()
                                           .questId;
                                    if (!IsQuestAccepted(gcQuest) && !IsQuestCompleted(gcQuest))
                                    {
                                        var (questId, dutyId) = GetGcQuest();
                                        await Questionable.GetAndProgressQuest(questId, token);
                                        await MessageHandler.WriteMessageAsync(serverSession, CommandType.Feature, new BumpOnALogMessage
                                                                                                                   {
                                                                                                                           Type     = BumpOnALogMessageType.Duty,
                                                                                                                           GCRank   = GetGrandCompanyRank(),
                                                                                                                           OpenDuty = dutyId
                                                                                                                   }.ToJson(), token);
                                        break;
                                    }

                                    if (IsQuestAccepted(gcQuest) && QuestManager.GetQuestSequence(gcQuest) == 255)
                                    {
                                        await Questionable.CompleteQuest(gcQuest, token);
                                        gcRank = GetGrandCompanyRank();
                                        if (gcRank is 8)
                                        {
                                            var huntMarks =
                                                    GetHuntMarks(true, GetRankInfo(true))
                                                           .Where(x => x.GetOpenMonsterNoteKills > 0)
                                                           .Select(x =>
                                                                   {
                                                                       var copy = new HuntMark(x);
                                                                       copy.NeededKills = x.GetOpenMonsterNoteKills;
                                                                       return copy;
                                                                   })
                                                           .OrderBy(x => x.TerritoryId)
                                                           .ToList();

                                            await MessageHandler.WriteMessageAsync(serverSession, CommandType.Feature, new BumpOnALogMessage
                                                                                                                       {
                                                                                                                               Type      = BumpOnALogMessageType.HunkMark,
                                                                                                                               HuntMarks = huntMarks,
                                                                                                                               GCRank    = GetGrandCompanyRank()
                                                                                                                       }.ToJson(), token);
                                        }
                                    }

                                    if (CanRankUp())
                                    {
                                        await RankUp(token);
                                        gcRank = GetGrandCompanyRank();
                                        continue;
                                    }

                                    await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
                                    await MessageHandler.WriteMessageAsync(serverSession, CommandType.ServerRequest, ServerRequest.Disconnect.ToJson(), token);
                                    return;
                                }

                                if (gcRank is 9)
                                {
                                    await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
                                    await MessageHandler.WriteMessageAsync(serverSession, CommandType.ServerRequest, ServerRequest.Disconnect.ToJson(), token);
                                    return;
                                }

                                break;
                            }
                        }

                        break;
                    }
                    case CommandType.ServerRequest:
                    {
                        var serverRequest = message.data.FromJsonEnum<ServerRequest>();
                        if (serverRequest == ServerRequest.Disconnect) return;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                InternalTaskError(e.ToString());
                return;
            }
        }
    }
}
