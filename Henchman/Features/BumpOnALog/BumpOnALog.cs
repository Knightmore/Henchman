using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Data;
using Henchman.Models;
using Lumina.Excel.Sheets;
using Underlings.GameHelpers;
using AutoDuty = Underlings.IPC.AutoDuty;
using GrandCompany = Lumina.Excel.Sheets.GrandCompany;
using Module = Underlings.Modules.Module;

namespace Henchman.Features.BumpOnALog;

public partial class BumpOnALog : Module
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

    internal async Task ProcessSingleMark(HuntMark mark, bool gcLog, CancellationToken token = default)
    {
        if (!IsCombat(Player.ClassJob.RowId))
        {
            Chat.Warning("You do not have equipped a combat class!");
            return;
        }

        var resolvedMark = ResolveSingleMarkVariant(mark, gcLog);
        await ProcessHuntMarks([resolvedMark], true, GetRankInfo(gcLog), gcLog, token);
    }

    private static HuntMark ResolveSingleMarkVariant(HuntMark mark, bool gcLog)
    {
        var currentTerritory = Svc.ClientState.TerritoryType;
        var currentTerritoryVariants = GetHuntMarksByName(mark.BNpcNameRowId)
                                      .Where(x => x.TerritoryId == currentTerritory && !x.IsDuty)
                                      .ToList();

        var localVariant = currentTerritoryVariants.FirstOrDefault(x => x.FateId == 0) ??
                           (!C.SkipFateMarks
                                    ? currentTerritoryVariants.FirstOrDefault()
                                    : null);

        return localVariant != null
                       ? CreateProgressVariant(localVariant, mark)
                       : ResolveBestLevelVariant(mark, Svc.PlayerState.Level, preferOverworldNonFate: !gcLog);
    }

    private async Task Process(bool gcLog, bool doDutyMarks = false, CancellationToken token = default)
    {
        if (!IsCombat(Player.ClassJob.RowId))
        {
            Chat.Warning("You do not have equipped a combat class!");
            return;
        }

        var logId = gcLog ? GetGrandCompany() : Player.ClassJob.Value.MonsterNote.RowId;
        var logs = gcLog ? GcHuntRanks : ClassHuntRanks;
        if (!logs.TryGetValue(logId, out var huntLog))
        {
            FullWarning(gcLog ? "This character has no Grand Company hunting log." : "This class or job has no hunting log.");
            return;
        }
        var rankCount = huntLog.HuntMarks.GetLength(0);
        if (GetRankInfo(gcLog) >= rankCount) return;

        if (!AutoRotation.CheckForAvailability(C.AutoRotationPlugin)) return;

        if (!gcLog)
        {
            while (GetRankInfo(gcLog) < Math.Min(Configuration!.StopAfterJobRank + 1, rankCount))
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
                                    .Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: false, FateId: 0 })
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
            const int maxAutomatedGcRank  = 9;
            const int maxGcLoopIterations = 10;

            var configuredStopRank = Math.Min(Configuration!.StopAfterGCRank + 1, maxAutomatedGcRank);
            var currentGcRank      = GetGrandCompanyRank();

            TaskLog.Verbose($"GrandCompanyRank {currentGcRank} | {configuredStopRank}");

            if (Configuration.IsAboveGCStoppingRank(currentGcRank))
            {
                FullWarning($"Current GC rank {currentGcRank} is above the configured stopping rank {configuredStopRank}. Increase 'Stop after GC rank' to run promotion prerequisites.");
                return;
            }

            if (currentGcRank > maxAutomatedGcRank)
            {
                await ProcessOverRankedGcLogAsync(currentGcRank, doDutyMarks, token);
            }
            else
            {
                var gcLoopIterations = 0;
                while (GetGrandCompanyRank() <= configuredStopRank && gcLoopIterations++ < maxGcLoopIterations)
                {
                    Log.Information($"{configuredStopRank} -> {GetRankInfo(gcLog)} | {GetGrandCompanyRank() < configuredStopRank}");
                    TaskLog.Verbose("Below second threshold");
                    var rank      = GetRankInfo(gcLog);
                    if (rank >= rankCount) break;
                    var huntMarks = GetHuntMarks(gcLog, rank);

                    var overworldMarks = huntMarks
                                        .Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: false, FateId: 0 })
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
                    else
                    {
                        TaskLog.Warning($"Unsupported GC rank {gcRank}; stopping GC rank processing");
                        break;
                    }
                }

                if (gcLoopIterations > maxGcLoopIterations)
                    TaskLog.Warning("Stopped GC rank processing after reaching the safety iteration limit");
            }
        }

        Chat.Info("Completed all selected mob entries!");
        await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
    }

    private async Task ProcessOverRankedGcLogAsync(int gcRank, bool doDutyMarks, CancellationToken token)
    {
        var currentGcLogRank = GetCurrentGcLogRank();
        var requiredGcRank = currentGcLogRank switch
                             {
                                     0 => 1,
                                     1 => 5,
                                     2 => 9,
                                     _ => int.MaxValue
                             };

        if (gcRank < requiredGcRank)
            return;

        TaskLog.Verbose($"Processing overdue GC hunt log {currentGcLogRank + 1} at GC rank {gcRank}");

        var huntMarks = GetHuntMarks(true, currentGcLogRank);
        var overworldMarks = huntMarks
                            .Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: false, FateId: 0 })
                            .ToList();
        var dutyMarks = huntMarks
                       .Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: true })
                       .OrderBy(x => x.TerritoryId)
                       .ToList();

        await ProcessAllMarks(overworldMarks, dutyMarks, true, doDutyMarks, token);
    }

    private async Task<bool> HandleGcRankAsync(
            int               gcRank,
            List<HuntMark>    overworldMarks,
            List<HuntMark>    dutyMarks,
            bool              doDutyMarks,
            CancellationToken token)
    {
        TaskLog.Verbose($"Handle current GC Rank {GetGrandCompanyRank()} - Log {GetCurrentGcLogRank()}");

        var currentGcLogRank = GetCurrentGcLogRank();

        if ((currentGcLogRank == 0 && GetGrandCompanyRank() <= 4) || (currentGcLogRank == 1 && GetGrandCompanyRank() is >= 5 and <= 8) || (currentGcLogRank == 2 && GetGrandCompanyRank() >= 9)) await ProcessAllMarks(overworldMarks, dutyMarks, true, doDutyMarks, token);

        if (Configuration!.AutoGCRankUp && gcRank < Math.Min(Configuration.StopAfterGCRank + 1, 9))
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
                                                    : ResolveBestLevelVariant(original, Svc.PlayerState.Level, preferOverworldNonFate: true);
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
                       8 => AurumDataRank8[GetGrandCompany()   - 1],
                       _ => (0, 0)
               };
    }

    private async Task HandleGcQuestAsync(CancellationToken token)
    {
        var (questId, dutyId) = GetGcQuest();
        if (questId == 0 || QuestManager.IsQuestComplete(questId)) return;

        ErrorThrowIf(!SubscriptionManager.IsLoaded(IPCNames.Questionable), "Questionable not enabled! Cannot complete GC promotion quests.");
        await CompleteGcDungeonUnlockAsync(dutyId, token);
        while (!QuestManager.IsQuestComplete(questId))
        {
            token.ThrowIfCancellationRequested();
            if (QuestManager.GetQuestSequence(questId) == 2)
            {
                ErrorThrowIf(!SubscriptionManager.IsLoaded(IPCNames.AutoDuty), "AutoDuty not enabled! Cannot run the GC promotion dungeon.");
                ErrorThrowIf(!Questionable.Stop.Invoke("GC promotion dungeon handoff"), "Could not stop Questionable before starting AutoDuty.");
                if (AutoDuty.IsStopped.Invoke())
                {
                    if (C.SoloUnsyncLogDuty)
                        IPC.AutoDuty.RunDutyUnsync(dutyId);
                    else
                        AutoDuty.RunDutySupport(dutyId);
                    await WaitUntilAsync(() => !AutoDuty.IsStopped.Invoke() || QuestManager.IsQuestComplete(questId) ||
                                               QuestManager.GetQuestSequence(questId) != 2,
                                         "Waiting for AutoDuty to start GC promotion dungeon", token, TimeSpan.FromSeconds(15));
                }
                await WaitUntilAsync(() => AutoDuty.IsStopped.Invoke(), "Waiting for Duty to finish", token);
                ErrorThrowIf(!QuestManager.IsQuestComplete(questId) && QuestManager.GetQuestSequence(questId) == 2,
                             $"AutoDuty stopped before completing GC promotion quest {questId}'s dungeon objective.");
            }
            else
                await ProgressGcQuestAsync(questId, token);
        }
    }

    private static async Task CompleteGcDungeonUnlockAsync(uint dutyId, CancellationToken token)
    {
        var unlockQuest = dutyId switch
                               {
                                       1330 => 66515u,
                                       1331 => 66550u,
                                       _ => 0u
                               };
        if (unlockQuest == 0 || QuestManager.IsQuestComplete(unlockQuest)) return;

        // GC quest scripts accept this prerequisite again if it is only abandoned.
        ErrorThrowIf(!SubscriptionManager.IsLoaded(IPCNames.Questionable), "Questionable not enabled! Cannot complete GC dungeon unlock quests.");
        try
        {
            TextAdvance.SetTemporary();
            ErrorThrowIf(!Questionable.StartSingleQuest.Invoke((unlockQuest - 65536).ToString()),
                         $"Questionable could not start dungeon unlock quest {unlockQuest}.");
            await WaitUntilAsync(() => QuestManager.IsQuestComplete(unlockQuest), $"Completing GC dungeon unlock quest {unlockQuest}", token);
        }
        finally
        {
            Questionable.Stop.Invoke("GC dungeon unlock handoff");
            TextAdvance.UnsetTemporary();
        }
    }

    private async Task ProgressGcQuestAsync(uint questId, CancellationToken token)
    {
        var sequence = QuestManager.GetQuestSequence(questId);
        TextAdvance.SetTemporary();
        try
        {
            ErrorThrowIf(!Questionable.StartSingleQuest.Invoke((questId - 65536).ToString()),
                         $"Questionable could not start GC promotion quest {questId}.");
            await WaitUntilAsync(() => QuestManager.IsQuestComplete(questId) || QuestManager.GetQuestSequence(questId) == 2 ||
                                       !Questionable.IsRunning.Invoke(),
                                 $"Questionable progressing GC promotion quest {questId}", token);
            ErrorThrowIf(!QuestManager.IsQuestComplete(questId) && QuestManager.GetQuestSequence(questId) == sequence,
                         $"Questionable stopped without advancing GC promotion quest {questId} (sequence {sequence}).");
        }
        finally
        {
            Questionable.Stop.Invoke("GC promotion quest handoff");
            TextAdvance.UnsetTemporary();
        }
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
        var questId = GetGcQuest().questId;
        if (questId != 0 && !QuestManager.IsQuestComplete(questId)) return false;
        if ((GetGrandCompanyRank() == 4 && GetCurrentGcLogRank() < 1) ||
            (GetGrandCompanyRank() == 8 && GetCurrentGcLogRank() < 2)) return false;

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
            List<HuntMark>    overworld,
            List<HuntMark>    duty,
            bool              gcLog,
            bool              doDutyMarks,
            CancellationToken token)
    {
        await ProcessHuntMarks(overworld, true, GetRankInfo(gcLog), gcLog, token);

        if (gcLog && (!Configuration!.SkipDutyMarks || doDutyMarks))
        {
            ErrorThrowIf(!SubscriptionManager.IsLoaded(IPCNames.AutoDuty),
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
                Lifestream.ExecuteCommand.Invoke("gc");
                await WaitPulseConditionAsync(() => Lifestream.IsBusy.Invoke(), "Moving To GC", token);
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
        public BumpOnALogMessageType Type      { get; init; }
        public ulong                 ContentId { get; init; }
        public ushort                WorldId   { get; init; }
        public List<HuntMark>?       HuntMarks { get; init; }
        public uint                  OpenDuty  { get; init; }
        public int                   GCRank    { get; init; }
    }
}
