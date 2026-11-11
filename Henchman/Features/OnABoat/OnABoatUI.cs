using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using Underlings.GameHelpers;
using Underlings.Keybinds;
using Underlings.Modules;
using Action = System.Action;

namespace Henchman.Features.OnABoat;

[Module]
internal class OnABoatUI : ModuleUI<OnABoat, Configuration>
{
    private static bool ConfigChanged;

    private static readonly Dictionary<string, World> Worlds =
            Svc.Data.GetExcelSheet<World>()
               .DistinctBy(x => x.Name.ExtractText())
               .ToDictionary(x => x.Name.ExtractText(), x => x);

    private readonly Table<OfflineCharacterData> ARTable;
    private readonly TableReorderable<BoatCharacter, Guid> manualTable;
    private BoatCharacter? characterToRemove;
    private string newCharacterName = string.Empty;
    private string newCharacterWorld = string.Empty;

    public OnABoatUI()
    {
        Configuration = LoadConfig<Configuration>() ?? new Configuration();
        var migrateCharacters = Configuration.ManualCharacters is null;
        Configuration.GetManualCharacters();
        if (migrateCharacters) SaveConfig(Configuration);

        ARTable = new Table<OfflineCharacterData>(
                                                  "##ARFisherTable",
                                                  new List<TableColumn<OfflineCharacterData>>
                                                  {
                                                          new("##Enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: (x, index) =>
                                                                                                                                     {
                                                                                                                                         if (!Configuration.EnableCharacterForOCFishing.TryAdd(x.CID, false))
                                                                                                                                         {
                                                                                                                                             var isEnabled = Configuration.EnableCharacterForOCFishing[x.CID];
                                                                                                                                             if (Widgets.Checkbox($"##ocFishing{x.CID}", ref isEnabled))
                                                                                                                                             {
                                                                                                                                                 Configuration.EnableCharacterForOCFishing[x.CID] = isEnabled;
                                                                                                                                                 ConfigChanged                                    = true;
                                                                                                                                             }
                                                                                                                                         }
                                                                                                                                         else
                                                                                                                                             ConfigChanged = true;
                                                                                                                                     }),
                                                          new("Name", x => x.Name, 135, FilterType.String, ColumnAlignment.Center),
                                                          new("World", x => x.World, 90, FilterType.MultiSelect, ColumnAlignment.Center),
                                                          new("DataCenter", x => Worlds[x.World].DataCenterName(), 90, FilterType.MultiSelect, ColumnAlignment.Center),
                                                          new("Lvl", x => x.ClassJobLevelArray[17]
                                                                           .ToString(), 35, Alignment: ColumnAlignment.Center),
                                                          new("Inv.", x => x.InventorySpace.ToString(), 75, Alignment: ColumnAlignment.Center)
                                                  },
                                                  () => Feature.GetCurrentARCharacterData(),
                                                  x => x.CID == Player.CID
                                                 );
        manualTable = CreateManualTable();
    }

    public sealed override Configuration   Configuration { get; init; }
    public override        string          Name          => "On A Boat";
    public override        Enum            Category      => Henchman.Category.Economy;
    public override        FontAwesomeIcon Icon          => FontAwesomeIcon.Sailboat;

    public override Action? Help => () =>
                                    {
                                        ImGui.Text(T("HelpText"));
                                        DrawRequirements(Requirements);
                                    };

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.WahTools, true),
            (IPCNames.AutoRetainer, false),
            (IPCNames.Questionable, false)
    ];

    public override bool LoginNeeded => false;

    public override ModuleAction PrimaryAction => new(Loc.G("UI.V2.Start", "Start"), Start, "On A Boat - Start");

    public override string? Summary => string.Format(T("SummaryCharacters"), Configuration.OCFishingHandleAR
                                                                                     ? Configuration.EnableCharacterForOCFishing.Count(x => x.Value)
                                                                                     : Configuration.GetManualCharacters().Count);

    public override Action? ActionBarInfo => () =>
                                             {
                                                 Widgets.Dot(Feature.IsRegistrationOpen ? V2Palette.Success : V2Palette.TextMuted, 8f);
                                                 ImGui.SameLine(0, 10 * GlobalFontScale);
                                                 Widgets.TextSoft(VoyageTimer());
                                             };

    [Keybind("On A Boat - Start")]
    public void Start()
    {
        if (IsTaskRunning(Name)) return;
        Feature.RunTask();
    }

    public override void Draw()
    {
        ConfigChanged = false;
        if (!SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            ConfigChanged = Configuration.OCFishingHandleAR || Configuration.SellAfterVoyage || Configuration.SellAtLocalVendor;
            Configuration.OCFishingHandleAR = false;
            Configuration.SellAfterVoyage = false;
            Configuration.SellAtLocalVendor = false;
        }

        Widgets.Columns(16f, (0f, () =>
                                  {
                                      DrawModeCard();
                                      DrawCharactersCard();
                                      DrawAfterVoyageCard();
                                  }),
                        (262f, () => Widgets.RequirementsCard(this)));

        if (ConfigChanged) SaveConfig(Configuration);
    }

    private string VoyageTimer()
    {
        var utcNow = DateTime.UtcNow;
        if (Feature.IsRegistrationOpen)
        {
            var remaining = new TimeSpan(0, 14 - utcNow.Minute, 59 - utcNow.Second);
            return string.Format(T("RegistrationOpenFmt"), remaining.Minutes, remaining.Seconds);
        }

        DateTime nextWindowStart;

        var currentWindow = new DateTime(
                                         utcNow.Year, utcNow.Month, utcNow.Day,
                                         utcNow.Hour, 00, 0, DateTimeKind.Utc);

        if (utcNow.Hour % 2 == 0)
        {
            nextWindowStart = utcNow < currentWindow
                                      ? currentWindow
                                      : currentWindow.AddHours(2);
        }
        else
            nextWindowStart = currentWindow.AddHours(1);

        var waitTime = nextWindowStart - utcNow;
        return string.Format(T("NextVoyageFmt"), waitTime.Hours, waitTime.Minutes, waitTime.Seconds);
    }

    private void DrawModeCard()
    {
        Widgets.Card(T("Mode"), () =>
                                {
                                    using (Widgets.Disabled(!SubscriptionManager.IsInitialized(IPCNames.AutoRetainer)))
                                    {
                                        ConfigChanged |= Widgets.Segmented("boatMode", ref Configuration.OCFishingHandleAR, [(false, T("ModeManual")), (true, T("ModeAutoRetainer"))], Widgets.Avail);
                                    }

                                    ImGui.AlignTextToFramePadding();
                                    ImGui.TextUnformatted(T("Route"));
                                    ImGui.SameLine();
                                    Widgets.RightAligned("##boatRoute", () => ConfigChanged |= Widgets.Segmented("boatRoute", ref Configuration.PreferRuby, [(false, "Indigo"), (true, "Ruby")]));

                                    if (!Configuration.OCFishingHandleAR) return;

                                    ConfigChanged |= Widgets.SwitchRow(T("StopAtLevel"), ref Configuration.OCFishingStopLevel);
                                    if (!Configuration.OCFishingStopLevel) return;

                                    ImGui.Indent(16 * GlobalFontScale);
                                    ImGui.SetNextItemWidth(Widgets.Avail);
                                    ConfigChanged |= ImGui.InputInt("##fisherMaxLevel", ref Configuration.MaxLevel);
                                    ImGui.Unindent(16 * GlobalFontScale);
                                });
    }

    private void DrawCharactersCard()
    {
        if (Configuration.OCFishingHandleAR && SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            Widgets.Card(T("Characters"), DrawARCharacters);
            return;
        }

        Widgets.Card(T("Characters"), DrawManualCharacters);
    }

    private void DrawARCharacters()
    {
        if (Widgets.Button(T("SelectAll")))
        {
            foreach (var keyValuePair in Configuration.EnableCharacterForOCFishing) Configuration.EnableCharacterForOCFishing[keyValuePair.Key] = true;
            ConfigChanged = true;
        }

        ImGui.SameLine(0, 6 * GlobalFontScale);
        if (Widgets.Button(T("DeselectAll")))
        {
            foreach (var keyValuePair in Configuration.EnableCharacterForOCFishing) Configuration.EnableCharacterForOCFishing[keyValuePair.Key] = false;
            ConfigChanged = true;
        }

        ImGui.SameLine(0, 6 * GlobalFontScale);
        if (Widgets.Button(T("SelectAllShown")))
        {
            foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForOCFishing[character.CID] = true;
            ConfigChanged = true;
        }

        ImGui.SameLine(0, 6 * GlobalFontScale);
        if (Widgets.Button(T("DeselectAllShown")))
        {
            foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForOCFishing[character.CID] = false;
            ConfigChanged = true;
        }

        DrawFittedTable("##boatARTable", ARTable.FilteredItems.Count, ARTable.Draw);
    }

    private TableReorderable<BoatCharacter, Guid> CreateManualTable() => new(
            "##ManualBoatTable",
            [
                    new(T("CharacterName"), x => x.Name, 150, FilterType.String, ColumnAlignment.Center),
                    new(T("World"), x => x.World, 110, FilterType.MultiSelect, ColumnAlignment.Center),
                    new(T("DataCenter"), x => Worlds.TryGetValue(x.World, out var world) ? world.DataCenterName() : string.Empty,
                        110, FilterType.MultiSelect, ColumnAlignment.Center),
                    new("##Remove", Width: 40, Alignment: ColumnAlignment.Center, DrawCustom: (x, _) =>
                                                                                           {
                                                                                               if (ImGuiComponents.IconButton($"##RemoveBoat{x.Id}", FontAwesomeIcon.Trash))
                                                                                                   characterToRemove = x;
                                                                                           })
            ],
            () => Configuration.GetManualCharacters().ToArray(),
            x => x.Id,
            x => Configuration.GetManualCharacters().FindIndex(h => h.Id == x.Id),
            MoveManualCharacter,
            highlightPredicate: x => Player.Available && x.Name == Player.Name && x.World == Player.HomeWorld.Value.Name.ExtractText(),
            drawExtraRow: DrawNewCharacterRow);

    private void MoveManualCharacter(Guid id, int targetIndex)
    {
        var characters = Configuration.GetManualCharacters();
        var sourceIndex = characters.FindIndex(x => x.Id == id);
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= characters.Count || sourceIndex == targetIndex) return;
        var character = characters[sourceIndex];
        characters.RemoveAt(sourceIndex);
        characters.Insert(targetIndex, character);
        ConfigChanged = true;
    }

    private void DrawManualCharacters()
    {
        Widgets.TextMuted(T("ManualCharactersHelp"), true);
        using (ImRaii.Disabled(IsTaskRunning(Name)))
        {
            DrawFittedTable("##boatManualTable", manualTable.FilteredItems.Count + 1, manualTable.Draw);
        }

        if (characterToRemove is not null)
        {
            Configuration.GetManualCharacters().Remove(characterToRemove);
            characterToRemove = null;
            ConfigChanged = true;
        }
    }

    private void DrawNewCharacterRow()
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(1);
        DrawCentered("##boatNewCharacter", () =>
                                          {
                                              ImGui.SetNextItemWidth(150f * GlobalFontScale);
                                              ImGui.InputText("##boatNewName", ref newCharacterName, 21);
                                          });
        ImGui.TableSetColumnIndex(2);
        DrawCentered("##boatNewWorld", () =>
                                      {
                                          ImGui.SetNextItemWidth(110f * GlobalFontScale);
                                          if (ExcelSheetCombo<World>("##boatNewWorld", out var selectedWorld,
                                                                    _ => newCharacterWorld, x => x.Name.ExtractText(),
                                                                    x => x.IsPublic && x.RowId != 3000 && x.RowId != 3001))
                                              newCharacterWorld = selectedWorld.Name.ExtractText();
                                      });
        ImGui.TableSetColumnIndex(3);
        DrawCentered("##boatNewDataCenter", () =>
                                             {
                                                 ImGui.AlignTextToFramePadding();
                                                 ImGui.TextUnformatted(Worlds.TryGetValue(newCharacterWorld, out var world)
                                                                               ? world.DataCenterName()
                                                                               : string.Empty);
                                             });
        ImGui.TableSetColumnIndex(4);
        DrawCentered("##boatAddCharacter", () =>
                                          {
                                              var name = newCharacterName.Trim();
                                              var valid = !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(newCharacterWorld) &&
                                                          !Configuration.GetManualCharacters().Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && x.World == newCharacterWorld);
                                              using var disabled = ImRaii.Disabled(!valid);
                                              var add = ImGuiComponents.IconButton("##boatAddCharacter", FontAwesomeIcon.Plus);
                                              if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("AddCharacter"));
                                              if (!add) return;
                                              Configuration.GetManualCharacters().Add(new BoatCharacter { Name = name, World = newCharacterWorld });
                                              newCharacterName = string.Empty;
                                              ConfigChanged = true;
                                          });
    }

    private void DrawAfterVoyageCard()
    {
        var arLoaded = SubscriptionManager.IsInitialized(IPCNames.AutoRetainer);
        Widgets.Card(T("AfterVoyage"), () =>
                                       {
                                           ConfigChanged |= Widgets.SwitchRow(T("UseARItemSell"), ref Configuration.SellAfterVoyage, help: T("UseARItemSellHelp"), enabled: arLoaded);
                                           if (Configuration.SellAfterVoyage)
                                               ConfigChanged |= Widgets.SwitchRow(T("UseARLocalSell"), ref Configuration.SellAtLocalVendor, help: T("UseARLocalSellHelp"), child: true, enabled: arLoaded);

                                           ConfigChanged |= Widgets.SwitchRow(T("DiscardAfterVoyage"), ref Configuration.DiscardAfterVoyage);
                                           if (!Configuration.DiscardAfterVoyage) return;

                                           ImGui.Indent(16 * GlobalFontScale);
                                           Widgets.TextDim(T("DiscardProvider"));
                                           HelpMarker(T("DiscardProviderHelp"));
                                           ConfigChanged |= Widgets.Segmented("boatDiscardProvider", ref Configuration.UseFeeshDiscard, [(false, "AutoRetainer"), (true, "WahTools (Feesh)")], Widgets.Avail);
                                           ImGui.Unindent(16 * GlobalFontScale);
                                       });
    }

    private static void DrawFittedTable(string id, int rows, Action drawTable)
    {
        var rowHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().CellPadding.Y * 2;
        using var child = ImRaii.Child(id, new Vector2(Widgets.Avail, (rows + 1) * rowHeight + 6 * GlobalFontScale), false, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
        if (child.Success) drawTable();
    }
}
