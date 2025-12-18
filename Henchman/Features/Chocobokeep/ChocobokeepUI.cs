using System.Linq;
using System.Text.Json.Serialization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.TaskManager;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.Chocobokeep;

[Feature]
internal class ChocobokeepUI : FeatureUI
{
    private readonly Chocobokeep feature = new();

    private         bool            HideUnlocked;
    public override string          Name     => "Chocobokeep";
    public override string          Category => Henchman.Category.Exploration;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Feather;

    public override Action Help => () =>
                                   {
                                       ImGui.Text("""
                                                  This only exists because a certain completionist *cough* Kawaii *cough* asked for a Chocobokeep Unlocker.
                                                  Hit "Start" and it will unlock all missing ChocoboTaxiStands.
                                                  """
                                                 );


                                       DrawRequirements(Requirements);
                                   };

    public override bool LoginNeeded => false;

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true)
    ];

    public override void Draw()
    {
        DrawCentered("##StartRetainers", () => Layout.DrawButton(() =>
                                                                 {
                                                                     if (StartButton() && !IsTaskEnqueued(Name))
                                                                         EnqueueTask(new TaskRecord(feature.Start, Name));
                                                                 }));
        ImGuiEx.LineCentered("###HideUnlocked", () => { ImGui.Checkbox("Hide Unlocked", ref HideUnlocked); });
        DrawCentered("##ChocoboKeepTable", () => { DrawChocoboTable(); });
    }

    private unsafe void DrawChocoboTable()
    {
        var keeps = HideUnlocked
                            ? feature.keeps.Where(x => !UIState.Instance()->IsChocoboTaxiStandUnlocked(x.ChocoboTaxiStandId))
                            : feature.keeps;
        var table = new Table<ChocobokeepData>(
                                               "##KeepsTable",
                                               new List<TableColumn<ChocobokeepData>>
                                               {
                                                       new("KeepId", h => h.ChocoboTaxiStandId.ToString()),
                                                       new("Territory", h => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                                .GetRow(h.TerritoryId)
                                                                                .PlaceName.Value.Name.ExtractText(), 200, Alignment : ColumnAlignment.Center),
                                                       new("Place Name", h => Svc.Data.GetExcelSheet<ChocoboTaxiStand>()
                                                                                 .GetRow(h.ChocoboTaxiStandId)
                                                                                 .PlaceName.ExtractText(), 200, Alignment : ColumnAlignment.Center),
                                                       new("Unlocked", h => UIState.Instance()->IsChocoboTaxiStandUnlocked(h.ChocoboTaxiStandId)
                                                                                    ? FontAwesomeIcon.Check.ToIconString()
                                                                                    : FontAwesomeIcon.Times.ToIconString(), 50, Alignment: ColumnAlignment.Center,
                                                           GetTextColor: h => UIState.Instance()->IsChocoboTaxiStandUnlocked(h.ChocoboTaxiStandId)
                                                                        ? Theme.SuccessGreen
                                                                        : Theme.ErrorRed)
                                               },
                                               () => keeps,
                                               size: new Vector2(550,0)
                                              );

        table.Draw();
    }

    public class ChocobokeepData
    {
        public uint Id                 { get; set; }
        public uint ChocoboTaxiStandId { get; set; }
        public uint TerritoryId        { get; set; }

        [JsonIgnore]
        public Vector3 Location { get; set; }

        [JsonPropertyName("Location")]
        public Vector3Dto LocationDto
        {
            get => new(Location);
            set => Location = new Vector3(value.X, value.Y, value.Z);
        }
    }

    public class Vector3Dto
    {
        public Vector3Dto() { }

        public Vector3Dto(Vector3 v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}
