using Henchman.Data;
using Henchman.Features.RetainerVocate;

namespace Henchman.Features.IntoTheLight;

public class RetainerCharacter
{
    public string                              Name;
    public uint                                Class = 18;
    public RetainerDetails.RetainerGender      Gender;
    public RetainerDetails.RetainerPersonality Personality = RetainerDetails.RetainerPersonality.Polite;
    public RetainerDetails.RetainerRace        Race;
}
