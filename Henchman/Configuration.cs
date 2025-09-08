using System.Linq;
using Henchman.Data;
using Henchman.Features.RetainerVocate;
using Henchman.Models;
using Lumina.Excel.Sheets;

namespace Henchman;

[Serializable]
public class Configuration
{
    /*
     * General Player Config
     */

    public bool   SeparateWindows    = false;
    public bool   EnableExperimental = false;
    public string AutoRotationPlugin = IPCNames.Wrath;
    public int    MinMountDistance   = 50;
    public int    MinRunDistance     = 20;
    public uint   MountId            = 1;
    public bool   UseMount           = true;
    public bool   UseMountRoulette   = true;
    public bool   UseChocoboInFights = false;

    public bool                             ReturnOnceDone     = false;
    public bool                             ReturnOnError      = false;
    public Lifestream.LifestreamDestination ReturnTo           = Lifestream.LifestreamDestination.Home;

    /*
     * Bring Your X Game
     */

    public uint BRankToFarm;
    public bool DetourForARanks = false;
    public bool DiscardOldBills = false;
    public bool TrackBRankSpots;
    public SortedSet<uint> EnabledTerritoriesForARank = new SortedSet<uint>(BRanks
                                                                           .Values
                                                                           .Where(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                                          .GetRow(x.TerritoryId)
                                                                                          .ExVersion.Value.RowId <=
                                                                                       2)
                                                                           .Select(x => x.TerritoryId));

    /*
     * On Your Mark
     */

    public Dictionary<string, bool> EnableHuntBills = HuntBoardOptions.ToDictionary(kvp => kvp, _ => false);
    public bool SkipFateMarks = false;

    /*
     * On A Boat
     */

    public bool OCFishingHandleAR = false;
    public Dictionary<ulong, bool> EnableCharacterForOCFishing = [];
    public string OceanChar = string.Empty;
    public string OceanWorld = string.Empty;
    public bool WaitOnTitleMenu = false;

    /*
     * Retainer Creator
     */

    public int RetainerAmount = 1;
    public NpcDatabase.StarterCity RetainerCity = NpcDatabase.StarterCity.LimsaLominsa;
    public uint RetainerClass = 18;
    public RetainerDetails.RetainerGender RetainerGender;
    public RetainerDetails.RetainerPersonality RetainerPersonality = RetainerDetails.RetainerPersonality.Polite;
    public RetainerDetails.RetainerRace RetainerRace;
    public bool SendOnFirstExploration = false;
    public uint QstClassJob = 1;
    public bool UseMaxRetainerAmount = true;

    /*
     * Bump On A Log
     */

    public bool SkipDutyMarks = false;
    public int StopAfterGCRank = 3;
    public int StopAfterJobRank = 5;
    public bool ProgressGCRanks = false;

    /*
     * Testy Trader
     */

    public TradeSession TradeSession = TradeSession.Boss;
    public bool TestyTraderARSupport = false;
    public Dictionary<ulong, bool> EnableCharacterForTrade = [];
    public List<TradeEntry> TradeEntries = [];

    /*
     * Into The Light
     */

    public uint     CharacterAmount    = 1;
    public uint     DataCenterId       = 8;
    public uint     WorldId            = 91;
    public bool     UseRandomFirstName = true;
    public bool     UseRandomLastName  = true;
    public string[] FirstName          = new string[8];
    public string[] LastName           = new string[8];
    public bool UsePreset = false;
    public uint PresetPosition = 1;
    public uint ClassJobId = 0;
}
