using System;
using System.Collections.Generic;

namespace Henchman.Features.OnABoat;

public class Configuration
{
    public bool                    UseFeeshDiscard             = false;
    public bool                    DiscardAfterVoyage          = false;
    public Dictionary<ulong, bool> EnableCharacterForOCFishing = [];
    public int                     MaxLevel                    = 100;
    public string                  OceanChar                   = string.Empty;
    public string                  OceanWorld                  = string.Empty;
    public bool                    OCFishingHandleAR           = false;
    public bool                    OCFishingStopLevel          = false;
    public bool                    SellAfterVoyage             = false;
    public bool                    SellAtLocalVendor           = false;
    public bool                    PreferRuby                 = false;
    public List<BoatCharacter>?    ManualCharacters;

    public List<BoatCharacter> GetManualCharacters()
    {
        if (ManualCharacters is not null) return ManualCharacters;
        ManualCharacters = [];
        if (!string.IsNullOrWhiteSpace(OceanChar) && !string.IsNullOrWhiteSpace(OceanWorld))
            ManualCharacters.Add(new BoatCharacter { Name = OceanChar.Trim(), World = OceanWorld.Trim() });
        return ManualCharacters;
    }

    public BoatCharacter? NextManualCharacter(Guid? previous)
    {
        var characters = GetManualCharacters();
        if (characters.Count == 0) return null;
        var index = characters.FindIndex(x => x.Id == previous);
        return characters[(index + 1) % characters.Count];
    }
}

public class BoatCharacter
{
    public Guid Id = Guid.NewGuid();
    public string Name = string.Empty;
    public string World = string.Empty;
}
