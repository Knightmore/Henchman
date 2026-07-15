using System.Linq;

namespace Henchman.Features.OnYourMark;

public class Configuration
{
    public Dictionary<string, bool> EnableHuntBills = HuntBoardOptions.ToDictionary(kvp => kvp, _ => false);
}
