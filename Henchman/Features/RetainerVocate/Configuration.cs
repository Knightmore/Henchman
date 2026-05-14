using System.Linq;
using Henchman.Abstractions;
using Henchman.Data;
using Henchman.Features.IntoTheLight;

namespace Henchman.Features.RetainerVocate;

public class Configuration : IConfig
{
    public uint QstClassJob    = 1;
    public int  RetainerAmount = 1;

    public RetainerCharacter[] RetainerCharacters = Enumerable.Range(0, 10)
                                                              .Select(i => new RetainerCharacter())
                                                              .ToArray();

    public NpcDatabase.StarterCity             RetainerCity  = NpcDatabase.StarterCity.LimsaLominsa;
    public uint                                RetainerClass = 18;
    public RetainerDetails.RetainerGender      RetainerGender;
    public RetainerDetails.RetainerPersonality RetainerPersonality = RetainerDetails.RetainerPersonality.Polite;
    public RetainerDetails.RetainerRace        RetainerRace;
    public bool                                SendOnFirstExploration = false;
    public bool                                UseMaxRetainerAmount   = true;
}
