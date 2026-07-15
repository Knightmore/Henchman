using System.Text.Json.Serialization;

namespace Henchman.Models;

public enum MobSampleKind
{
    Common,
    Fate,
    ARank,
    BRank,
    IsolatedCandidate
}

public enum MobScrapeRoutePointState
{
    Pending,
    Completed,
    Skipped,
    Failed
}

public class MobScrapeTerritoryOverview
{
    public uint   TerritoryId   { get; set; }
    public string TerritoryName { get; set; } = string.Empty;
    public string ZoneName      { get; set; } = string.Empty;
    public string ExpansionName { get; set; } = string.Empty;
    public int    SampleCount   { get; set; }
    public int    ClusterCount  { get; set; }
    public int    RouteCount    { get; set; }
}

public class MobSample
{
    public uint          BNpcNameRowId       { get; set; }
    public uint          BNpcBaseRowId       { get; set; }
    public uint          TerritoryId         { get; set; }
    public uint          FateId              { get; set; }
    public byte          Level               { get; set; }
    public float         X                   { get; set; }
    public float         Y                   { get; set; }
    public float         Z                   { get; set; }
    public uint          EntityId            { get; set; }
    public ulong         OwnerId             { get; set; }
    public byte          BattleNpcSubKind    { get; set; }
    public byte          NameplateKind       { get; set; }
    public byte          TargetStatus        { get; set; }
    public byte          TargetableStatus    { get; set; }
    public int           SameNameNearbyCount { get; set; }
    public MobSampleKind Kind                { get; set; }

    [JsonIgnore]
    public Vector3 Position => new(X, Y, Z);
}

public class MobCluster
{
    public uint          BNpcNameRowId { get; set; }
    public uint          BNpcBaseRowId { get; set; }
    public uint          TerritoryId   { get; set; }
    public uint          FateId        { get; set; }
    public byte          Level         { get; set; }
    public MobSampleKind Kind          { get; set; }
    public float         X             { get; set; }
    public float         Y             { get; set; }
    public float         Z             { get; set; }
    public float         Radius        { get; set; }
    public int           SampleCount   { get; set; }

    [JsonIgnore]
    public Vector3 Center => new(X, Y, Z);
}

public class MobScrapeRoutePoint
{
    public uint                     TerritoryId { get; set; }
    public float                    X           { get; set; }
    public float                    Y           { get; set; }
    public float                    Z           { get; set; }
    public float                    ProbeY      { get; set; }
    public MobScrapeRoutePointState State       { get; set; }

    [JsonIgnore]
    public Vector3 Position => new(X, Y, Z);
}
