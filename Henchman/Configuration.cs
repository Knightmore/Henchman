using ECommons.Configuration;
using Henchman.Data;
using Henchman.Features.RetainerVocate;
using System.Linq;

namespace Henchman;

[Serializable]
public class Configuration : IEzConfig
{
    /*
     * On Your Mark
     */

    public Dictionary<string, bool> EnableHuntBills = HuntBoardOptions.ToDictionary(kvp => kvp, _ => false);
    public bool DetourForOtherAB = false;
    public bool DiscardOldBills = false;
    public bool SkipFateMarks = false;

    public uint MountId = 1;
    /*
     * General Player Config
     */

    public string AutoRotationPlugin = IPCNames.Wrath;
    public bool UseChocoboInFights = false;
    public bool UseMount = true;
    public bool UseMountRoulette = true;
    public bool UseOnlineMobData = false;
    public bool UseMeleeRange = false;
    public bool ReturnOnceDone = false;
    public Lifestream.LifestreamDestination ReturnTo = Lifestream.LifestreamDestination.Home;
    public int MinMountDistance = 50;
    public int MinRunDistance = 20;

    /*
     * Retainer Creator
     */

    public int RetainerAmount = 1;
    public NpcDatabase.StarterCity RetainerCity = NpcDatabase.StarterCity.LimsaLominsa;
    public uint RetainerClass = 18;
    public RetainerDetails.RetainerGender RetainerGender;
    public RetainerDetails.RetainerPersonality RetainerPersonality = RetainerDetails.RetainerPersonality.Polite;
    public RetainerDetails.RetainerRace RetainerRace;
    public bool UseMaxRetainerAmount = true;
    public bool SendOnFirstExploration = false;
    public uint QstClassJob = 1;

    /*
     * On Your B Game
     */
    public uint BRankToFarm;
    public bool TrackBRankSpots;
}
