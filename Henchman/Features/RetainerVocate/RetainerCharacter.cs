using Henchman.Features.RetainerVocate;

namespace Henchman.Features.IntoTheLight;

public class RetainerCharacter
{
    public int                                 Clan  = 1;
    public uint                                Class = 18;
    public RetainerDetails.RetainerGender      Gender;
    public string                              Name;
    public RetainerDetails.RetainerPersonality Personality = RetainerDetails.RetainerPersonality.Polite;
    public (byte DenseIndex, byte RealIndex)   PresetId    = (255, 255);
    public RetainerDetails.RetainerRace        Race;
}
