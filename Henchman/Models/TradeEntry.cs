using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Henchman.Models
{
    public class TradeEntry
    {
        public uint      Id;
        public TradeMode Mode;
        public uint      Amount;
    }

    public enum TradeMode
    {
        Give,
        Keep,
        AskUntil
    }
}
