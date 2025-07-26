using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using ECommons.MathHelpers;
using Henchman.Helpers;
using Henchman.TaskManager;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Text;
using Action = System.Action;

namespace Henchman.Features.OnYourBGame;

[Feature]
public class OnYourBGameUI : FeatureUI
{
    private readonly OnYourBGame feature = new();
    public override string Name => "On Your B Game";

    public override Action Help => () =>
                                   {
                                       ImGui.Text(
                                                  """
                                                  Pick the territory you want to roam and "Start" start to begin.
                                                  "On Your B Game" will farm any B-Rank that it can find until you stop it.
                                                  """);

                                       ImGuiHelper.DrawRequirements(Requirements);
                                   };

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.BossMod, false),
            (IPCNames.Wrath, false),
            (IPCNames.RotationSolverReborn, false)
    ];

    public override bool LoginNeeded => true;

    public List<Vector3> possibleSpawnPoints = [];
    public List<Vector3> foundSpawns = [];
    Map map = default;

    public override void Draw()
    {
        var configChanged = false;
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail()
                                  .X /
                             2) -
                            10);
        if (ImGui.Button("Start"))
        {
            EnqueueTask(new TaskRecord(feature.Start, "On Your B Game"));
            if (C.TrackBRankSpots)
            {
                if (BRanks.TryGetValue(C.BRankToFarm, out var mark))
                {
                    possibleSpawnPoints.Clear();
                    foreach (var position in mark.Positions)
                    {
                        possibleSpawnPoints.Add(position);
                    }
                    foundSpawns.Clear();
                }
            }
        }

        ImGui.Text("B-Rank to Farm:");
        ImGui.SameLine(150);
        ImGui.SetNextItemWidth(150f);
        if (ImGuiEx.ExcelSheetCombo<BNpcName>("##bRank", out var brank, s => s.GetRowOrDefault(C.BRankToFarm) is { } row
                                                                                     ? Utils.ToTitleCaseExtended(s.GetRow(C.BRankToFarm)
                                                                                                                  .Singular.ExtractText(), Svc.ClientState.ClientLanguage)
                                                                                     : string.Empty, x => Utils.ToTitleCaseExtended(x.Singular.ExtractText(), Svc.ClientState.ClientLanguage), x => BRanks.Keys.Any(b => b == x.RowId)))
        {
            C.BRankToFarm = brank.RowId;
            configChanged = true;
        }

        ImGui.Text("Track found B-Rank spots");
        ImGui.SameLine(200);
        configChanged |= ImGui.Checkbox("##echoBRanks", ref C.TrackBRankSpots);

        if (C.TrackBRankSpots)
        {
            if (BRanks.TryGetValue(C.BRankToFarm, out var mark))
            {
                map = Svc.Data.GetExcelSheet<TerritoryType>()
                         .GetRow(mark.TerritoryId)
                         .Map.Value;
            }

            ImGui.Separator();
            if (ImGui.Button("Write positions to log"))
            {
                var sb = new StringBuilder();
                sb.AppendLine("Left over possible positions");
                foreach (var position in possibleSpawnPoints)
                {
                    sb.AppendLine($"World - X: {position.X} | Y: {position.Y} | Z: {position.Z}");
                    var mapCoords = WorldToMap(position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                    sb.AppendLine($"Map - X: {mapCoords.X} | Y: {mapCoords.Y}");
                    sb.AppendLine("");
                }
                sb.AppendLine("Registered spawn positions");
                foreach (var position in foundSpawns)
                {
                    sb.AppendLine($"World - X: {position.X} | Y: {position.Y} | Z: {position.Z}");
                    var mapCoords = WorldToMap(position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                    sb.AppendLine($"Map - X: {mapCoords.X} | Y: {mapCoords.Y}");
                    sb.AppendLine("");
                }
                Log(sb.ToString());
            }
            using (var overviewTable = ImRaii.Table("###positionTables", 2))
            {
                ImGui.TableSetupColumn("##leftPositions", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("##rightPositions", ImGuiTableColumnFlags.WidthFixed);
                var regionSize = ImGui.GetContentRegionAvail();
                ImGui.TableNextColumn();
                DrawPossibleSpawnTable(regionSize, map);

                ImGui.TableNextColumn();
                DrawFoundSpawnTable(regionSize, map);
            }
        }

        if (configChanged) EzConfig.Save();
    }

    private void DrawPossibleSpawnTable(Vector2 regionSize, Map map)
    {
        using (var leftChild = ImRaii.Child("###leftChild", regionSize with { X = regionSize.X / 2 }, false, ImGuiWindowFlags.NoDecoration))
        {
            using (var possibleTable = ImRaii.Table("###possibleSpawns", 1, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Possible Positions", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                foreach (var position in possibleSpawnPoints)
                {
                    ImGui.Text($"World");
                    ImGui.SameLine(100);
                    ImGui.Text($"X: {position.X} | Y: {position.Y} | Z: {position.Z}");
                    var mapCoords = WorldToMap(position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                    ImGui.Text($"Map");
                    ImGui.SameLine(100);
                    ImGui.Text($"X: {mapCoords.X} | Y: {mapCoords.Y}");
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                }
            }
        }
    }

    private void DrawFoundSpawnTable(Vector2 regionSize, Map map)
    {
        using (var leftChild = ImRaii.Child("###rightChild", regionSize with { X = regionSize.X / 2 }, false, ImGuiWindowFlags.NoDecoration))
        {
            using (var foundTable = ImRaii.Table("###foundSpawns", 1, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Spawned Positions", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                foreach (var position in foundSpawns)
                {
                    ImGui.Text($"World");
                    ImGui.SameLine(100);
                    ImGui.Text($"X: {position.X} | Y: {position.Y} | Z: {position.Z}");
                    var mapCoords = WorldToMap(position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                    ImGui.Text($"Map");
                    ImGui.SameLine(100);
                    ImGui.Text($"X: {mapCoords.X} | Y: {mapCoords.Y}");
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                }
            }
        }
    }
}
