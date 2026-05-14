using Henchman.Abstractions;

namespace Henchman.Multiboxing;

public class Configuration : IConfig
{
    public byte[] IpBytes = new byte[64];

    public bool LocalOnly = true;

    //public bool   SingleServerInstance = true;
    public uint Port = 5410;
}
