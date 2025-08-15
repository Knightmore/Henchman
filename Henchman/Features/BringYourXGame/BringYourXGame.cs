using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI;
using Henchman.Models;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                                 .Where(x => C.EnabledTerritoriesForARank.Contains(x.Key))
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
            FullError("No valid B-Rank to farm picked!");
    }

    private static async Task FarmARank(Dictionary<uint, List<Vector3>> ARankPositions, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        foreach (var territory in ARankPositions)
        {
            Verbose($"Look for A-Rank in: {territory.Key} - {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territory.Key).PlaceName.Value.Name.ExtractText()}");
            if (Player.Territory != territory.Key)
            {
                var closestAetheryte = GetAetheryte(territory.Key, territory.Value[0]);

                if (closestAetheryte > 0)
                    await TeleportTo(closestAetheryte, token);
                else
                {
                    ErrorThrowIf(closestAetheryte == 0 || !IsAetheryteUnlocked(closestAetheryte),
                                 $"You aren't attuned to any Aetheryte for the territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territory.Key).PlaceName.Value.Name.ExtractText()} ({territory.Key})");
                }

                // TODO: Switch to MappingTheRealm once/if ever released.
                if (Player.Territory == 478)
                {
                    Verbose("In Idyllshire");
                    await MoveToNextZone(new Vector3(164f, 207f, 129f), 399, token);
                }
            }

            await RoamUntilTargetNearby(territory.Value, int.MaxValue, false, true, 10, token);
        }
        ChatPrintInfo("Completed all selected mob A-Rank territories!");
        await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
    }

    private static async Task FarmBRank(HuntMark huntMark, CancellationToken token = default)
    {
        var retries = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            ErrorThrowIf(retries == 3, "Emergency Stop. Player already died three times");

            Verbose($"Try: {retries}");
            if (Player.Territory != huntMark.TerritoryId)
            {
                if (!huntMark.Positions.TryGetFirst(out var markPosition))
                {
                    FullError($"HuntMark {huntMark.Name} has no valid position!");
                    break;
                }

                var closestAetheryte = GetAetheryte(huntMark.TerritoryId, markPosition);

                if (closestAetheryte > 0)
                    await TeleportTo(closestAetheryte, token);
                else
                {
                    ErrorThrowIf(closestAetheryte == 0 || !IsAetheryteUnlocked(closestAetheryte),
                                 $"You aren't attuned to any Aetheryte for the Hunt Marks territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(huntMark.TerritoryId).PlaceName.Value.Name.ExtractText()} ({huntMark.TerritoryId})");
                }

                // TODO: Switch to MappingTheRealm once/if ever released.
                if (Player.Territory == 478)
                {
                    Verbose("In Idyllshire");
                    await MoveToNextZone(new Vector3(164f, 207f, 129f), 399, token);
                }
            }

            if (!await RoamUntilTargetNearby(huntMark.Positions, huntMark.BNpcNameRowId, false, C.DetourForARanks, token: token))
            {
                retries++;
                continue;
            }

            var killResult = await KillHuntMark(huntMark, token);

            if (killResult == KillResult.Died) retries++;
        }
    }
}
