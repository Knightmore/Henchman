using AutoRetainerAPI.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using ECommons.MathHelpers;
using Henchman.Helpers;
using Henchman.TaskManager;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Action = System.Action;
using Map = Lumina.Excel.Sheets.Map;

namespace Henchman.Features.BringYourXGame;

[Feature]
public class BringYourXGameUI : FeatureUI
{
    private readonly ImmutableSortedSet<uint> ARankTerritories = BRanks
                                                                .Values
                                                                .Where(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                               .GetRow(x.TerritoryId)
                                                                               .ExVersion.Value.RowId <=
                                                                            2)
                                                                .Select(x => x.TerritoryId)
                                                                .ToImmutableSortedSet();

    private readonly BringYourXGame feature = new();

    private readonly Dictionary<uint, List<uint>> GroupedARankTerritories = BRanks
                                                                           .Values
                                                                           .Where(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                                          .GetRow(x.TerritoryId)
                                                                                          .ExVersion.Value.RowId <=
                                                                                       2)
                                                                           .DistinctBy(x => x.TerritoryId)
                                                                           .GroupBy(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                                            .GetRow(x.TerritoryId)
                                                                                            .ExVersion.Value.RowId)
                                                                           .ToDictionary(
                                                                                         g => g.Key,
                                                                                         g => g.Select(x => x.TerritoryId)
                                                                                               .ToList()
                                                                                        );

    private readonly Dictionary<uint, Dictionary<uint, List<uint>>> TerritoryGroupedByExpansionAndZone =
            BRanks.Values
                  .Where(x =>
                                 Svc.Data.GetExcelSheet<TerritoryType>()
                                    .GetRow(x.TerritoryId)
                                    .ExVersion.Value.RowId <=
                                 2)
                  .DistinctBy(x => x.TerritoryId)
                  .GroupBy(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                   .GetRow(x.TerritoryId)
                                   .ExVersion.Value.RowId)
                  .ToDictionary(
                                expansionGroup => expansionGroup.Key,
                                expansionGroup => expansionGroup
                                                 .GroupBy(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                  .GetRow(x.TerritoryId)
                                                                  .PlaceNameZone.Value.RowId)
                                                 .ToDictionary(
                                                               zoneGroup => zoneGroup.Key,
                                                               zoneGroup => zoneGroup.Select(x => x.TerritoryId)
                                                                                     .ToList()
                                                              )
                               );

    internal List<Vector3> FoundSpawns = [];
    private  Map           map;

    internal        List<Vector3>   PossibleSpawnPoints = [];
    internal        uint            SpawnsRecordedFor;
    public override string          Name     => "Bring Your A/B Game";
    public override string          Category => Henchman.Category.Combat;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Gamepad;


    public override Action Help => () =>
                                   {
                                       ImGui.Text("""
                                                  A:
                                                  Just hit "Start" and it will roam all available A-Rank destinations up to Stormbloond.

                                                  B:
                                                  Pick the territory you want to roam and click "Start" to begin.
                                                  It will farm any B-Rank that it can find until you stop it.
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
        if (ImGui.BeginTabBar("RankTabs"))
        {
            using (var tab = ImRaii.TabItem("A-Rank"))
            {
                if (tab)
                    DrawA();
            }

            using (var tab = ImRaii.TabItem("B-Rank"))
            {
                if (tab)
                    DrawB();
            }


            ImGui.EndTabBar();
        }
    }

    private void DrawA()
    {
        var configChanged = false;
        ImGuiHelper.DrawCentered("##ARankStart", () =>
                                         Layout.DrawButton(() =>
                                                           {
                                                               if (ImGui.Button("Start", new Vector2(70, 30)) && !IsTaskEnqueued(Name)) EnqueueTask(new TaskRecord(feature.StartA, Name, onDone: () =>
                                                                                                                                                                                                 {
                                                                                                                                                                                                     Bossmod.DisableAI();
                                                                                                                                                                                                     AutoRotation.Disable();
                                                                                                                                                                                                     ResetCurrentTarget();
                                                                                                                                                                                                 }));
                                                           }));

        ImGuiHelper.DrawCentered("###ARankSelector", () =>
                                            {
                                                if (ImGui.Button("Select All"))
                                                {
                                                    C.EnabledTerritoriesForARank = new SortedSet<uint>(ARankTerritories);
                                                    configChanged                = true;
                                                }

                                                ImGui.SameLine();
                                                if (ImGui.Button("Deselect All"))
                                                {
                                                    C.EnabledTerritoriesForARank.Clear();
                                                    configChanged = true;
                                                }
                                            });


        using var exTabs = ImRaii.TabBar("ExTabs");
        if (exTabs)
        {
            foreach (var expansion in TerritoryGroupedByExpansionAndZone)
            {
                using (var exTab = ImRaii.TabItem(Svc.Data.GetExcelSheet<ExVersion>()
                                                     .GetRow(expansion.Key)
                                                     .Name.ExtractText()))
                {
                    if (exTab)
                    {
                        ImGui.NewLine();
                        ImGuiEx.LineCentered("##ExpansionButtons", () =>
                                                                   {
                                                                       if (ImGui.Button("Select Expansion"))
                                                                       {
                                                                           C.EnabledTerritoriesForARank.AddRange(expansion.Value.Values.SelectMany(e => e));
                                                                           configChanged = true;
                                                                       }

                                                                       ImGui.SameLine();
                                                                       if (ImGui.Button("Deselect Expansion"))
                                                                       {
                                                                           C.EnabledTerritoriesForARank.RemoveWhere(x => expansion.Value.Values.SelectMany(e => e)
                                                                                                                                  .Contains(x));

                                                                           configChanged = true;
                                                                       }
                                                                   });
                        ImGui.NewLine();
                        using var zoneTabs = ImRaii.TabBar("ZoneTabs");
                        if (zoneTabs)
                        {
                            ImGui.NewLine();
                            foreach (var zone in expansion.Value)
                            {
                                using (var zoneTab = ImRaii.TabItem(Svc.Data.GetExcelSheet<PlaceName>()
                                                                       .GetRow(zone.Key)
                                                                       .Name.ExtractText()))
                                {
                                    if (zoneTab)
                                    {
                                        ImGui.NewLine();
                                        ImGuiEx.LineCentered("##ZoneButtons", () =>
                                                                              {
                                                                                  if (ImGui.Button("Select Zone"))
                                                                                  {
                                                                                      C.EnabledTerritoriesForARank.AddRange(zone.Value);
                                                                                      configChanged = true;
                                                                                  }

                                                                                  ImGui.SameLine();
                                                                                  if (ImGui.Button("Deselect Zone"))
                                                                                  {
                                                                                      C.EnabledTerritoriesForARank.RemoveWhere(t => zone.Value.Contains(t));

                                                                                      configChanged = true;
                                                                                  }
                                                                              });
                                        ImGui.NewLine();
                                        ImGuiHelper.DrawCentered(() => { DrawARankTable(zone.Value); });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if (configChanged) EzConfig.Save();
    }

    private void DrawARankTable(List<uint> zone)
    {
        var table = new Table<uint>(
                                    "##KeepsTable",
                                    new List<TableColumn<uint>>
                                    {
                                            new("##enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: x =>
                                                                                                                       {
                                                                                                                           var enabled = C.EnabledTerritoriesForARank.Contains(x);
                                                                                                                           if (ImGui.Checkbox($"##{x}", ref enabled))
                                                                                                                           {
                                                                                                                               if (enabled)
                                                                                                                                   C.EnabledTerritoriesForARank.Add(x);
                                                                                                                               else
                                                                                                                                   C.EnabledTerritoriesForARank.Remove(x);
                                                                                                                           }
                                                                                                                       }),
                                            new("Territory", x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                     .GetRow(x)
                                                                     .PlaceName.Value.Name.GetText(), 200, ColumnAlignment.Center)
                                    },
                                    () => zone,
                                    size: new Vector2(300, 0)
                                   );

        table.Draw();
    }

    private void DrawB()
    {
        var configChanged = false;
        ImGuiHelper.DrawCentered("##BRankStart", () => Layout.DrawButton(() =>
                                                         {
                                                             if (ImGui.Button("Start", new Vector2(70, 30)) && !IsTaskEnqueued(Name))
                                                             {
                                                                 EnqueueTask(new TaskRecord(feature.StartB, Name, onDone: () =>
                                                                                                                          {
                                                                                                                              Bossmod.DisableAI();
                                                                                                                              AutoRotation.Disable();
                                                                                                                              ResetCurrentTarget();
                                                                                                                          }));
                                                                 if (C.TrackBRankSpots)
                                                                 {
                                                                     if (BRanks.TryGetValue(C.BRankToFarm, out var mark))
                                                                     {
                                                                         if (SpawnsRecordedFor != C.BRankToFarm)
                                                                         {
                                                                             SpawnsRecordedFor = C.BRankToFarm;
                                                                             PossibleSpawnPoints.Clear();
                                                                             foreach (var position in mark.Positions) PossibleSpawnPoints.Add(position);
                                                                             FoundSpawns.Clear();
                                                                         }
                                                                     }
                                                                 }
                                                             }
                                                         }));

        ImGuiHelper.DrawCentered(() =>
                                 {
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
                                 });

        if (C.TrackBRankSpots)
        {
            if (BRanks.TryGetValue(C.BRankToFarm, out var mark))
            {
                map = Svc.Data.GetExcelSheet<TerritoryType>()
                         .GetRow(mark.TerritoryId)
                         .Map.Value;
            }

            ImGui.Separator();
            ImGuiHelper.DrawCentered(() =>
                                     {
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
                                     });

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
        var adjustedHeight = regionSize.Y - 5;
        using (var leftChild = ImRaii.Child("###leftChild", regionSize with { X = (regionSize.X / 2) - 5, Y = adjustedHeight }, false, ImGuiWindowFlags.NoDecoration))
        {
            var table = new Table<Vector3>(
                                                        "##BGamePossible",
                                                        new List<TableColumn<Vector3>>
                                                        {
                                                            new("World", pos => $"X: {pos.X} | Y: {pos.Y} | Z: {pos.Z}"),
                                                            new("Map", pos =>
                                                                       {
                                                                           var mapCoords = WorldToMap(pos.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                                                                           return $"X: {mapCoords.X} | Y: {mapCoords.Y}";
                                                                       })
                                                        },
                                                        () => PossibleSpawnPoints);

            table.Draw();
        }
    }

    private void DrawFoundSpawnTable(Vector2 regionSize, Map map)
    {
        var adjustedHeight = regionSize.Y - 5;
        using (var leftChild = ImRaii.Child("###rightChild", regionSize with { X = (regionSize.X / 2) - 5, Y = adjustedHeight }, false, ImGuiWindowFlags.NoDecoration))
        {
            var table = new Table<Vector3>(
                                           "##BGameSpawned",
                                           new List<TableColumn<Vector3>>
                                           {
                                                   new("World", pos => $"X: {pos.X} | Y: {pos.Y} | Z: {pos.Z}"),
                                                   new("Map", pos =>
                                                              {
                                                                  var mapCoords = WorldToMap(pos.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                                                                  return $"X: {mapCoords.X} | Y: {mapCoords.Y}";
                                                              })
                                           },
                                           () => FoundSpawns);

            table.Draw();
        }
    }
}
