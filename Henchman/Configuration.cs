using System.Linq;
using Henchman.Data;
using Henchman.Features.RetainerVocate;

namespace Henchman;

[Serializable]
public class Configuration
{
    /*
     * General Player Config
     */

    public string AutoRotationPlugin = IPCNames.Wrath;

    /*
     * Bring Your B Game
     */

    public uint BRankToFarm;
    public bool DetourForARanks = false;
    public bool DiscardOldBills = false;

    public Dictionary<ulong, bool> EnableCharacterForOCFishing = [];

    /*
     * On Your Mark
     */

    public Dictionary<string, bool> EnableHuntBills = HuntBoardOptions.ToDictionary(kvp => kvp, _ => false);

    /*
     * On A Boat
     */

    public bool HandleAR         = false;
    public int  MinMountDistance = 50;
    public int  MinRunDistance   = 20;

    public uint   MountId         = 1;
    public string OceanChar       = string.Empty;
    public string OceanWorld      = string.Empty;
    public bool   ProgressGCRanks = false;
    public uint   QstClassJob     = 1;

    /*
     * Retainer Creator
     */

    public int                                 RetainerAmount = 1;
    public NpcDatabase.StarterCity             RetainerCity   = NpcDatabase.StarterCity.LimsaLominsa;
    public uint                                RetainerClass  = 18;
    public RetainerDetails.RetainerGender      RetainerGender;
    public RetainerDetails.RetainerPersonality RetainerPersonality = RetainerDetails.RetainerPersonality.Polite;
    public RetainerDetails.RetainerRace        RetainerRace;
    public bool                                ReturnOnceDone         = false;
    public bool                                ReturnOnError          = false;
    public Lifestream.LifestreamDestination    ReturnTo               = Lifestream.LifestreamDestination.Home;
    public bool                                SendOnFirstExploration = false;

    /*
     * Bump On A Log
     */

    public bool SkipDutyMarks    = false;
    public bool SkipFateMarks    = false;
    public int  StopAfterGCRank  = 3;
    public int  StopAfterJobRank = 5;
    public bool TrackBRankSpots;
    public bool UseChocoboInFights   = false;
    public bool UseMaxRetainerAmount = true;
    public bool UseMount             = true;
    public bool UseMountRoulette     = true;
    public bool WaitOnTitleMenu      = false;
}
