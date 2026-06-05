using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Abstractions;
using Henchman.Data;
using Henchman.Helpers;
using Henchman.Models;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GrandCompany = Lumina.Excel.Sheets.GrandCompany;

namespace Henchman.Features.BumpOnALog;

public partial class BumpOnALog : Feature
{
    private static readonly (uint Quest, uint Duty)[] DzemaelDataRank7 =
    [
            (66664, 1330), // Maelstrom
            (66665, 1330), // Twin Adder
            (66666, 1330)  // Immortal Flames
    ];

    private static readonly (uint gcQuest, uint Duty)[] AurumDataRank8 =
    [
            (66667, 1331), // Maelstrom
            (66668, 1331), // Twin Adder
            (66669, 1331)  // Immortal Flames
    ];

    private static Configuration? Configuration => GetFeatureConfig<BumpOnALogUI, Configuration>();

    internal async Task StartGCRank(CancellationToken token = default, bool doDutyMarks = false)
    {
        await Process(true, doDutyMarks, token);
    }

    internal async Task StartClassRank(CancellationToken token = default)
    {
        await Process(false, token: token);
    }

    private async Task Process(bool gcLog, bool doDutyMarks = false, CancellationToken token = default)
    {
        if (!IsCombat(Player.ClassJob.RowId))
        {
            ChatPrintWarning("You do not have equipped a combat class!");
            return;
        }

        if (!gcLog)
        {
            while (GetRankInfo(gcLog) < Configuration!.StopAfterJobRank + 1)
            {
                var rank = GetRankInfo(gcLog);

                var requiredLevel = rank switch
                {
                    2 => 10,
                    3 => 20,
                    4 => 30,
                    5 => 40,
                    _ => 0
                };

                if (Svc.PlayerState.Level < requiredLevel)
                    break;


                var huntMarks = GetHuntMarks(gcLog, rank);

                var overworldMarks = huntMarks
                                    .Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: false })
                                    //.If(gcLog, q => q.OrderBy(x => x.TerritoryId))
                                    .ToList();


                var dutyMarks = huntMarks
                               .Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: true })
                               .OrderBy(x => x.TerritoryId)
                               .ToList();

                if (overworldMarks.Count == 0)
                {
                    var openMarks = huntMarks.Where(x => x.GetOpenMonsterNoteKills > 0)
                                             .ToList();
                    if (openMarks.Count > 0)
                    {
                        FullWarning($"No actionable class hunt log marks for rank {rank + 1}. Open marks after level resolving: {string.Join(", ", openMarks.Select(x => $"{x.Name} ({x.BNpcNameRowId}, lvl {x.Level?.ToString() ?? "?"}, territory {x.TerritoryId}, fate {x.FateId}, duty {x.IsDuty})"))}");
                        break;
                    }
                }

                await ProcessAllMarks(overworldMarks, dutyMarks, gcLog, doDutyMarks, token);
                await Task.Delay(GeneralDelayMs, token);
            }
        }
        else
        {
            Verbose($"GrandCompanyRank {GetGrandCompanyRank()} | {Configuration.StopAfterGCRank + 1}");
            while (GetGrandCompanyRank() <= Configuration!.StopAfterGCRank + 1)
            {
                Log($"{Configuration!.StopAfterGCRank + 1} -> {GetRankInfo(gcLog)} | {GetGrandCompanyRank() < Configuration!.StopAfterGCRank + 1}");
                Verbose("Below second threshold");
                var rank = GetRankInfo(gcLog);
                var huntMarks = GetHuntMarks(gcLog, rank);

                var overworldMarks = huntMarks
                                    .Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: false })
                                    //.If(gcLog, q => q.OrderBy(x => x.TerritoryId))
                                    .ToList();

                var dutyMarks = huntMarks
                               .Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: true })
                               .OrderBy(x => x.TerritoryId)
                               .ToList();

                var gcRank = GetGrandCompanyRank();

                if (gcRank is >= 1 and <= 9)
                {
                    var handled = await HandleGcRankAsync(gcRank, overworldMarks, dutyMarks, doDutyMarks, token);
                    if (handled)
                        break;
                }

                /*if (gcRank > 8)
                {
                    await ProcessAllMarks(overworldMarks, dutyMarks, gcLog, doDutyMarks, token);
                    break;
                }*/
            }
        }

        ChatPrintInfo("Completed all selected mob entries!");
        await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
    }

    private async Task<bool> HandleGcRankAsync(
            int gcRank,
            List<HuntMark> overworldMarks,
            List<HuntMark> dutyMarks,
            bool doDutyMarks,
            CancellationToken token)
    {
        Verbose($"Handle current GC Rank {GetGrandCompanyRank()} - Log {GetCurrentGcLogRank()}");

        var currentGcLogRank = GetCurrentGcLogRank();

        if ((currentGcLogRank == 0 && GetGrandCompanyRank() <= 4) || (currentGcLogRank == 1 && GetGrandCompanyRank() is >= 5 and <= 8) || (currentGcLogRank == 2 && GetGrandCompanyRank() >= 9)) await ProcessAllMarks(overworldMarks, dutyMarks, true, doDutyMarks, token);

        if (Configuration!.AutoGCRankUp)
        {
            if (gcRank is 7 or 8) await HandleGcQuestAsync(token);

            if (CanRankUp())
            {
                await RankUp(token);
                return false;
            }
        }

        return true;
    }


    private unsafe List<HuntMark> GetHuntMarks(bool gcLog, int currentRank)
    {
        return Enumerable.Range(0, gcLog
                                           ? GcHuntRanks[PlayerState.Instance()->GrandCompany]
                                            .HuntMarks.GetLength(1)
                                           : ClassHuntRanks[(uint)Svc.Data.GetExcelSheet<ClassJob>()
                                                                     .GetRow(PlayerState.Instance()->CurrentClassJobId)
                                                                     .MonsterNote.RowId.ToInt()]
                                            .HuntMarks.GetLength(1))
                         .Select(col =>
                                 {
                                     var original = gcLog
                                                            ? GcHuntRanks[PlayerState.Instance()->GrandCompany]
                                                                   .HuntMarks[currentRank, col]
                                                            : ClassHuntRanks[(uint)Svc.Data.GetExcelSheet<ClassJob>()
                                                                                      .GetRow(PlayerState.Instance()->CurrentClassJobId)
                                                                                      .MonsterNote.RowId.ToInt()]
                                                                   .HuntMarks[currentRank, col];

                                     return original == null
                                                    ? null
                                                    : HuntDatabase.ResolveBestLevelVariant(original, Svc.PlayerState.Level, preferOverworldNonFate: !gcLog);
                                 })
                         .Where(mark => mark != null)
                         .OfType<HuntMark>()
                         .ToList();
    }

    private (uint questId, uint dutyId) GetGcQuest()
    {
        return GetGrandCompanyRank() switch
        {
            7 => DzemaelDataRank7[GetGrandCompany() - 1],
            8 => AurumDataRank8[GetGrandCompany() - 1],
            _ => (0, 0)
        };
    }

    private async Task HandleGcQuestAsync(CancellationToken token)
    {
        bool accepted;
        var (questId, dutyId) = GetGcQuest();
        if (questId == 0) return;
        unsafe
        {
            accepted = QuestManager.Instance()->IsQuestAccepted(questId);
        }

        var completed = QuestManager.IsQuestComplete(questId);
        if (!accepted && !completed) await Questionable.GetAndProgressQuest(questId, token);
        var seq = QuestManager.GetQuestSequence(questId);
        if (seq == 2)
        {
            AutoDuty.RunDutySupport(dutyId);
            await WaitUntilAsync(AutoDuty.IsStopped, "Waiting for Duty to finish", token);
            seq = QuestManager.GetQuestSequence(questId);
        }

        if (seq == 255) await Questionable.CompleteQuest(questId, token);
    }

    private unsafe int GetCurrentGcLogRank()
    {
        var gcMonsterNoteId = (int)Svc.Data.GetExcelSheet<GrandCompany>()
                                      .GetRow(PlayerState.Instance()->GrandCompany)
                                      .MonsterNote.RowId;

        return MonsterNoteManager.Instance()->RankData[gcMonsterNoteId].Rank;
    }

    private bool CanRankUp()
    {
        var seals = InventoryHelper.GetGCSealAmount();
        return GetGrandCompanyRank() switch
        {
            1 => seals >= 2000,
            2 => seals >= 3000,
            3 => seals >= 4000,
            4 => seals >= 5000,
            5 => seals >= 6000,
            6 => seals >= 7000,
            7 => seals >= 8000,
            8 => seals >= 9000,
            9 => seals >= 10000,
            _ => false
        };
    }

    private async Task ProcessAllMarks(
            List<HuntMark> overworld,
            List<HuntMark> duty,
            bool gcLog,
            bool doDutyMarks,
            CancellationToken token)
    {
        await ProcessHuntMarks(overworld, true, GetRankInfo(gcLog), gcLog, token);

        if (gcLog && (!Configuration!.SkipDutyMarks || doDutyMarks))
        {
            ErrorThrowIf(!SubscriptionManager.IsInitialized(IPCNames.AutoDuty),
                         "AutoDuty not enabled/working! Skipping Duty Mobs.");

            await ProcessDutyMarks(duty, token);
        }
    }

    private async Task RankUp(CancellationToken token = default)
    {
        uint playerGC;
        unsafe
        {
            playerGC = PlayerState.Instance()->GrandCompany;
        }

        switch (playerGC)
        {
            case 1 when Svc.ClientState.TerritoryType == 128:
                await MoveTo(new Vector3(93f, 40f, 74f), false, token);
                break;
            case 2 when Svc.ClientState.TerritoryType == 132:
                await MoveTo(new Vector3(-68f, -0.5f, -7f), false, token);
                break;
            case 2 when Svc.ClientState.TerritoryType == 130:
                await MoveTo(new Vector3(-142f, 4f, -105f), false, token);
                break;
            default:
                Lifestream.ExecuteCommand("gc");
                await WaitPulseConditionAsync(() => Lifestream.IsBusy(), "Moving To GC", token);
                break;
        }

        uint baseId = playerGC switch
        {
            1 => 1002388,
            2 => 1002394,
            3 => 1002391
        };
        await InteractWithByBaseId(baseId, token);
        await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringApplyForPromotion), "Select Apply for promotion", token);
        await WaitUntilAsync(() => FireCallbackOnAddon("GrandCompanyRankUp", values: [0]), "Confirming rank up", token);
        await WaitWhileAsync(() => IsPlayerBusy, "Wait for player not busy", token);
    }

    internal enum BumpOnALogMessageType : ushort
    {
        FirstStatus,
        HunkMark,
        DutyQuest,
        GCProgress,
        Duty
    }

    internal record BumpOnALogMessage
    {
        public BumpOnALogMessageType Type { get; init; }
        public ulong ContentId { get; init; }
        public ushort WorldId { get; init; }
        public List<HuntMark>? HuntMarks { get; init; }
        public uint OpenDuty { get; init; }
        public int GCRank { get; init; }
    }
}
