using System;
using System.Collections.Generic;
using System.Text;

namespace Henchman.Features.BumpOnALog
{
    public class Configuration : IConfig
    {
        public bool SkipDutyMarks     = false;
        public int  StopAfterGCRank   = 3;
        public int  StopAfterJobRank  = 5;
        public bool ProgressGCRanks   = false;
    }
}
