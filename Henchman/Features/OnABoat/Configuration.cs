using System;
using System.Collections.Generic;
using System.Text;

namespace Henchman.Features.OnABoat
{
    public class Configuration : IConfig
    {
        public bool                    OCFishingHandleAR           = false;
        public Dictionary<ulong, bool> EnableCharacterForOCFishing = [];
        public bool                    UseOnlyVersatile            = true;
        public string                  OceanChar                   = string.Empty;
        public string                  OceanWorld                  = string.Empty;
        public bool                    DiscardAfterVoyage          = false;
        public bool                    OCFishingStop100            = false;
    }
}
