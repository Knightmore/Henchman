using System.Linq;
using Henchman.Data;
using Henchman.Features.RetainerVocate;
using Henchman.Models;
using Lumina.Excel.Sheets;
using Henchman.Features.TestyTrader;
using Henchman.Features.IntoTheLight;

namespace Henchman;

[Serializable]
public class Configuration
{
    public string AutoRotationPlugin = IPCNames.Wrath;
    public int MinMountDistance = 50;
    public int MinRunDistance = 20;
    public uint MountId = 1;
    public bool UseMount = true;
    public bool UseMountRoulette = true;
    public bool UseChocoboInFights = false;

    public bool ReturnOnceDone = false;
    public Lifestream.LifestreamDestination ReturnTo = Lifestream.LifestreamDestination.Home;

    public bool DetourForARanks = false;
    public bool DiscardOldBills = false;

    public bool SkipFateMarks     = false;
    public bool SoloUnsyncLogDuty = false;
    public bool TrackBRankSpots = false;
}
