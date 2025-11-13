namespace Henchman.Models;

public enum TradeSession
{
    Boss,
    Henchman
}

public class TradeEntry
{
    public uint      Amount;
    public bool      Enabled;
    public uint      Id;
    public TradeMode Mode;
}

public enum TradeMode
{
    Give,
    Keep,
    AskFor,
    AskUntil,
    PARLevel
}
