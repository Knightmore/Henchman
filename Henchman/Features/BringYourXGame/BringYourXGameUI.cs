using System.Linq;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using ECommons.MathHelpers;
using Henchman.Helpers;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.OnYourBGame;

[Feature]
public class BringYourXGameUI : FeatureUI
{
    private readonly BringYourXGame feature = new();

    internal List<Vector3> FoundSpawns = [];
    private  string        gameRank    = "X";
    private  Map           map;

    internal        List<Vector3> PossibleSpawnPoints = [];
    public override string        Name => $"Bring Your {gameRank} Game";

    public override Action Help => () =>
                                   {
                                       ImGui.Text(gameRank == "A"
                                                          ? """
                                                            Just hit "Start" and it will roam all available A-Rank destinations up to Stormbloond.
                                                            """
                                                          : """
                                                            Pick the territory you want to roam and click "Start" to begin.
                                                            "Bring Your B Game" will farm any B-Rank that it can find until you stop it.
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

    public override bool LoginNeeded => false;

    public override void Draw()
    {
        if (gameRank == "X")
        {
            gameRank           = "A";
            P!.SelectedFeature = Name;
        }

        string[] ranks = { "A", "B" };

        var sortedRanks = ranks.OrderByDescending(r => r == gameRank);

        if (ImGui.BeginTabBar("RankTabs"))
        {
            foreach (var rank in sortedRanks)
            {
                var label    = $"{rank}-Rank";
                var isActive = gameRank == rank;

                var tab = ImRaii.TabItem(label);
                if (tab != null)
                {
                    if (isActive)
                        DrawRank();

                    if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        gameRank           = rank;
                        P!.SelectedFeature = Name;
                    }
                }
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawRank()
    {
        if (gameRank == "A")
        {
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail()
                                      .X /
                                 2) -
                                10);
            if (ImGui.Button("Start")) EnqueueTask(new TaskRecord(feature.StartA, "Bring Your A Game"));
        }
        else if (gameRank == "B")
        {
            var configChanged = false;
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail()
                                      .X /
                                 2) -
                                10);
            if (ImGui.Button("Start"))
            {
                EnqueueTask(new TaskRecord(feature.StartB, "Bring Your B Game"));
                if (C.TrackBRankSpots)
                {
                    if (BRanks.TryGetValue(C.BRankToFarm, out var mark))
                    {
                        PossibleSpawnPoints.Clear();
                        foreach (var position in mark.Positions) PossibleSpawnPoints.Add(position);
                        FoundSpawns.Clear();
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
                    foreach (var position in PossibleSpawnPoints)
                    {
                        sb.AppendLine($"World - X: {position.X} | Y: {position.Y} | Z: {position.Z}");
                        var mapCoords = WorldToMap(position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                        sb.AppendLine($"Map - X: {mapCoords.X} | Y: {mapCoords.Y}");
                        sb.AppendLine("");
                    }

                    sb.AppendLine("Registered spawn positions");
                    foreach (var position in FoundSpawns)
                    {
                        sb.AppendLine($"World - X: {position.X} | Y: {position.Y} | Z: {position.Z}");
                        var mapCoords = WorldToMap(position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                        sb.AppendLine($"Map - X: {mapCoords.X} | Y: {mapCoords.Y}");
                        sb.AppendLine("");
                    }

                    Log(sb.ToString());
                }

                ImGui.SameLine();
                if (ImGui.Button("Print spawn locations to chat"))
                {
                    var mapRow = Svc.Data.GetExcelSheet<TerritoryType>()
                                    .GetRow(BRanks[C.BRankToFarm].TerritoryId)
                                    .Map.Value;
                    foreach (var position in FoundSpawns)
                    {
                        var mapCoords = WorldToMap(position.ToVector2(), mapRow.OffsetX, mapRow.OffsetY, mapRow.SizeFactor);
                        var mapLink   = SeString.CreateMapLink(mapRow.PlaceName.Value.Name.ExtractText(), mapCoords.X, mapCoords.Y);
                        var message = new XivChatEntry
                                      {
                                              Type = XivChatType.Echo,
                                              Message = new SeStringBuilder().AddUiForeground($"B-Rank {BRanks[C.BRankToFarm].Name} found @ ", 561)
                                                                             .Append(mapLink)
                                                                             .AddUiForegroundOff()
                                                                             .Build()
                                      };
                        Svc.Chat.Print(message);
                    }
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

                foreach (var position in PossibleSpawnPoints)
                {
                    ImGui.Text("World");
                    ImGui.SameLine(100);
                    ImGui.Text($"X: {position.X} | Y: {position.Y} | Z: {position.Z}");
                    var mapCoords = WorldToMap(position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                    ImGui.Text("Map");
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

                foreach (var position in FoundSpawns)
                {
                    ImGui.Text("World");
                    ImGui.SameLine(100);
                    ImGui.Text($"X: {position.X} | Y: {position.Y} | Z: {position.Z}");
                    var mapCoords = WorldToMap(position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                    ImGui.Text("Map");
                    ImGui.SameLine(100);
                    ImGui.Text($"X: {mapCoords.X} | Y: {mapCoords.Y}");
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                }
            }
        }
    }
}
