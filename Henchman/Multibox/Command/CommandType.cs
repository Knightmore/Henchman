namespace Henchman.Multibox.Command;

public enum CommandType : ushort
{
    None = 0,
    RPC,
    ServerRequest,
    RoundRobinResponse,
    Feature
}

public enum RoundRobinResponse : ushort
{
    TurnDone,
    Available,
    Finished
}

public enum ServerRequest : ushort
{
    Turn
}
