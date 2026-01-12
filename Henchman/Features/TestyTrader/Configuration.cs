using System;
using System.Collections.Generic;
using System.Text;
using Henchman.Models;

namespace Henchman.Features.TestyTrader
{
    public class Configuration : IConfig
    {
        public TradeSession                                 TradeSession                  = TradeSession.Boss;
        public bool                                         TestyTraderARSupport          = false;
        public Dictionary<ulong, bool>                      EnableCharacterForTrade       = [];
        public List<TestyTraderUI.TestyTraderCharacterData> TestyTraderImportedCharacters = [];
        public List<TradeEntry>                             TradeEntries                  = [];
        public bool                                         UseARItemSell                 = true;
        public bool                                         MoveBossToHenchman            = false;
    }
}
