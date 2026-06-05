using Henchman.Abstractions;

namespace Henchman.Features.OnABoat;

public class Configuration : IConfig
{
    public bool DiscardAfterVoyage = false;
    public Dictionary<ulong, bool> EnableCharacterForOCFishing = [];
    public string OceanChar = string.Empty;
    public string OceanWorld = string.Empty;
    public bool OCFishingHandleAR = false;
    public bool OCFishingStop100 = false;
    public bool SellAfterVoyage = false;
    public bool UseOnlyVersatile = true;
    public bool SellAtLocalVendor = false;
}
