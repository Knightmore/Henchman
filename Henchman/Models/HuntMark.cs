using System.Linq;
using System.Text.Json.Serialization;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace Henchman.Models;

public class HuntMark
{
    public HuntMark(uint bnpcNameRowId, float x, float y, float z, uint territoryId, uint fateId)
    {
        BNpcNameRowId = bnpcNameRowId;
        Positions.Add(new Vector3(x, y, z));
        TerritoryId = territoryId;
        FateId      = fateId;
    }

    public HuntMark(HuntMark original)
    {
        BNpcNameRowId = original.BNpcNameRowId;
        Positions     = new List<Vector3>(original.Positions);
        TerritoryId   = original.TerritoryId;
        FateId        = original.FateId;
        NeededKills   = original.NeededKills;
    }

    public uint BNpcNameRowId { get; private set; }

    public uint FateId { get; private set; }

    public uint TerritoryId { get; private set; }

    public List<Vector3> Positions { get; } = [];

    public int NeededKills { get; set; }

    public bool IsDuty => Svc.Data.Excel.GetSheet<TerritoryType>()
                             .GetRow(TerritoryId)
                             .ExclusiveType ==
                          2;

    public        byte MobHuntRowId           { get; set; }
    public        byte MobHuntSubRowId        { get; set; }
    public unsafe int  GetCurrentMobHuntKills => MobHunt.Instance()->GetKillCount(MobHuntRowId, MobHuntSubRowId);

    public int GetOpenMobHuntKills => NeededKills - GetCurrentMobHuntKills > 0
                                              ? NeededKills - GetCurrentMobHuntKills
                                              : 0;

    public int MonsterNoteId      { get; set; }
    public int MonsterNoteSubRank { get; set; }
    public int MonsterNoteCount   { get; set; }

    public unsafe int GetCurrentMonsterNoteKills => MonsterNoteManager.Instance()->RankData[MonsterNoteId]
                                                   .RankData[MonsterNoteSubRank]
                                                   .Counts[MonsterNoteCount];

    public int GetOpenMonsterNoteKills => NeededKills - GetCurrentMonsterNoteKills > 0
                                                  ? NeededKills - GetCurrentMonsterNoteKills
                                                  : 0;

    public unsafe MobHuntOrder GetMobHuntOrderRow => Svc.Data.GetSubrowExcelSheet<MobHuntOrder>()[Svc.Data.GetExcelSheet<MobHuntOrderType>()
                                                                                                     .GetRow(MobHuntRowId)
                                                                                                     .OrderStart.Value.RowId +
                                                                                                  ((uint)MobHunt.Instance()->ObtainedMarkId[MobHuntRowId] - 1)][MobHuntSubRowId];

    public bool IsCurrentTarget = false;

    public Fate Fate => Svc.Data.GetExcelSheet<Fate>()
                           .GetRow(FateId);

    public BNpcName BNpcNameSheet => Svc.Data.GetExcelSheet<BNpcName>()
                                        .GetRow(BNpcNameRowId);

    public string Name => Svc.Data.GetExcelSheet<BNpcName>()
                             .GetRow(BNpcNameRowId)
                             .Singular.ExtractText();

    public int Icon => Svc.Data.GetExcelSheet<MonsterNoteTarget>()
                          .FirstOrDefault(x => x.BNpcName.RowId == BNpcNameRowId)
                          .Icon;
}

public class JsonHuntMark
{
    [JsonPropertyName("BnpcName")]
    public uint BnpcName { get; set; }

    [JsonPropertyName("X")]
    public float X { get; set; }

    [JsonPropertyName("Y")]
    public float Y { get; set; }

    [JsonPropertyName("Z")]
    public float Z { get; set; }

    [JsonPropertyName("TerritoryId")]
    public uint TerritoryId { get; set; }

    [JsonPropertyName("FateId")]
    public uint FateId { get; set; }
}
