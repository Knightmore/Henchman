using System.Linq;
using System.Text.Json.Serialization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Underlings.Keybinds;
using Underlings.Modules;
using Action = System.Action;

namespace Henchman.Features.Chocobokeep;

[Module]
internal class ChocobokeepUI : ModuleUI
{
    private readonly Chocobokeep feature = new();

    private         bool            hideUnlocked;
    public override string          Name     => "Chocobokeep";
    public override Enum            Category => Henchman.Category.Exploration;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Feather;

    public override Action Help => () =>
                                   {
                                       ImGui.Text(T("HelpText"));
                                       DrawRequirements(Requirements);
                                   };

    public override bool LoginNeeded => false;

    [Keybind("Chocobokeep - Start")]
    private void StartTask()
    {
        if (IsTaskRunning(Name)) return;
        feature.RunTask();
    }

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true)
    ];

    public override ModuleAction PrimaryAction => new(Loc.G("UI.V2.Start", "Start"), StartTask, "Chocobokeep - Start");

    public override string? Summary
    {
        get
        {
            var (unlocked, total) = Progress();
            return string.Format(T("SummaryUnlocked"), unlocked, total);
        }
    }

    public override void Draw()
    {
        Widgets.Columns(16f, (0f, () =>
                                  {
                                      DrawProgressCard();
                                      DrawStandsCard();
                                  }),
                        (262f, () => Widgets.RequirementsCard(this)));
    }

    private (int Unlocked, int Total) Progress() => (feature.keeps.Count(k => IsUnlocked(k.ChocoboTaxiStandId)), feature.keeps.Count);

    private static unsafe bool IsUnlocked(uint standId) => UIState.Instance()->IsChocoboTaxiStandUnlocked(standId);

    private void DrawProgressCard()
    {
        var (unlocked, total) = Progress();
        var missing      = feature.keeps.Where(k => !IsUnlocked(k.ChocoboTaxiStandId)).ToList();
        var territories  = missing.Select(k => k.TerritoryId).Distinct().Count();
        var fraction     = total == 0 ? 0f : unlocked / (float)total;

        Widgets.Card(null, () =>
                           {
                               using (V2Fonts.PushLarge())
                               {
                                   ImGui.TextUnformatted(unlocked.ToString());
                               }

                               ImGui.SameLine(0, 8 * GlobalFontScale);
                               ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 6 * GlobalFontScale);
                               Widgets.TextDim(string.Format(T("OfStandsUnlocked"), total));
                               ImGui.SameLine();
                               Widgets.RightAligned("##chocoboPercent", () => Widgets.TextColored(V2Palette.Blue, $"{fraction:P0}"));
                               Widgets.ProgressBar(fraction, V2Palette.Blue, 8f);
                               Widgets.TextColored(missing.Count > 0 ? V2Palette.Warning : V2Palette.Success, string.Format(T("MissingIn"), missing.Count, territories));
                           });
    }

    private void DrawStandsCard()
    {
        var keeps = hideUnlocked
                            ? feature.keeps.Where(x => !IsUnlocked(x.ChocoboTaxiStandId))
                            : feature.keeps;

        Widgets.Card(T("Stands"), () =>
                                  {
                                      using var table = ImRaii.Table("##KeepsTable", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp, new Vector2(Widgets.Avail, 0));
                                      if (!table.Success) return;

                                      ImGui.TableSetupColumn(T("ColKeepId").ToUpperInvariant(), ImGuiTableColumnFlags.WidthFixed, 90 * GlobalFontScale);
                                      ImGui.TableSetupColumn(T("ColTerritory").ToUpperInvariant());
                                      ImGui.TableSetupColumn(T("ColPlaceName").ToUpperInvariant());
                                      ImGui.TableSetupColumn(T("ColUnlocked").ToUpperInvariant(), ImGuiTableColumnFlags.WidthFixed, 100 * GlobalFontScale);
                                      using (ImRaii.PushColor(ImGuiCol.Text, V2Palette.TextMuted))
                                      {
                                          ImGui.TableHeadersRow();
                                      }

                                      foreach (var keep in keeps)
                                      {
                                          var unlocked = IsUnlocked(keep.ChocoboTaxiStandId);
                                          ImGui.TableNextRow();
                                          ImGui.TableNextColumn();
                                          Widgets.TextMuted(keep.ChocoboTaxiStandId.ToString());
                                          ImGui.TableNextColumn();
                                          Widgets.TextColored(unlocked ? V2Palette.TextDim : V2Palette.Text, Svc.Data.GetExcelSheet<TerritoryType>()
                                                                                                                  .GetRow(keep.TerritoryId)
                                                                                                                  .PlaceName.Value.Name.ExtractText());
                                          ImGui.TableNextColumn();
                                          Widgets.TextColored(unlocked ? V2Palette.TextDim : V2Palette.Text, Svc.Data.GetExcelSheet<ChocoboTaxiStand>()
                                                                                                                  .GetRow(keep.ChocoboTaxiStandId)
                                                                                                                  .PlaceName.ExtractText());
                                          ImGui.TableNextColumn();
                                          Widgets.Chip(unlocked ? T("Unlocked") : T("Missing"), unlocked ? V2Palette.Success : V2Palette.Warning);
                                      }
                                  }, 0f, () =>
                                         {
                                             Widgets.TextSoft(T("HideUnlocked"));
                                             ImGui.SameLine(0, 10 * GlobalFontScale);
                                             Widgets.Switch("##hideUnlocked", ref hideUnlocked);
                                         });
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
