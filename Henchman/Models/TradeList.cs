namespace Henchman.Models;

public class TradeList
{
    public Guid             Id      = Guid.NewGuid();
    public string           Name    = "Plan 1";
    public List<TradeEntry> Entries = [];
}
