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
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Action = System.Action;
using Map = Lumina.Excel.Sheets.Map;

namespace Henchman.Features.BringYourXGame;

[Feature]
public class BringYourXGameUI : FeatureUI<BringYourXGame, Configuration>
{
    private readonly ImmutableSortedSet<uint> ARankTerritories = BRanks
                                                                .Where(x => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                               .GetRow(x.TerritoryId)
                                                                               .ExVersion.Value.RowId <=
                                                                            2)
                                                                .Select(x => x.TerritoryId)
                                                                .ToImmutableSortedSet();

    private readonly BringYourXGame Feature = new();

    private readonly Dictionary<uint, List<uint>> GroupedARankTerritories = BRanks
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
            BRanks
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
    private Map map;

    internal List<Vector3> PossibleSpawnPoints = [];
    internal uint SpawnsRecordedFor;

    public BringYourXGameUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public override string Name => "Bring Your A/B Game";
    public override Category Category => Category.Combat;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Gamepad;


    public override Action Help => () =>
                                   {
                                       ImGui.Text(T("HelpText"));
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

    public override bool LoginNeeded => false;
    public sealed override required Configuration Configuration { get; init; }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("RankTabs"))
        {
            using (var tab = ImRaii.TabItem(T("TabARank")))
            {
                if (tab)
                    DrawA();
            }

            using (var tab = ImRaii.TabItem(T("TabBRank")))
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
                                             if (ImGui.Button(T("SelectAll")))
                                             {
                                                 Configuration.EnabledTerritoriesForARank = new SortedSet<uint>(ARankTerritories);
                                                 configChanged = true;
                                             }

                                             ImGui.SameLine();
                                             if (ImGui.Button(T("DeselectAll")))
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
                                                                       if (ImGui.Button(T("SelectExpansion")))
                                                                       {
                                                                           Configuration.EnabledTerritoriesForARank.AddRange(expansion.Value.Values.SelectMany(e => e));
                                                                           configChanged = true;
                                                                       }

                                                                       ImGui.SameLine();
                                                                       if (ImGui.Button(T("DeselectExpansion")))
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
                                                                                  if (ImGui.Button(T("SelectZone")))
                                                                                  {
                                                                                      Configuration.EnabledTerritoriesForARank.AddRange(zone.Value);
                                                                                      configChanged = true;
                                                                                  }

                                                                                  ImGui.SameLine();
                                                                                  if (ImGui.Button(T("DeselectZone")))
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
                                            new(T("ColTerritory"), x => Svc.Data.GetExcelSheet<TerritoryType>()
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
                                                                         if (GetBRank(Configuration.BRankToFarm) is { } mark)
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
                         ImGui.Text(T("BRankToFarm"));
                         ImGui.SameLine(150);
                         ImGui.SetNextItemWidth(150f);
                         if (ImGuiEx.ExcelSheetCombo<BNpcName>("##bRank", out var brank, s => s.GetRowOrDefault(Configuration.BRankToFarm) is { } row
                                                                                                      ? ToTitleCaseExtended(s.GetRow(Configuration.BRankToFarm)
                                                                                                                             .Singular.ExtractText(), Svc.ClientState.ClientLanguage)
                                                                                                      : string.Empty, x => ToTitleCaseExtended(x.Singular.ExtractText(), Svc.ClientState.ClientLanguage), x => BRanks.Any(b => b.BNpcNameRowId == x.RowId)))
                         {
                             Configuration.BRankToFarm = brank.RowId;
                             configChanged = true;
                         }

                         ImGui.Text(T("TrackFoundSpots"));
                         ImGui.SameLine();
                         configChanged |= ImGui.Checkbox("##echoBRanks", ref C.TrackBRankSpots);
                     });

        if (C.TrackBRankSpots)
        {
            if (GetBRank(Configuration.BRankToFarm) is { } mark)
            {
                map = Svc.Data.GetExcelSheet<TerritoryType>()
                         .GetRow(mark.TerritoryId)
                         .Map.Value;
            }

            ImGui.Separator();
            DrawCentered(() =>
                         {
                             if (ImGui.Button(T("WritePositions")))
                             {
                                 var sb = new StringBuilder();
                                 sb.AppendLine(T("LeftPositions"));
                                 foreach (var position in PossibleSpawnPoints)
                                 {
                                     sb.AppendLine($"World - X: {position.X} | Y: {position.Y} | Z: {position.Z}");
                                     var mapCoords = WorldToMap(position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                                     sb.AppendLine($"Map - X: {mapCoords.X} | Y: {mapCoords.Y}");
                                     sb.AppendLine("");
                                 }

                                 sb.AppendLine(T("RegisteredPositions"));
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
                             if (ImGui.Button(T("PrintSpawnLocations")))
                             {
                                 var selectedMark = GetBRank(Configuration.BRankToFarm)!;
                                 var mapRow = Svc.Data.GetExcelSheet<TerritoryType>()
                                                 .GetRow(selectedMark.TerritoryId)
                                                 .Map.Value;
                                 foreach (var position in FoundSpawns)
                                 {
                                     var mapCoords = WorldToMap(position.ToVector2(), mapRow.OffsetX, mapRow.OffsetY, mapRow.SizeFactor);
                                     var mapLink = SeString.CreateMapLink(mapRow.PlaceName.Value.Name.ExtractText(), mapCoords.X, mapCoords.Y);
                                     var message = new XivChatEntry
                                     {
                                         Type = XivChatType.Echo,
                                         Message = new SeStringBuilder().AddUiForeground($"B-Rank {selectedMark.Name} found @ ", 561)
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
                                                   new(T("ColWorld"), pos => $"X: {pos.X} | Y: {pos.Y} | Z: {pos.Z}"),
                                                   new(T("ColMap"), pos =>
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
                                                   new(T("ColWorld"), pos => $"X: {pos.X} | Y: {pos.Y} | Z: {pos.Z}"),
                                                   new(T("ColMap"), pos =>
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
