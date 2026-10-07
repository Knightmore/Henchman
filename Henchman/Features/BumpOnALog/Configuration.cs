using System;
using Henchman.Multiboxing;

namespace Henchman.Features.BumpOnALog;

public class Configuration
{
    public Dictionary<ulong, bool> EnableCharacter = [];
    public Dictionary<ulong, byte> SelectedGearset = [];
    public Dictionary<ulong, int> CharacterGCRanks = [];

    public bool      AutoGCRankUp      = true;
    public PartySize MultiboxPartySize = PartySize.Two;
    public bool      OrderByTerritory  = false;

    public SessionType SessionType      = SessionType.Boss;
    public bool        SkipDutyMarks    = false;
    public int         StopAfterGCRank  = 8;
    public int         StopAfterJobRank = 5;

    internal bool RecordGCRank(ulong cid, int rank)
    {
        if (CharacterGCRanks.TryGetValue(cid, out var savedRank) && savedRank == rank) return false;
        CharacterGCRanks[cid] = rank;
        return true;
    }

    internal void SetCharactersEnabled(IEnumerable<ulong> cids, bool enabled)
    {
        foreach (var cid in cids) EnableCharacter[cid] = enabled;
    }

    internal bool IsAboveGCStoppingRank(int rank) => rank <= 9 && rank > Math.Min(StopAfterGCRank + 1, 9);

    internal bool ShouldSkipGCCharacter(ulong cid) => CharacterGCRanks.TryGetValue(cid, out var rank) && IsAboveGCStoppingRank(rank);

    internal List<LogCycleCharacter> CreateCyclePlan(IEnumerable<(ulong CID, string Name, string World)> characters, bool gcLog = false)
    {
        var plan = new List<LogCycleCharacter>();
        foreach (var character in characters)
        {
            if (!EnableCharacter.GetValueOrDefault(character.CID)) continue;
            if (gcLog && ShouldSkipGCCharacter(character.CID)) continue;
            byte? gearsetId = SelectedGearset.TryGetValue(character.CID, out var selectedId) ? selectedId : null;
            if (gearsetId is >= 100)
                throw new InvalidOperationException($"Select a gearset for {character.Name}@{character.World} before starting the cycle.");
            plan.Add(new LogCycleCharacter(character.CID, character.Name, character.World, gearsetId));
        }
        if (plan.Count == 0) throw new InvalidOperationException(gcLog
                ? "No enabled characters need this GC cycle based on their stored ranks."
                : "Enable at least one character before starting the cycle.");
        return plan;
    }
}

internal readonly record struct LogCycleCharacter(ulong CID, string Name, string World, byte? GearsetId);
