namespace Henchman.Multiboxing.Command;

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
    ServerFull,
    Turn,          // for RoundRobin
    StartParallel, // for Parallel
    Disconnect
}
