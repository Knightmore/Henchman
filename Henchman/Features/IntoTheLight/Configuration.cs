using System;
using System.Collections.Generic;
using System.Text;

namespace Henchman.Features.IntoTheLight
{
    public class Configuration : IConfig
    {
        public List<LightCharacter> LightCharacters  = [];
        public bool                 LightNoLoginSkip = false;
    }
}
