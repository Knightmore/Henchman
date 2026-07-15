using System.Linq;
using Henchman.Models;
using Henchman.Multiboxing;

namespace Henchman.Features.TestyTrader;

public enum TestyTraderInvColumnMode
{
    Inventory,
    Tanks,
    Repair
}

public class Configuration
{
    public Dictionary<ulong, bool>                      EnableCharacterForTrade       = [];
    public bool                                         IncludeArmory                 = false;
    public bool                                         MoveBossToHenchman            = false;
    public Guid                                          SelectedTradeListId           = Guid.Empty;
    public bool                                         TestyTraderARSupport          = false;
    public List<TestyTraderUI.TestyTraderCharacterData> TestyTraderImportedCharacters = [];
    public TestyTraderInvColumnMode                     TestyTraderInvColumnMode      = TestyTraderInvColumnMode.Inventory;
    public List<TradeEntry>                             TradeEntries                  = [];
    public List<TradeList>                              TradeLists                    = [];
    public SessionType                                  TradeSession                  = SessionType.Boss;
    public bool                                         UseARItemSell                 = true;

    public TradeList? GetActivePlan() => TradeLists.FirstOrDefault(x => x.Id == SelectedTradeListId) ?? TradeLists.FirstOrDefault();
}
