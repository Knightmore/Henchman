using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using Underlings.Configuration;
using Underlings.Keybinds;
using Underlings.Modules;
using Action = System.Action;
using Map = Lumina.Excel.Sheets.Map;

namespace Henchman.Features.BringYourXGame;

[Module]
public class BringYourXGameUI : ModuleUI<BringYourXGame, Configuration>
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
    private  Map           map;

    internal List<Vector3> PossibleSpawnPoints = [];
    private  uint          selectedExpansion;
    internal uint          SpawnsRecordedFor;

    public BringYourXGameUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public override string          Name     => "Bring Your A/B Game";
    public override Enum            Category => Henchman.Category.Combat;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Gamepad;


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

    public override                 bool          LoginNeeded   => false;
    public sealed override required Configuration Configuration { get; init; }

    public override IReadOnlyList<string> Tabs => [T("TabARank"), T("TabBRank")];

    public override ModuleAction PrimaryAction => SelectedTab == 1
                                                          ? new ModuleAction(T("StartBRank"), StartBRank, "Bring Your A/B Game - Start B-Rank")
                                                          : new ModuleAction(T("StartARank"), StartARank, "Bring Your A/B Game - Start A-Rank");

    [Keybind("Bring Your A/B Game - Start A-Rank")]
    private void StartARank()
    {
        if (IsTaskRunning(Name)) return;
        Feature.RunTask(true);
    }

    [Keybind("Bring Your A/B Game - Start B-Rank")]
    private void StartBRank()
    {
        if (IsTaskRunning(Name)) return;

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

    public override void Draw()
    {
        if (SelectedTab == 1)
            DrawB();
        else
            DrawA();
    }

    private void DrawA()
    {
        var configChanged = false;
        var rowH          = ImGui.GetTextLineHeight() + 20 * GlobalFontScale;
        var expansions = TerritoryGroupedByExpansionAndZone.Keys
                                                           .Select(x => (x, Svc.Data.GetExcelSheet<ExVersion>()
                                                                               .GetRow(x)
                                                                               .Name.ExtractText()))
                                                           .ToList();
        var counts = TerritoryGroupedByExpansionAndZone.Values
                                                       .Select(x => SelectionCount(x.Values.SelectMany(e => e).ToList()))
                                                       .ToList();

        Widgets.Segmented("##aRankExpansion", ref selectedExpansion, expansions, counts: counts);
        if (TerritoryGroupedByExpansionAndZone.TryGetValue(selectedExpansion, out var expansion))
        {
            ImGui.SameLine(0, 8 * GlobalFontScale);
            if (Widgets.IconButton("selectExpansion", FontAwesomeIcon.CheckDouble, T("SelectExpansion"), ButtonKind.Secondary, rowH / GlobalFontScale))
            {
                Configuration.EnabledTerritoriesForARank.UnionWith(expansion.Values.SelectMany(e => e));
                configChanged = true;
            }

            ImGui.SameLine(0, 6 * GlobalFontScale);
            if (Widgets.IconButton("deselectExpansion", FontAwesomeIcon.Times, T("DeselectExpansion"), ButtonKind.Secondary, rowH / GlobalFontScale))
            {
                Configuration.EnabledTerritoriesForARank.RemoveWhere(x => expansion.Values.SelectMany(e => e)
                                                                                   .Contains(x));

                configChanged = true;
            }
        }

        ImGui.SameLine();
        Widgets.RightAligned("##aRankToolbar", () =>
                                               {
                                                   Widgets.VCenter(rowH, 0, () => Widgets.TextMuted(string.Format(T("TerritoriesSelectedFmt"),
                                                                                                                  ARankTerritories.Count(Configuration.EnabledTerritoriesForARank.Contains),
                                                                                                                  ARankTerritories.Count)));
                                                   ImGui.SameLine(0, 12 * GlobalFontScale);
                                                   if (Widgets.Button(T("SelectAll"), ButtonKind.Secondary, null, new Vector2(0, rowH)))
                                                   {
                                                       Configuration.EnabledTerritoriesForARank = new SortedSet<uint>(ARankTerritories);
                                                       configChanged                            = true;
                                                   }

                                                   ImGui.SameLine(0, 6 * GlobalFontScale);
                                                   if (Widgets.Button(T("DeselectAll"), ButtonKind.Secondary, null, new Vector2(0, rowH)))
                                                   {
                                                       Configuration.EnabledTerritoriesForARank.Clear();
                                                       configChanged = true;
                                                   }
                                               });

        ImGui.Dummy(new Vector2(0, 4 * GlobalFontScale));
        if (expansion != null)
            Widgets.Grid(3, 12f, expansion.Select(zone => new Action<float>(width => configChanged |= DrawZoneCard(zone.Key, zone.Value, width)))
                                          .ToList());

        if (configChanged) SaveConfig(Configuration);
    }

    private string SelectionCount(List<uint> territories) => $"{territories.Count(Configuration.EnabledTerritoriesForARank.Contains)}/{territories.Count}";

    private bool DrawZoneCard(uint zoneId, List<uint> territories, float width)
    {
        var configChanged = false;
        var headerButton  = new Vector2(0, ImGui.GetTextLineHeight() + 4 * GlobalFontScale);

        Widgets.Card(Svc.Data.GetExcelSheet<PlaceName>()
                        .GetRow(zoneId)
                        .Name.ExtractText(), () =>
                                             {
                                                 foreach (var territory in territories)
                                                 {
                                                     var enabled = Configuration.EnabledTerritoriesForARank.Contains(territory);
                                                     if (!Widgets.Checkbox($"{Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territory).PlaceName.Value.Name.ExtractText()}##aRank{territory}",
                                                                           ref enabled)) continue;

                                                     if (enabled)
                                                         Configuration.EnabledTerritoriesForARank.Add(territory);
                                                     else
                                                         Configuration.EnabledTerritoriesForARank.Remove(territory);
                                                     configChanged = true;
                                                 }
                                             }, width, () =>
                                                       {
                                                           Widgets.TextMuted(SelectionCount(territories));
                                                           ImGui.SameLine(0, 6 * GlobalFontScale);
                                                           if (Widgets.Button($"{T("All")}##selectZone{zoneId}", ButtonKind.Ghost, null, headerButton, tooltip: T("SelectZone")))
                                                           {
                                                               Configuration.EnabledTerritoriesForARank.UnionWith(territories);
                                                               configChanged = true;
                                                           }

                                                           ImGui.SameLine(0, 2 * GlobalFontScale);
                                                           if (Widgets.Button($"{T("None")}##deselectZone{zoneId}", ButtonKind.Ghost, null, headerButton, tooltip: T("DeselectZone")))
                                                           {
                                                               Configuration.EnabledTerritoriesForARank.RemoveWhere(t => territories.Contains(t));
                                                               configChanged = true;
                                                           }
                                                       });

        return configChanged;
    }

    private void DrawB()
    {
        var configChanged = false;

        if (C.TrackBRankSpots && GetBRank(Configuration.BRankToFarm) is { } mark)
        {
            map = Svc.Data.GetExcelSheet<TerritoryType>()
                     .GetRow(mark.TerritoryId)
                     .Map.Value;
        }

        Widgets.Card(null, () =>
                           {
                               var rowY = ImGui.GetCursorScreenPos().Y;
                               ImGui.AlignTextToFramePadding();
                               ImGui.TextUnformatted(T("BRankToFarm"));
                               ImGui.SameLine();
                               Widgets.RightAligned("##bRankToFarm", () =>
                                                                     {
                                                                         ImGui.SetNextItemWidth(220f * GlobalFontScale);
                                                                         if (ExcelSheetCombo<BNpcName>("##bRank", out var brank, s => s.GetRowOrDefault(Configuration.BRankToFarm) is { } row
                                                                                                                                              ? ToTitleCaseExtended(s.GetRow(Configuration.BRankToFarm)
                                                                                                                                                                     .Singular.ExtractText(), Svc.ClientState.ClientLanguage)
                                                                                                                                              : string.Empty, x => ToTitleCaseExtended(x.Singular.ExtractText(), Svc.ClientState.ClientLanguage), x => BRanks.Any(b => b.BNpcNameRowId == x.RowId)))
                                                                         {
                                                                             Configuration.BRankToFarm = brank.RowId;
                                                                             configChanged             = true;
                                                                         }
                                                                     }, rowY);

                               configChanged |= Widgets.SwitchRow(T("TrackFoundSpots"), ref C.TrackBRankSpots);
                               if (!C.TrackBRankSpots) return;

                               ImGui.Dummy(new Vector2(0, 4 * GlobalFontScale));
                               if (Widgets.Button(T("WritePositions"))) WritePositions();
                               ImGui.SameLine(0, 6 * GlobalFontScale);
                               if (Widgets.Button(T("PrintSpawnLocations"))) PrintSpawnLocations();
                           });

        if (C.TrackBRankSpots)
        {
            Widgets.Columns(16f, (0f, () => DrawSpawnTable(T("LeftPositions"), "##BGamePossible", PossibleSpawnPoints)),
                            (0f, () => DrawSpawnTable(T("RegisteredPositions"), "##BGameSpawned", FoundSpawns)));
        }

        if (configChanged)
        {
            PluginConfig.Save();
            SaveConfig(Configuration);
        }
    }

    private void WritePositions()
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

        TaskLog.Info(sb.ToString());
    }

    private void PrintSpawnLocations()
    {
        var selectedMark = GetBRank(Configuration.BRankToFarm)!;
        var mapRow = Svc.Data.GetExcelSheet<TerritoryType>()
                        .GetRow(selectedMark.TerritoryId)
                        .Map.Value;
        foreach (var position in FoundSpawns)
        {
            var mapCoords = WorldToMap(position.ToVector2(), mapRow.OffsetX, mapRow.OffsetY, mapRow.SizeFactor);
            var mapLink   = SeString.CreateMapLink(mapRow.PlaceName.Value.Name.ExtractText(), mapCoords.X, mapCoords.Y);
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

    private void DrawSpawnTable(string title, string id, List<Vector3> positions)
    {
        var rows = positions.ToList();
        Widgets.Card(title, () =>
                            {
                                using var table = ImRaii.Table(id, 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp,
                                                               new Vector2(Widgets.Avail, 0));
                                if (!table.Success) return;

                                ImGui.TableSetupColumn(T("ColWorld").ToUpperInvariant());
                                ImGui.TableSetupColumn(T("ColMap").ToUpperInvariant());
                                using (ImRaii.PushColor(ImGuiCol.Text, V2Palette.TextMuted))
                                {
                                    ImGui.TableHeadersRow();
                                }

                                foreach (var pos in rows)
                                {
                                    var mapCoords = WorldToMap(pos.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    ImGui.TextUnformatted($"X: {pos.X} | Y: {pos.Y} | Z: {pos.Z}");
                                    ImGui.TableNextColumn();
                                    ImGui.TextUnformatted($"X: {mapCoords.X} | Y: {mapCoords.Y}");
                                }
                            }, 0f, () => Widgets.TextMuted(rows.Count.ToString()));
    }
}
