using Henchman.Models;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Threading.Tasks;

namespace Henchman.Data;

internal static class HuntDatabase
{
    internal static List<HuntMark> HuntMarks = [];
    internal static List<HuntMark> BRanks = [];

    internal static IEnumerable<ClassJob> HuntLogClasses;
    internal static Dictionary<uint, HuntLog> ClassHuntRanks = new();
    internal static Dictionary<uint, HuntLog> GcHuntRanks = new();

    internal static Dictionary<GrandCompany, Location> ArrHuntBoardLocations = new()
                                                                               {
                                                                                       {
                                                                                               GrandCompany.Maelstrom, new Location(93.19f, 40.24f, 61.89f, 128)
                                                                                       },
                                                                                       {
                                                                                               GrandCompany.OrderOfTheTwinAdder,
                                                                                               new Location(-76.34f, -0.50f, 1.44f, 132)
                                                                                       },
                                                                                       {
                                                                                               GrandCompany.ImmortalFlames,
                                                                                               new Location(-150.89f, 4.1f, -93.6f, 130)
                                                                                       }
                                                                               };

    internal static Dictionary<string, Location> ExpansionHuntBoardLocations = new()
                                                                               {
                                                                                       {
                                                                                               "Heavensward", new Location(72.97f, 23.97f, 20.28f, 418)
                                                                                       },
                                                                                       {
                                                                                               "Stormblood", new Location(-30.26f, 0.1f, -43.21f, 628)
                                                                                       },
                                                                                       {
                                                                                               "Shadowbringers", new Location(-84.60f, 0.20f, -90.72f, 819)
                                                                                       },
                                                                                       {
                                                                                               "Endwalker", new Location(29.58f, -15.65f, 100.22f, 962)
                                                                                       },
                                                                                       {
                                                                                               "Dawntrail", new Location(22.10f, -14.00f, 133.44f, 1185)
                                                                                       }
                                                                               };

    internal static Dictionary<string, uint> AethernetIdCloseToHuntboard = new()
                                                                           {
                                                                                   {
                                                                                           "Heavensward", 80
                                                                                   },
                                                                                   {
                                                                                           "Endwalker", 189
                                                                                   },
                                                                                   {
                                                                                           "Dawntrail", 221
                                                                                   }
                                                                           };

    internal static readonly List<string> HuntBoardOptions =
    [
            "A Realm Reborn1",
            "A Realm Reborn2",
            "Heavensward1",
            "Heavensward2",
            "Heavensward3",
            "Heavensward4",
            "Stormblood1",
            "Stormblood2",
            "Stormblood3",
            "Stormblood4",
            "Shadowbringers1",
            "Shadowbringers2",
            "Shadowbringers3",
            "Shadowbringers4",
            "Endwalker1",
            "Endwalker2",
            "Endwalker3",
            "Endwalker4",
            "Dawntrail1",
            "Dawntrail2",
            "Dawntrail3",
            "Dawntrail4"
    ];

    internal static readonly Dictionary<string, string> BillCategories = new()
                                                                         {
                                                                                 /*{ HuntBoardOptions[0], "Mark Bill" },
                                                                                 { HuntBoardOptions[1], "Elite Mark Bill" },
                                                                                 { HuntBoardOptions[2], "Level 1 Clan Bill" },
                                                                                 { HuntBoardOptions[3], "Level 2 Clan Bill" },
                                                                                 { HuntBoardOptions[4], "Level 3 Clan Bill" },
                                                                                 { HuntBoardOptions[5], "Elite Clan Bill" },
                                                                                 { HuntBoardOptions[6], "Level 1 Veteran Clan Bill" },
                                                                                 { HuntBoardOptions[7], "Level 2 Veteran Clan Bill" },
                                                                                 { HuntBoardOptions[8], "Level 3 Veteran Clan Bill" },
                                                                                 { HuntBoardOptions[9], "Elite Veteran Clan Bill" },
                                                                                 { HuntBoardOptions[10], "One-Nut Clan Bill" },
                                                                                 { HuntBoardOptions[11], "Two-Nut Clan Bill" },
                                                                                 { HuntBoardOptions[12], "Three-Nut Clan Bill" },
                                                                                 { HuntBoardOptions[13], "Elite Nut Clan Bill" },
                                                                                 { HuntBoardOptions[14], "Junior Guildship Bill" },
                                                                                 { HuntBoardOptions[15], "Associate Guildship Bill" },
                                                                                 { HuntBoardOptions[16], "Senior Guildship Bill" },
                                                                                 { HuntBoardOptions[17], "Elite Guildship Bill" },
                                                                                 { HuntBoardOptions[18], "Beginner Dawn Hunt Bill" },
                                                                                 { HuntBoardOptions[19], "Intermediate Dawn Hunt Bill" },
                                                                                 { HuntBoardOptions[20], "Advanced Dawn Hunt Bill" },
                                                                                 { HuntBoardOptions[21], "Elite Dawn Hunt Bill" }*/
                                                                                 { HuntBoardOptions[0], Svc.Data.GetExcelSheet<EventItem>().GetRow(2001361).Name.ExtractText() },
                                                                                 { HuntBoardOptions[1], Svc.Data.GetExcelSheet<EventItem>().GetRow(2001362).Name.ExtractText() },
                                                                                 { HuntBoardOptions[2], Svc.Data.GetExcelSheet<EventItem>().GetRow(2001700).Name.ExtractText() },
                                                                                 { HuntBoardOptions[3], Svc.Data.GetExcelSheet<EventItem>().GetRow(2001701).Name.ExtractText() },
                                                                                 { HuntBoardOptions[4], Svc.Data.GetExcelSheet<EventItem>().GetRow(2001702).Name.ExtractText() },
                                                                                 { HuntBoardOptions[5], Svc.Data.GetExcelSheet<EventItem>().GetRow(2001703).Name.ExtractText() },
                                                                                 { HuntBoardOptions[6], Svc.Data.GetExcelSheet<EventItem>().GetRow(2002113).Name.ExtractText() },
                                                                                 { HuntBoardOptions[7], Svc.Data.GetExcelSheet<EventItem>().GetRow(2002114).Name.ExtractText() },
                                                                                 { HuntBoardOptions[8], Svc.Data.GetExcelSheet<EventItem>().GetRow(2002115).Name.ExtractText() },
                                                                                 { HuntBoardOptions[9], Svc.Data.GetExcelSheet<EventItem>().GetRow(2002116).Name.ExtractText() },
                                                                                 { HuntBoardOptions[10], Svc.Data.GetExcelSheet<EventItem>().GetRow(2002628).Name.ExtractText() },
                                                                                 { HuntBoardOptions[11], Svc.Data.GetExcelSheet<EventItem>().GetRow(2002629).Name.ExtractText() },
                                                                                 { HuntBoardOptions[12], Svc.Data.GetExcelSheet<EventItem>().GetRow(2002630).Name.ExtractText() },
                                                                                 { HuntBoardOptions[13], Svc.Data.GetExcelSheet<EventItem>().GetRow(2002631).Name.ExtractText() },
                                                                                 { HuntBoardOptions[14], Svc.Data.GetExcelSheet<EventItem>().GetRow(2003090).Name.ExtractText() },
                                                                                 { HuntBoardOptions[15], Svc.Data.GetExcelSheet<EventItem>().GetRow(2003091).Name.ExtractText() },
                                                                                 { HuntBoardOptions[16], Svc.Data.GetExcelSheet<EventItem>().GetRow(2003092).Name.ExtractText() },
                                                                                 { HuntBoardOptions[17], Svc.Data.GetExcelSheet<EventItem>().GetRow(2003093).Name.ExtractText() },
                                                                                 { HuntBoardOptions[18], Svc.Data.GetExcelSheet<EventItem>().GetRow(2003509).Name.ExtractText() },
                                                                                 { HuntBoardOptions[19], Svc.Data.GetExcelSheet<EventItem>().GetRow(2003510).Name.ExtractText() },
                                                                                 { HuntBoardOptions[20], Svc.Data.GetExcelSheet<EventItem>().GetRow(2003511).Name.ExtractText() },
                                                                                 { HuntBoardOptions[21], Svc.Data.GetExcelSheet<EventItem>().GetRow(2003512).Name.ExtractText() }
                                                                         };

    internal static readonly Dictionary<string, string> HuntBoardSelect = new()
                                                                          {
                                                                                  { HuntBoardOptions[0], Lang.DailyHuntString(1) },
                                                                                  { HuntBoardOptions[1], Lang.DailyHuntString(2) },
                                                                                  { HuntBoardOptions[2], Lang.DailyHuntString(5) },
                                                                                  { HuntBoardOptions[3], Lang.DailyHuntString(6) },
                                                                                  { HuntBoardOptions[4], Lang.DailyHuntString(7) },
                                                                                  { HuntBoardOptions[5], Lang.DailyHuntString(8) },
                                                                                  { HuntBoardOptions[6], Lang.DailyHuntString(12) },
                                                                                  { HuntBoardOptions[7], Lang.DailyHuntString(13) },
                                                                                  { HuntBoardOptions[8], Lang.DailyHuntString(14) },
                                                                                  { HuntBoardOptions[9], Lang.DailyHuntString(15) },
                                                                                  { HuntBoardOptions[10], Lang.DailyHuntString(18) },
                                                                                  { HuntBoardOptions[11], Lang.DailyHuntString(19) },
                                                                                  { HuntBoardOptions[12], Lang.DailyHuntString(20) },
                                                                                  { HuntBoardOptions[13], Lang.DailyHuntString(21) },
                                                                                  { HuntBoardOptions[14], Lang.DailyHuntString(24) },
                                                                                  { HuntBoardOptions[15], Lang.DailyHuntString(25) },
                                                                                  { HuntBoardOptions[16], Lang.DailyHuntString(26) },
                                                                                  { HuntBoardOptions[17], Lang.DailyHuntString(27) },
                                                                                  { HuntBoardOptions[18], Lang.DailyHuntString(30) },
                                                                                  { HuntBoardOptions[19], Lang.DailyHuntString(31) },
                                                                                  { HuntBoardOptions[20], Lang.DailyHuntString(32) },
                                                                                  { HuntBoardOptions[21], Lang.DailyHuntString(33) }
                                                                          };

    internal static readonly Dictionary<GrandCompany, uint> GCHuntBoardIds = new()
                                                                             {
                                                                                     { GrandCompany.Maelstrom, 2004438 },
                                                                                     { GrandCompany.OrderOfTheTwinAdder, 2004439 },
                                                                                     { GrandCompany.ImmortalFlames, 2004440 }
                                                                             };

    internal static readonly Dictionary<string, uint> HuntBoardIds = new()
                                                                     {
                                                                             { "A Realm Reborn", 0 },
                                                                             { "Heavensward", 2005909 },
                                                                             { "Stormblood", 2008655 },
                                                                             { "Shadowbringers", 2010340 },
                                                                             { "Endwalker", 2012236 },
                                                                             { "Dawntrail", 2014155 }
                                                                     };

    internal static List<MobHuntOrderType> GetCorrectedMobHuntOrderTypes()
    {
        var mobHuntOrderType = Svc.Data.GetExcelSheet<MobHuntOrderType>()
                                  .ToList();
        mobHuntOrderType.Insert(1, mobHuntOrderType[4]);
        mobHuntOrderType.RemoveAt(5);
        return mobHuntOrderType;
    }

    /*
     * The `A Realm Reborn Elite` was added after Heavensward
     */
    internal static int GetTranslatedMobHuntOrderType(uint mobHuntOrderTypRowId)
    {
        switch (mobHuntOrderTypRowId)
        {
            case 1:
                return 4;
            case 2:
                return 1;
            case 3:
                return 2;
            case 4:
                return 3;
            default:
                return (int)mobHuntOrderTypRowId;
        }
    }

    internal static IEnumerable<HuntMark> GetHuntMarksByName(uint bnpcNameRowId) => HuntMarks.Where(x => x.BNpcNameRowId == bnpcNameRowId);

    internal static IEnumerable<HuntMark> GetHuntMarksByNameAndLevel(uint bnpcNameRowId, byte level) => HuntMarks.Where(x => x.BNpcNameRowId == bnpcNameRowId && x.Level == level);

    internal static HuntMark? GetHuntMark(uint bnpcNameRowId, uint territoryId, uint fateId = 0) => HuntMarks.FirstOrDefault(x => x.BNpcNameRowId == bnpcNameRowId && x.TerritoryId == territoryId && x.FateId == fateId);

    internal static HuntMark? GetHuntMarkForExpansion(uint bnpcNameRowId, uint exVersionRowId) =>
            GetHuntMarksByName(bnpcNameRowId)
                   .FirstOrDefault(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                          .GetRow(x.TerritoryId)
                                          .ExVersion.RowId ==
                                        exVersionRowId);

    internal static HuntMark ResolveBestLevelVariant(HuntMark source, int playerLevel, bool useLowest = true, bool preferOverworldNonFate = false)
    {
        var candidates = GetHuntMarksByName(source.BNpcNameRowId)
                        .Where(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                       .GetRow(x.TerritoryId)
                                       .ExVersion.RowId ==
                                    Svc.Data.GetExcelSheet<TerritoryType>()
                                       .GetRow(source.TerritoryId)
                                       .ExVersion.RowId)
                        .ToList();

        var preferredCandidates = preferOverworldNonFate
                                          ? candidates.Where(x => !x.IsDuty && x.FateId == 0).ToList()
                                          : candidates.Where(x => x.IsDuty == source.IsDuty &&
                                                                 (source.FateId > 0 ? x.FateId > 0 : x.FateId == 0))
                                                      .ToList();

        var best = SelectBestLevelVariant(preferredCandidates, playerLevel, useLowest) ??
                   SelectBestLevelVariant(candidates, playerLevel, useLowest) ??
                   candidates.FirstOrDefault() ??
                   source;

        return new HuntMark(best)
        {
                TargetStateSource = source,
                NeededKills = source.NeededKills,
                MobHuntRowId = source.MobHuntRowId,
                MobHuntSubRowId = source.MobHuntSubRowId,
                MonsterNoteId = source.MonsterNoteId,
                MonsterNoteSubRank = source.MonsterNoteSubRank,
                MonsterNoteCount = source.MonsterNoteCount,
                IsCurrentTarget = source.IsCurrentTarget
        };
    }

    private static HuntMark? SelectBestLevelVariant(List<HuntMark> candidates, int playerLevel, bool useLowest)
    {
        var leveledCandidates = candidates.Where(x => x.Level != null)
                                          .ToList();
        return (useLowest
                       ? leveledCandidates.OrderBy(x => x.Level).FirstOrDefault()
                       : leveledCandidates.Where(x => x.Level <= playerLevel)
                                          .OrderByDescending(x => x.Level)
                                          .FirstOrDefault() ??
                         leveledCandidates.OrderBy(x => Math.Abs(x.Level!.Value - playerLevel))
                                          .FirstOrDefault()) ??
               candidates.FirstOrDefault();
    }

    internal static HuntMark? GetBRank(uint bnpcNameRowId) => BRanks.FirstOrDefault(x => x.BNpcNameRowId == bnpcNameRowId);

    internal static void ProcessHuntMarkJson(string filePath, List<HuntMark> marks)
    {
        try
        {
            var jsonMarks = ReadLocalJsonFile<List<JsonHuntMark>>(filePath);

            if (jsonMarks != null)
            {
                foreach (var jsonMark in jsonMarks)
                {
                    var matchingMarks = marks.Where(x => x.BNpcNameRowId == jsonMark.BnpcName &&
                                                         x.TerritoryId == jsonMark.TerritoryId &&
                                                         x.FateId == jsonMark.FateId);
                    var huntMark = jsonMark.Level == null
                                           ? matchingMarks.FirstOrDefault()
                                           : matchingMarks.FirstOrDefault(x => x.Level == jsonMark.Level) ??
                                             matchingMarks.FirstOrDefault(x => x.Level == null);
                    if (huntMark == null)
                    {
                        marks.Add(new HuntMark(jsonMark.BnpcName, jsonMark.X, jsonMark.Y, jsonMark.Z, jsonMark.TerritoryId, jsonMark.FateId, jsonMark.Level));
                        continue;
                    }

                    if (huntMark.Level == null)
                        huntMark.Level = jsonMark.Level;

                    var position = new Vector3(jsonMark.X, jsonMark.Y, jsonMark.Z);
                    if (!huntMark.Positions.Contains(position)) huntMark.Positions.Add(position);
                }
            }
        }
        catch (Exception e)
        {
            FullError($"Could not process HuntMarks: {e.Message}");
        }
    }

    internal static async Task PopulateMarks()
    {
        try
        {
            HuntMarks = [];
            BRanks = [];
            ClassHuntRanks = new Dictionary<uint, HuntLog>();
            GcHuntRanks = new Dictionary<uint, HuntLog>();

            ProcessHuntMarkJson("ARRHunt.json", HuntMarks);
            ProcessHuntMarkJson("HWHunt.json", HuntMarks);
            ProcessHuntMarkJson("StBHunt.json", HuntMarks);
            ProcessHuntMarkJson("SHBHunt.json", HuntMarks);
            ProcessHuntMarkJson("EWHunt.json", HuntMarks);
            ProcessHuntMarkJson("DTHunt.json", HuntMarks);
            ProcessHuntMarkJson("BRanks.json", HuntMarks);

            ProcessHuntMarkJson("BRanks.json", BRanks);
        }
        catch (Exception e)
        {
            FullError($"Could not populate HuntMarks: {e.Message}");
        }
    }

    internal static void PopulateClassHuntLogs()
    {
        HuntLogClasses = Svc.Data.GetExcelSheet<ClassJob>()
                            .DistinctBy(x => x.MonsterNote.RowId)
                            .Where(x => x.MonsterNote.RowId != 127 && x.MonsterNote.RowId < 12);

        foreach (var huntClass in HuntLogClasses)
        {
            var classHuntLog = new HuntLog();
            var rowBase = (int)huntClass.RowId * 10000;

            for (var rankNumber = 0; rankNumber < 5; rankNumber++)
            {
                var entryBase = rowBase + (rankNumber * 10) + 1;
                var count = 0;
                var subRank = 0;
                for (var rankEntry = entryBase; rankEntry <= entryBase + 9; rankEntry++)
                {
                    var rankEntryRow = Svc.Data.GetExcelSheet<MonsterNote>()
                                          .GetRow((uint)rankEntry);
                    for (var i = 0; i < 4; i++)
                    {
                        var monsterTarget = rankEntryRow.MonsterNoteTarget[i].Value;
                        if (GetHuntMarkForExpansion(monsterTarget.BNpcName.Value.RowId, 0) is { } huntMark)
                        {
                            var newHuntMark = new HuntMark(huntMark)
                            {
                                NeededKills = rankEntryRow.Count[i],
                                MonsterNoteId = (int)huntClass.MonsterNote.RowId,
                                MonsterNoteSubRank = subRank,
                                MonsterNoteCount = i
                            };

                            classHuntLog.HuntMarks[rankNumber, count] = newHuntMark;
                        }
                        else
                            classHuntLog.HuntMarks[rankNumber, count] = null;

                        count++;
                    }

                    subRank++;
                }
            }

            ClassHuntRanks.Add(huntClass.MonsterNote.RowId, classHuntLog);
        }
    }

    internal static void PopulateGcHuntLogs()
    {
        var grandCompanies = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.GrandCompany>()
                                .Where(x => x.RowId > 0);

        foreach (var gc in grandCompanies)
        {
            var gcHuntLog = new HuntLog();
            var rowBase = gc.RowId * 1000000;
            for (uint rankNumber = 0; rankNumber < 5; rankNumber++)
            {
                var entryBase = rowBase + (rankNumber * 10) + 1;
                var count = 0;
                var subRank = 0;
                for (var rankEntry = entryBase; rankEntry <= entryBase + 9; rankEntry++)
                {
                    var rankEntryRow = Svc.Data.GetExcelSheet<MonsterNote>()
                                          .GetRow(rankEntry);
                    for (var i = 0; i < 4; i++)
                    {
                        var monsterTarget = rankEntryRow.MonsterNoteTarget[i].Value;
                        if (GetHuntMarkForExpansion(monsterTarget.BNpcName.Value.RowId, 0) is { } huntMark)
                        {
                            var newHuntMark = new HuntMark(huntMark)
                            {
                                NeededKills = rankEntryRow.Count[i],
                                MonsterNoteId = (int)gc.MonsterNote.RowId,
                                MonsterNoteSubRank = subRank,
                                MonsterNoteCount = i
                            };

                            gcHuntLog.HuntMarks[rankNumber, count] = newHuntMark;
                        }
                        else
                            gcHuntLog.HuntMarks[rankNumber, count] = null;

                        count++;
                    }

                    subRank++;
                }
            }

            GcHuntRanks.Add(gc.RowId, gcHuntLog);
        }
    }

    internal static void Initialize()
    {
        PopulateMarks();
        PopulateClassHuntLogs();
        PopulateGcHuntLogs();
    }

    internal static void ResetCurrentTarget()
    {
        foreach (var huntMarksValue in HuntMarks) huntMarksValue.IsCurrentTarget = false;

        foreach (var bRanksValue in BRanks) bRanksValue.IsCurrentTarget = false;

        foreach (var huntLog in ClassHuntRanks.Values)
        {
            for (var row = 0; row < huntLog.HuntMarks.GetLength(0); row++)
            {
                for (var col = 0; col < huntLog.HuntMarks.GetLength(1); col++)
                {
                    var mark = huntLog.HuntMarks[row, col];

                    if (mark != null) mark.IsCurrentTarget = false;
                }
            }
        }

        foreach (var huntLog in GcHuntRanks.Values)
        {
            for (var row = 0; row < huntLog.HuntMarks.GetLength(0); row++)
            {
                for (var col = 0; col < huntLog.HuntMarks.GetLength(1); col++)
                {
                    var mark = huntLog.HuntMarks[row, col];

                    if (mark != null) mark.IsCurrentTarget = false;
                }
            }
        }
    }

    internal enum GrandCompany
    {
        Maelstrom = 1,
        OrderOfTheTwinAdder,
        ImmortalFlames
    }
}
