using System.Threading;
using System.Threading.Tasks;

namespace Henchman.Features.OnYourBGame;

internal class OnYourBGame
{
    internal async Task Start(CancellationToken token = default)
    {
        if (C.BRankToFarm > 0 && BRanks.TryGetValue(C.BRankToFarm, out var huntMark))
            await FarmBRank(huntMark, token);
        else
            Error("No valid B-Rank to farm picked!");
    }
}
