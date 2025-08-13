using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommons.GameHelpers;
using Henchman.Helpers;
using Henchman.Models;
using Lumina.Excel.Sheets;

namespace Henchman.Features.BumpOnALog;

internal class BumpOnALog
{
    internal async Task StartGCRank(CancellationToken token = default)
    {
        await Process(true, token);
    }

    internal async Task StartClassRank(CancellationToken token = default)
    {
        await Process(false, token);
    }


    private async Task Process(bool gcLog, CancellationToken token)
    {
        byte grandCompany;
        var currentRank = gcLog
                                  ? HuntLogHelper.GetGrandCompanyRankInfo()
                                  : HuntLogHelper.GetClassJobRankInfo();

        if (currentRank >
            (gcLog
                     ? 2
                     : 4))
            return;

        var huntMarks = GetHuntMarks(gcLog, currentRank);

        var overworldMarks = huntMarks.Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: false })
                                      .OrderBy(x => x!.TerritoryId)
                                      .ToList();
        var dutyMarks = huntMarks.Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: true })
                                 .OrderBy(x => x!.TerritoryId)
                                 .ToList();
        await ProcessHuntMarks(overworldMarks, true, currentRank, gcLog, token);
        if (gcLog && !C.SkipDutyMarks)
        {
            if(SubscriptionManager.IsInitialized(IPCNames.AutoDuty))
                await ProcessDutyMarks(dutyMarks, token);
            else
                Warning("AutoDuty not enabled! Skipping Duty Mobs.");
        }


        /*while(currentRank <= (gcLog ? C.StopAfterGCRank : C.StopAfterJobRank))
        {
            var huntMarks = GetHuntMarks(gcLog, currentRank);

            var overWorldMarks = huntMarks.Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: false })
                                          .OrderBy(x => x!.TerritoryId)
                                          .ToList();
            var dutyMarks = huntMarks.Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: true })
                                     .OrderBy(x => x!.TerritoryId)
                                     .ToList();
            await ProcessHuntMarks(overWorldMarks, true, currentRank, gcLog, token);

            if (gcLog)
                if( !C.SkipDutyMarks)
                    await ProcessDutyMarks(dutyMarks, token);
                else if (GetHuntMarks(gcLog, currentRank)
                                .Count ==
                         0)
                {

                }
        }*/

        ChatPrintInfo("Completed all selected mob entries!");
        await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
    }

    private List<HuntMark?> GetHuntMarks(bool gcLog, int currentRank)
    {
        return Enumerable.Range(0, gcLog
                                           ? GcHuntRanks[(byte)Player.GrandCompany]
                                            .HuntMarks.GetLength(1)
                                           : ClassHuntRanks[(uint)Svc.Data.GetExcelSheet<ClassJob>()
                                                                     .GetRow(Player.JobId)
                                                                     .MonsterNote.RowId.ToInt()]
                                            .HuntMarks.GetLength(1))
                         .Select(col =>
                                 {
                                     var original = gcLog
                                                            ? GcHuntRanks[(byte)Player.GrandCompany]
                                                                   .HuntMarks[currentRank, col]
                                                            : ClassHuntRanks[(uint)Svc.Data.GetExcelSheet<ClassJob>()
                                                                                      .GetRow(Player.JobId)
                                                                                      .MonsterNote.RowId.ToInt()]
                                                                   .HuntMarks[currentRank, col];

                                     return original ?? null;
                                 })
                         .Where(mark => mark != null)
                         .ToList();
    }
}
