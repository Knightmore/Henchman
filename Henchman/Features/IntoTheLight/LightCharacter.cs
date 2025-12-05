namespace Henchman.Features.IntoTheLight;

public class LightCharacter
{
    public IntoTheLightUI.ClassJob ClassJob     = IntoTheLightUI.ClassJob.Gladiator;
    public uint                    DataCenterId = 7;
    public string                  FirstName    = "";
    public string                  LastName     = "";
    public byte                    PresetId     = 255;
    public uint                    WorldId      = 66;

    public LightCharacter(LightCharacter other)
    {
        ClassJob     = other.ClassJob;
        DataCenterId = other.DataCenterId;
        FirstName    = other.FirstName;
        LastName     = other.LastName;
        PresetId     = other.PresetId;
        WorldId      = other.WorldId;
    }

    public LightCharacter() { }
}
