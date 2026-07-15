using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Henchman.Generated;
using Henchman.Models;
using Henchman.Multiboxing;
using Henchman.Multiboxing.Command;
using Henchman.Multiboxing.Server;
using Henchman.Multiboxing.Transport;
using Lumina.Excel.Sheets;
using Underlings.GameHelpers;

namespace Henchman.Features.BumpOnALog;

public partial class BumpOnALog
{
    private  bool                                AurumDone;
    private  List<Mount>                         availableMounts   = [];
    internal Dictionary<string, CharacterData>   ClientData        = [];
    private  Dictionary<string, PartyMemberType> clientMemberTypes = [];
    private  uint                                dutyQuestResponses;
    private  bool                                DzemaelDone;
    private  List<uint>                          markDuties         = [];
    private  List<HuntMark>                      openOverworldMarks = [];
    internal MultiboxServer?                     server;

    private BumpOnALogMessage statusMessage;

    internal async Task Server(CancellationToken token = default)
    {
        var listener = TransportFactory.CreateServerListener("TestyTrader", (int)Configuration!.MultiboxPartySize - 1);

        server = new MultiboxServer("BumpOnALog", (int)Configuration!.MultiboxPartySize - 1, (session, type, data, _) => ServerSessionHandler(session, type, data, token), listener, token);

        try
        {
            bool MultiseaterCheck(int connectedClients)
            {
                availableMounts = Svc.Data.Excel.GetSheet<Mount>()
                                     .Where(x => x.ExtraSeats >= connectedClients)
                                     .ToList();
                return availableMounts.Count > 0;
            }

            await server.StartParallelAsync(MultiseaterCheck);
        } finally
        {
            server.Dispose();

            server = null;
        }
    }

    internal async Task<bool> ServerSessionHandler(MultiboxServer.ClientSession client, CommandType type, string data, CancellationToken token = default)
    {
        async Task<bool> HandleRefreshedClientData()
        {
            await ClientData.ForEachAsync(async kvp =>
                                          {
                                              if (kvp.Value.GCRank is < 4 or > 8)
                                              {
                                                  var doneClient = server!.clients.First(x => x.Id == kvp.Key);
                                                  await server.SendToClient(doneClient, CommandType.ServerRequest, ServerRequest.Disconnect.ToJson(), token);
                                                  server!.Kick(doneClient);
                                                  kvp.Value.ContentId = 0;
                                              }
                                          });

            ClientData.RemoveAll(x => x.Value.ContentId == 0);

            if (ClientData.Count == 0)
            {
                TaskLog.Debug("No clients with open progress connected.");
                return false;
            }

            var lowestGCRank = ClientData.Values.Min(x => x.GCRank);
            if (lowestGCRank is 4 or 8)
            {
                openOverworldMarks =
                        ClientData.Values
                                  .Where(x => x.GCRank == lowestGCRank)
                                  .SelectMany(x => x.HuntMarks)
                                  .GroupBy(x => x.BNpcNameRowId)
                                  .Select(x => x
                                              .OrderByDescending(y => y.NeededKills)
                                              .First())
                                  .Where(x => !x.IsDuty)
                                  .OrderBy(x => x.TerritoryId)
                                  .ToList();

                markDuties =
                        ClientData.Values
                                  .Where(x => x.GCRank == lowestGCRank)
                                  .SelectMany(x => x.HuntMarks)
                                  .Where(x => x.IsDuty)
                                  .GroupBy(x => x.BNpcNameRowId)
                                  .Select(x => x
                                              .OrderByDescending(y => y.NeededKills)
                                              .First()
                                              .TerritoryId)
                                  .ToList();

                await openOverworldMarks.ForEachAsync(async mark =>
                                                      {
                                                          await HandlePartyTeleport(mark, token);
                                                          await Mount(availableMounts[0].RowId, token);
                                                          await GetPartyMembersMounted(token);
                                                          await MoveTo(mark.Positions[0], true, token);
                                                          for (var i = 0; i < mark.NeededKills; i++)
                                                              await KillHuntMark(mark, token);
                                                      });

                openOverworldMarks = new List<HuntMark>();

                if (markDuties.Count > 0)
                    await server!.Broadcast(CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.QuestingRPC_UnlockDuty), [markDuties[0]]), token);

                return true;
            }

            if (lowestGCRank is 5 or 6)
            {
                var rankUpClients = ClientData.Where(x => x.Value.GCRank is 5 or 6)
                                              .ToArray();

                await server!.clients.ForEachAsync(async session =>
                                                   {
                                                       if (rankUpClients.Any(y => y.Key == session.Id))
                                                       {
                                                           await server.SendToClient(session, CommandType.Feature, new BumpOnALogMessage
                                                                                                                   {
                                                                                                                           Type = BumpOnALogMessageType.GCProgress
                                                                                                                   }.ToJson(), token);
                                                       }
                                                   });

                await server.Broadcast(CommandType.Feature, new BumpOnALogMessage
                                                            {
                                                                    Type = BumpOnALogMessageType.HunkMark
                                                            }.ToJson(), token);

                ClientData.Values.ForEach(x => x.DataRefreshed = false);
                return true;
            }

            if (lowestGCRank is 7)
            {
                var rankUpClients = ClientData.Where(x => x.Value.GCRank == 7)
                                              .ToArray();

                await server!.clients.ForEachAsync(async session =>
                                                   {
                                                       if (rankUpClients.Any(y => y.Key == session.Id))
                                                       {
                                                           await server.SendToClient(session, CommandType.Feature, new BumpOnALogMessage
                                                                                                                   {
                                                                                                                           Type = BumpOnALogMessageType.GCProgress
                                                                                                                   }.ToJson(), token);
                                                       }
                                                   });

                await server.Broadcast(CommandType.Feature, new BumpOnALogMessage
                                                            {
                                                                    Type = BumpOnALogMessageType.HunkMark
                                                            }.ToJson(), token);

                ClientData.Values.ForEach(x => x.DataRefreshed = false);
            }

            return true;
        }

        switch (type)
        {
            case CommandType.Feature:
            {
                var responseData = data.FromJson<BumpOnALogMessage>();
                switch (responseData.Type)
                {
                    case BumpOnALogMessageType.FirstStatus:
                    {
                        ClientData.TryAdd(client.Id, new CharacterData
                                                     {
                                                             ContentId = responseData.ContentId,
                                                             WorldId   = responseData.WorldId
                                                     }
                                         );
                        if (server!.clients.Count == ClientData.Count)
                        {
                            await ClientData.Values.ForEachAsync(async clientData =>
                                                                 {
                                                                     unsafe
                                                                     {
                                                                         if (Svc.Party.All(x => x.ContentId != clientData.ContentId))
                                                                         {
                                                                             TaskLog.Debug($"Inviting {clientData.ContentId} from {clientData.WorldId}");
                                                                             InfoProxyPartyInvite.Instance()->InviteToPartyContentId(clientData.ContentId, clientData.WorldId);
                                                                         }
                                                                     }

                                                                     await Task.Delay(GeneralDelayMs * 4, token);
                                                                 });

                            await server.Broadcast(CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.GeneralRPC_AcceptInvitation), [Player.Name!]), token);

                            await WaitUntilAsync(() =>
                                                 {
                                                     unsafe
                                                     {
                                                         return GroupManager.Instance()->MainGroup.MemberCount == ClientData.Count + 1;
                                                     }
                                                 }, "Waiting for party members", token);
                            await server!.Broadcast(CommandType.Feature, new BumpOnALogMessage
                                                                         {
                                                                                 Type = BumpOnALogMessageType.HunkMark
                                                                         }.ToJson(), token);
                        }

                        break;
                    }
                    case BumpOnALogMessageType.HunkMark:
                    {
                        ClientData[client.Id] = new CharacterData
                                                {
                                                        DataRefreshed = true,
                                                        HuntMarks     = responseData.HuntMarks!,
                                                        GCRank        = responseData.GCRank
                                                };

                        if (ClientData.Any(x => !x.Value.DataRefreshed)) break;
                        return await HandleRefreshedClientData();
                    }
                    case BumpOnALogMessageType.DutyQuest:
                    {
                        dutyQuestResponses++;
                        if (dutyQuestResponses >= server!.clients.Count)
                        {
                            global::Henchman.IPC.AutoDuty.RunDutyUnsync(markDuties[0]);
                            await server!.Broadcast(CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.AutoDuty_RunDutyUnsync), [markDuties[0]]), token);
                            await WaitUntilAsync(() => Underlings.IPC.AutoDuty.IsStopped.Invoke(), "Waiting for duty completion", token);

                            markDuties.RemoveAt(0);
                            if (markDuties.Count > 0)
                                await server!.Broadcast(CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.QuestingRPC_UnlockDuty), [markDuties[0]]), token);
                            else
                            {
                                await server!.Broadcast(CommandType.Feature, new BumpOnALogMessage
                                                                             {
                                                                                     Type = BumpOnALogMessageType.GCProgress
                                                                             }.ToJson(), token);
                            }

                            dutyQuestResponses = 0;
                        }

                        break;
                    }
                    case BumpOnALogMessageType.Duty:
                    {
                        if (responseData.OpenDuty == 1330)
                        {
                            if (!DzemaelDone)
                            {
                                global::Henchman.IPC.AutoDuty.RunDutyUnsync(1330);
                                await server!.Broadcast(CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.AutoDuty_RunDutyUnsync), [1330]), token);
                                await WaitUntilAsync(() => Underlings.IPC.AutoDuty.IsStopped.Invoke(), "Waiting for duty completion", token);
                                DzemaelDone = true;
                            }

                            await server!.Broadcast(CommandType.Feature, new BumpOnALogMessage
                                                                         {
                                                                                 Type = BumpOnALogMessageType.GCProgress
                                                                         }.ToJson(), token);
                            break;
                        }

                        if (responseData.OpenDuty == 1331)
                        {
                            if (!AurumDone)
                            {
                                global::Henchman.IPC.AutoDuty.RunDutyUnsync(1331);
                                await server!.Broadcast(CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.AutoDuty_RunDutyUnsync), [1331]), token);
                                await WaitUntilAsync(() => Underlings.IPC.AutoDuty.IsStopped.Invoke(), "Waiting for duty completion", token);
                                AurumDone = true;
                            }

                            await server!.Broadcast(CommandType.Feature, new BumpOnALogMessage
                                                                         {
                                                                                 Type = BumpOnALogMessageType.GCProgress
                                                                         }.ToJson(), token);
                        }

                        break;
                    }
                }

                break;
            }
            case CommandType.ServerRequest:
            {
                if (data.FromJsonEnum<ServerRequest>() == ServerRequest.Disconnect)
                {
                    dutyQuestResponses = 0;
                    server!.RemoveClient(client);
                    ClientData.Remove(client.Id);
                    if (ClientData.Count == 0)
                        return false;

                    if (ClientData.All(x => x.Value.DataRefreshed))
                        return await HandleRefreshedClientData();
                }

                break;
            }
        }

        await Task.Delay(50, token);
        return true;
    }

    private async Task HandlePartyTeleport(HuntMark mark, CancellationToken token = default)
    {
        if (server != null)
        {
            await TeleportTo(GetAetheryte(mark.TerritoryId, mark.Positions[0]), token);
            await server.Broadcast(CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.GeneralRPC_HandlePartyTeleport), [GetAetheryte(mark.TerritoryId, mark.Positions[0])]), token);

            await WaitUntilAsync(() => PartyMembers.All(x => x.TerritoryType == Player.TerritoryId && Player.DistanceTo(x.Position) < 6), "Waiting for members", token);
        }
    }

    private async Task GetPartyMembersMounted(CancellationToken token = default)
    {
        if (server != null)
        {
            await server.Broadcast(CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.MovementRPC_GoToPlayer), [Player.TerritoryId, Player.Position, Player.CurrentWorldName, Player.CID]), token);
            await WaitUntilAsync(() =>
                                 {
                                     unsafe
                                     {
                                         return ClientData.Values.Select(c => c.ContentId)
                                                          .All(cid => Svc.Objects.OfType<IPlayerCharacter>()
                                                                         .Any(o => o.BattleChara()->ContentId == cid && Vector3.Distance(o.Position, Player.Position) < 6));
                                     }
                                 }, "Wait for all clients to be close", token);

            await server.Broadcast(CommandType.RPC, CommandEnvelope.Create(nameof(CommandKey.MovementRPC_RidePillion), [Player.CID]), token);
            await WaitUntilAsync(() => Svc.Objects.LocalPlayer!.HasPartyMembersPillion(ClientData.Count), "Waiting for members to mount", token);
        }
    }

    internal class CharacterData
    {
        public bool           DataRefreshed;
        public ulong          ContentId { get; set; }
        public ushort         WorldId   { get; set; }
        public List<HuntMark> HuntMarks { get; set; } = [];
        public int            GCRank    { get; set; }
    }
}
