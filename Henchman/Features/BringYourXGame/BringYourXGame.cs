using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace Henchman.Features.OnYourBGame;

internal class BringYourXGame
{
    internal async Task StartA(CancellationToken token = default)
    {
        var aggregatedPositions = BRanks
                                 .Values
                                 .Where(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                .GetRow(x.TerritoryId)
                                                .ExVersion.Value.RowId <=
                                             2)
                                 .GroupBy(x => x.TerritoryId)
                                 .ToDictionary(
                                               group => group.Key,
                                               group => group.SelectMany(h => h.Positions)
                                                             .ToList()
                                              );

        await FarmARank(aggregatedPositions, token);
    }

    internal async Task StartB(CancellationToken token = default)
    {
        if (C.BRankToFarm > 0 && BRanks.TryGetValue(C.BRankToFarm, out var huntMark))
            await FarmBRank(huntMark, token);
        else
            Error("No valid B-Rank to farm picked!");
    }
}
