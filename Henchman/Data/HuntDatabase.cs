using System.Linq;
using System.Threading.Tasks;
using Henchman.Helpers;
using Henchman.Models;
using Lumina.Excel.Sheets;

namespace Henchman.Data;

internal static class HuntDatabase
{
    internal static Dictionary<uint, HuntMark> HuntMarks = new();
    internal static Dictionary<uint, HuntMark> BRanks    = new();

    internal static IEnumerable<ClassJob>     HuntLogClasses;
    internal static Dictionary<uint, HuntLog> ClassHuntRanks = new();
    internal static Dictionary<uint, HuntLog> GcHuntRanks    = new();

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
                                                                        },
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
                                                                                 { HuntBoardOptions[0], "Mark Bill" },
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
                                                                                 { HuntBoardOptions[21], "Elite Dawn Hunt Bill" }
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

    internal static void ProcessHuntMarkJson(string filePath, Dictionary<uint, HuntMark> marksDict)
    {
        try
        {
            var marks = Utils.ReadLocalJsonFile<List<JsonHuntMark>>(filePath);

            if (marks != null)
            {
                foreach (var jsonMark in marks)
                {
                    if (!marksDict.TryGetValue(jsonMark.BnpcName, out var huntMark))
                        marksDict[jsonMark.BnpcName] = new HuntMark(jsonMark.BnpcName, jsonMark.X, jsonMark.Y, jsonMark.Z, jsonMark.TerritoryId, jsonMark.FateId);
                    else if (huntMark.TerritoryId == jsonMark.TerritoryId)
                        huntMark.Positions.Add(new Vector3(jsonMark.X, jsonMark.Y, jsonMark.Z));
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
            HuntMarks      = new Dictionary<uint, HuntMark>();
            BRanks         = new Dictionary<uint, HuntMark>();
            ClassHuntRanks = new Dictionary<uint, HuntLog>();
            GcHuntRanks    = new Dictionary<uint, HuntLog>();

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
            var rowBase      = (int)huntClass.RowId * 10000;

            for (var rankNumber = 0; rankNumber < 5; rankNumber++)
            {
                var entryBase = rowBase + (rankNumber * 10) + 1;
                var count     = 0;
                var subRank   = 0;
                for (var rankEntry = entryBase; rankEntry <= entryBase + 9; rankEntry++)
                {
                    var rankEntryRow = Svc.Data.GetExcelSheet<MonsterNote>()
                                          .GetRow((uint)rankEntry);
                    for (var i = 0; i < 4; i++)
                    {
                        var monsterTarget = rankEntryRow.MonsterNoteTarget[i].Value;
                        if (HuntMarks.TryGetValue(monsterTarget.BNpcName.Value.RowId, out var huntMark))
                        {
                            var newHuntMark = new HuntMark(huntMark)
                                              {
                                                      NeededKills        = rankEntryRow.Count[i],
                                                      MonsterNoteId      = (int)huntClass.MonsterNote.RowId,
                                                      MonsterNoteSubRank = subRank,
                                                      MonsterNoteCount   = i
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
            var rowBase   = gc.RowId * 1000000;
            for (uint rankNumber = 0; rankNumber < 5; rankNumber++)
            {
                var entryBase = rowBase + (rankNumber * 10) + 1;
                var count     = 0;
                var subRank   = 0;
                for (var rankEntry = entryBase; rankEntry <= entryBase + 9; rankEntry++)
                {
                    var rankEntryRow = Svc.Data.GetExcelSheet<MonsterNote>()
                                          .GetRow(rankEntry);
                    for (var i = 0; i < 4; i++)
                    {
                        var monsterTarget = rankEntryRow.MonsterNoteTarget[i].Value;
                        if (HuntMarks.TryGetValue(monsterTarget.BNpcName.Value.RowId, out var huntMark))
                        {
                            var newHuntMark = new HuntMark(huntMark)
                                              {
                                                      NeededKills        = rankEntryRow.Count[i],
                                                      MonsterNoteId      = (int)gc.MonsterNote.RowId,
                                                      MonsterNoteSubRank = subRank,
                                                      MonsterNoteCount   = i
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
        foreach (var huntMarksValue in HuntMarks.Values)
        {
            huntMarksValue.IsCurrentTarget = false;
        }

        foreach (var bRanksValue in BRanks.Values)
        {
            bRanksValue.IsCurrentTarget = false;
        }

        foreach (var huntLog in ClassHuntRanks.Values)
        {
            for (int row = 0; row < huntLog.HuntMarks.GetLength(0); row++)
            {
                for (int col = 0; col < huntLog.HuntMarks.GetLength(1); col++)
                {
                    var mark = huntLog.HuntMarks[row, col];

                    if (mark != null)
                    {
                        mark.IsCurrentTarget = false;
                    }
                }
            }
        }

        foreach (var huntLog in GcHuntRanks.Values)
        {
            for (int row = 0; row < huntLog.HuntMarks.GetLength(0); row++)
            {
                for (int col = 0; col < huntLog.HuntMarks.GetLength(1); col++)
                {
                    var mark = huntLog.HuntMarks[row, col];

                    if (mark != null)
                    {
                        mark.IsCurrentTarget = false;
                    }
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
