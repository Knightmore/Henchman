using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using GrandCompany = Lumina.Excel.Sheets.GrandCompany;

namespace Henchman.Helpers;

internal static class HuntLogHelper
{
    internal static unsafe int GetGrandCompanyRankInfo()
    {
        var gcMonsterNoteId = Svc.Data.GetExcelSheet<GrandCompany>()
                                 .GetRow((byte)Player.GrandCompany)
                                 .MonsterNote.RowId.ToInt();

        var gcMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[gcMonsterNoteId];

        return gcMonsterNoteRankInfo.Rank;
    }

    internal static unsafe int GetClassJobRankInfo()
    {
        var classMonsterNoteId = Svc.Data.GetExcelSheet<ClassJob>()
                                    .GetRow(Player.ClassJob.RowId)
                                    .MonsterNote.RowId.ToInt();
        var classMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[classMonsterNoteId];

        return classMonsterNoteRankInfo.Rank;
    }
}
