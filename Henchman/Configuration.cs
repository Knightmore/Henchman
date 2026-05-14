namespace Henchman;

[Serializable]
public class Configuration
{
    public string AutoRotationPlugin = IPCNames.Wrath;

    public bool DetourForARanks  = false;
    public bool DiscardOldBills  = false;
    public int  MinMountDistance = 50;
    public int  MinRunDistance   = 20;
    public uint MountId          = 1;

    public bool                             ReturnOnceDone = false;
    public Lifestream.LifestreamDestination ReturnTo       = Lifestream.LifestreamDestination.Home;

    public bool SkipFateMarks      = false;
    public bool SoloUnsyncLogDuty  = false;
    public bool TrackBRankSpots    = false;
    public bool UseChocoboInFights = false;
    public bool UseMount           = true;
    public bool UseMountRoulette   = true;
}
