using Henchman.Abstractions;
using Henchman.Models;
using Henchman.Multiboxing;

namespace Henchman.Features.TestyTrader;

public class Configuration : IConfig
{
    public Dictionary<ulong, bool>                      EnableCharacterForTrade       = [];
    public bool                                         MoveBossToHenchman            = false;
    public bool                                         TestyTraderARSupport          = false;
    public List<TestyTraderUI.TestyTraderCharacterData> TestyTraderImportedCharacters = [];
    public List<TradeEntry>                             TradeEntries                  = [];
    public SessionType                                  TradeSession                  = SessionType.Boss;
    public bool                                         UseARItemSell                 = true;
}
