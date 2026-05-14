using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using ECommons.MathHelpers;
using Henchman.Abstractions;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using Action = System.Action;
using Map = Lumina.Excel.Sheets.Map;

namespace Henchman.Features.BringYourXGame;

[Feature]
public class BringYourXGameUI : FeatureUI<BringYourXGame, Configuration>
{
    private readonly ImmutableSortedSet<uint> ARankTerritories = BRanks
                                                                .Values
                                                                .Where(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                               .GetRow(x.TerritoryId)
                                                                               .ExVersion.Value.RowId <=
                                                                            2)
                                                                .Select(x => x.TerritoryId)
                                                                .ToImmutableSortedSet();

    private readonly BringYourXGame Feature = new();

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

    internal List<Vector3> PossibleSpawnPoints = [];
    internal uint          SpawnsRecordedFor;

    public BringYourXGameUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public override string          Name     => "Bring Your A/B Game";
    public override Category        Category => Category.Combat;
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


                                       DrawRequirements(Requirements);
                                   };

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.BossMod, false),
            (IPCNames.Wrath, false),
            (IPCNames.RotationSolverReborn, false)
    ];

    public override                 bool          LoginNeeded   => false;
    public sealed override required Configuration Configuration { get; init; }

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
        DrawCentered("##ARankStart", () =>
                                             Layout.DrawButton(() =>
                                                               {
                                                                   if (StartButton() && !IsTaskEnqueued(Name)) Feature.RunTask(true);
                                                               }));

        DrawCentered("###ARankSelector", () =>
                                         {
                                             if (ImGui.Button("Select All"))
                                             {
                                                 Configuration.EnabledTerritoriesForARank = new SortedSet<uint>(ARankTerritories);
                                                 configChanged                            = true;
                                             }

                                             ImGui.SameLine();
                                             if (ImGui.Button("Deselect All"))
                                             {
                                                 Configuration.EnabledTerritoriesForARank.Clear();
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
                                                                           Configuration.EnabledTerritoriesForARank.AddRange(expansion.Value.Values.SelectMany(e => e));
                                                                           configChanged = true;
                                                                       }

                                                                       ImGui.SameLine();
                                                                       if (ImGui.Button("Deselect Expansion"))
                                                                       {
                                                                           Configuration.EnabledTerritoriesForARank.RemoveWhere(x => expansion.Value.Values.SelectMany(e => e)
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
                                                                                      Configuration.EnabledTerritoriesForARank.AddRange(zone.Value);
                                                                                      configChanged = true;
                                                                                  }

                                                                                  ImGui.SameLine();
                                                                                  if (ImGui.Button("Deselect Zone"))
                                                                                  {
                                                                                      Configuration.EnabledTerritoriesForARank.RemoveWhere(t => zone.Value.Contains(t));

                                                                                      configChanged = true;
                                                                                  }
                                                                              });
                                        ImGui.NewLine();
                                        DrawCentered("AGameTable", () => { DrawARankTable(zone.Value); });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if (configChanged) SaveConfig(Configuration);
    }

    private void DrawARankTable(List<uint> zone)
    {
        var table = new Table<uint>(
                                    "##KeepsTable",
                                    new List<TableColumn<uint>>
                                    {
                                            new("##enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: (x, index) =>
                                                                                                                       {
                                                                                                                           var enabled = Configuration.EnabledTerritoriesForARank.Contains(x);
                                                                                                                           if (ImGui.Checkbox($"##{x}", ref enabled))
                                                                                                                           {
                                                                                                                               if (enabled)
                                                                                                                                   Configuration.EnabledTerritoriesForARank.Add(x);
                                                                                                                               else
                                                                                                                                   Configuration.EnabledTerritoriesForARank.Remove(x);
                                                                                                                           }
                                                                                                                       }),
                                            new("Territory", x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                     .GetRow(x)
                                                                     .PlaceName.Value.Name.GetText(), 200, Alignment: ColumnAlignment.Center)
                                    },
                                    () => zone,
                                    size: new Vector2(300, 0)
                                   );

        table.Draw();
    }

    private void DrawB()
    {
        var configChanged = false;
        DrawCentered("##BRankStart", () => Layout.DrawButton(() =>
                                                             {
                                                                 if (StartButton() && !IsTaskEnqueued(Name))
                                                                 {
                                                                     Feature.RunTask(false);

                                                                     if (C.TrackBRankSpots)
                                                                     {
                                                                         if (BRanks.TryGetValue(Configuration.BRankToFarm, out var mark))
                                                                         {
                                                                             if (SpawnsRecordedFor != Configuration.BRankToFarm)
                                                                             {
                                                                                 SpawnsRecordedFor = Configuration.BRankToFarm;
                                                                                 PossibleSpawnPoints.Clear();
                                                                                 foreach (var position in mark.Positions) PossibleSpawnPoints.Add(position);
                                                                                 FoundSpawns.Clear();
                                                                             }
                                                                         }
                                                                     }
                                                                 }
                                                             }));

        DrawCentered(() =>
                     {
                         ImGui.Text("B-Rank to Farm:");
                         ImGui.SameLine(150);
                         ImGui.SetNextItemWidth(150f);
                         if (ImGuiEx.ExcelSheetCombo<BNpcName>("##bRank", out var brank, s => s.GetRowOrDefault(Configuration.BRankToFarm) is { } row
                                                                                                      ? ToTitleCaseExtended(s.GetRow(Configuration.BRankToFarm)
                                                                                                                             .Singular.ExtractText(), Svc.ClientState.ClientLanguage)
                                                                                                      : string.Empty, x => ToTitleCaseExtended(x.Singular.ExtractText(), Svc.ClientState.ClientLanguage), x => BRanks.Keys.Any(b => b == x.RowId)))
                         {
                             Configuration.BRankToFarm = brank.RowId;
                             configChanged             = true;
                         }

                         ImGui.Text("Track found B-Rank spots");
                         ImGui.SameLine(200);
                         configChanged |= ImGui.Checkbox("##echoBRanks", ref C.TrackBRankSpots);
                     });

        if (C.TrackBRankSpots)
        {
            if (BRanks.TryGetValue(Configuration.BRankToFarm, out var mark))
            {
                map = Svc.Data.GetExcelSheet<TerritoryType>()
                         .GetRow(mark.TerritoryId)
                         .Map.Value;
            }

            ImGui.Separator();
            DrawCentered(() =>
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

                                 TaskLog(sb.ToString());
                             }

                             ImGui.SameLine();
                             if (ImGui.Button("Print spawn locations to chat"))
                             {
                                 var mapRow = Svc.Data.GetExcelSheet<TerritoryType>()
                                                 .GetRow(BRanks[Configuration.BRankToFarm].TerritoryId)
                                                 .Map.Value;
                                 foreach (var position in FoundSpawns)
                                 {
                                     var mapCoords = WorldToMap(position.ToVector2(), mapRow.OffsetX, mapRow.OffsetY, mapRow.SizeFactor);
                                     var mapLink   = SeString.CreateMapLink(mapRow.PlaceName.Value.Name.ExtractText(), mapCoords.X, mapCoords.Y);
                                     var message = new XivChatEntry
                                                   {
                                                           Type = XivChatType.Echo,
                                                           Message = new SeStringBuilder().AddUiForeground($"B-Rank {BRanks[Configuration.BRankToFarm].Name} found @ ", 561)
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

        if (configChanged)
        {
            EzConfig.Save();
            SaveConfig(Configuration);
        }
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
