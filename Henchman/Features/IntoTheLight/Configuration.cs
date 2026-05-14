using Henchman.Abstractions;

namespace Henchman.Features.IntoTheLight;

public class Configuration : IConfig
{
    public List<LightCharacter> LightCharacters  = [];
    public bool                 LightNoLoginSkip = false;
}
