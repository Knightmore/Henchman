using System.Linq;
using Henchman.Abstractions;
using Lumina.Excel.Sheets;

namespace Henchman.Features.BringYourXGame;

public class Configuration : IConfig
{
    public uint BRankToFarm;

    public SortedSet<uint> EnabledTerritoriesForARank = new(BRanks
                                                           .Where(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                          .GetRow(x.TerritoryId)
                                                                          .ExVersion.Value.RowId <=
                                                                       2)
                                                           .Select(x => x.TerritoryId));
}
