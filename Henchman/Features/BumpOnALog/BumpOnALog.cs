using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Helpers;
using Henchman.Models;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        var currentRank = gcLog
                                  ? HuntLogHelper.GetGrandCompanyRankInfo()
                                  : HuntLogHelper.GetClassJobRankInfo();

        if (currentRank >
            (gcLog
                     ? 2
                     : 4))
            return;

        var huntMarks = GetHuntMarks(gcLog, currentRank);

        huntMarks = huntMarks.Where(x => x is { GetOpenMonsterNoteKills: > 0, IsDuty: false })
                             .OrderBy(x => x!.TerritoryId)
                             .ToList();
        await ProcessHuntMarks(huntMarks, true, currentRank, gcLog, token);
        ChatPrint("Completed all non-Duty mob entries!");
        await Lifestream.LifestreamReturn(C.ReturnTo, token);
    }

    private unsafe List<HuntMark?> GetHuntMarks(bool gcLog, int currentRank)
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

                                     return original ?? null;
                                 })
                         .Where(mark => mark != null)
                         .ToList();
    }
}
