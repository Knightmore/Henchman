using Henchman.Abstractions;
using Henchman.Multiboxing;

namespace Henchman.Features.BumpOnALog;

public class Configuration : IConfig
{
    public bool      AutoGCRankUp      = true;
    public PartySize MultiboxPartySize = PartySize.Two;
    public bool      OrderByTerritory  = false;

    public SessionType SessionType      = SessionType.Boss;
    public bool        SkipDutyMarks    = false;
    public int         StopAfterGCRank  = 8;
    public int         StopAfterJobRank = 5;
}
