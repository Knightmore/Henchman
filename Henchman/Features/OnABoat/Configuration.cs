namespace Henchman.Features.OnABoat;

public class Configuration
{
    public bool                    DiscardAfterVoyage          = false;
    public Dictionary<ulong, bool> EnableCharacterForOCFishing = [];
    public string                  OceanChar                   = string.Empty;
    public string                  OceanWorld                  = string.Empty;
    public bool                    OCFishingHandleAR           = false;
    public bool                    OCFishingStop100            = false;
    public bool                    SellAfterVoyage             = false;
    public bool                    SellAtLocalVendor           = false;
    public bool                    UseOnlyVersatile            = true;
}
