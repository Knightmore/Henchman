namespace Henchman.Features.AlexAndrite;

public class Configuration
{
    public bool UseSaddlebag;
    public int MapsToFarm;
    public bool FarmPoetics;
    public PoeticsDuty SelectedDuty = PoeticsDuty.CastrumMeridianum;
}

public enum PoeticsDuty
{
    CastrumMeridianum,
    Praetorium,
    PortaDecumana
}
