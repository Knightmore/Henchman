using System.Text.Json.Serialization;
using Lumina.Excel.Sheets;

namespace Henchman.Models;

public class Location(float x, float y, float z, uint territoryId)
{
    public float X           { get; set; } = x;
    public float Y           { get; set; } = y;
    public float Z           { get; set; } = z;
    public uint  TerritoryId { get; set; } = territoryId;

    [JsonIgnore]
    public Vector3 Position => new(X, Y, Z);

    public TerritoryType TerritoryType => Svc.Data.GetExcelSheet<TerritoryType>()
                                             .GetRow(TerritoryId);
}
