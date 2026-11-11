using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Henchman.Models;
using Henchman.Multiboxing;
using Lumina.Excel.Sheets;
using Underlings.GameHelpers;
using Underlings.Keybinds;
using Underlings.Modules;
using Underlings.TaskManager;
using Action = System.Action;

namespace Henchman.Features.TestyTrader;

[Module]
public class TestyTraderUI : ModuleUI<TestyTrader, Configuration>
{
    public override void Dispose() => GameItemTooltip.Dispose();

    private static readonly Item[] TradableItems = Svc.Data.GetExcelSheet<Item>()
                                                      .Where(x => x is { IsUntradable: false, RowId: > 0 and < 20 or > 100 } && !string.IsNullOrEmpty(x.Name.ExtractText()))
                                                      .OrderBy(x => x.Name.ExtractText())
                                                      .ToArray();

    private static readonly TestyTrader Feature = new();
    private static          bool        ConfigChanged;

    private static readonly Dictionary<string, World> Worlds =
            Svc.Data.GetExcelSheet<World>()
               .DistinctBy(x => x.Name.ExtractText())
               .ToDictionary(x => x.Name.ExtractText(), x => x);

    private readonly Table<OfflineCharacterData> ARTable;
    private readonly TableReorderable<TestyTraderCharacterData, Guid> manualTable;

    private readonly List<(Item Item, uint RowId, string DisplayName)> expandedItems = TradableItems
                                                                                      .SelectMany(x =>
                                                                                                  {
                                                                                                      var baseEntry = (Item: x, x.RowId, DisplayName: x.Name.ExtractText());
                                                                                                      if (x.CanBeHq)
                                                                                                      {
                                                                                                          var hqEntry = (Item: x, RowId: x.RowId + 1_000_000, DisplayName: x.Name.ExtractText() + " (HQ)");
                                                                                                          return [baseEntry, hqEntry];
                                                                                                      }

                                                                                                      return new[] { baseEntry };
                                                                                                  })
                                                                                      .ToList();

    private readonly TableColumn<OfflineCharacterData> invColumn;

    private readonly List<ItemSearchCategory> searchCategories = Svc.Data.GetExcelSheet<ItemSearchCategory>()
                                                                    .Where(x => x.Category > 1)
                                                                    .OrderBy(x => x.Category)
                                                                    .ToList();

    internal TestyTraderCharacterData? CharacterToRemove;

    private TestyTraderCharacterData newCharacter = new();
    private TradeList?               planToDelete;
    private string                   newPlanName = string.Empty;

    internal string              Search = string.Empty;
    private  ItemSearchCategory? selectedSearchCategory;

    public TestyTraderUI()
    {
        Configuration = LoadConfig<Configuration>() ?? new Configuration();

        if (Configuration.TradeLists.Count == 0)
        {
            var migrated = new TradeList
                           {
                                   Name    = "Plan 1",
                                   Entries = Configuration.TradeEntries
                           };
            Configuration.TradeEntries        = [];
            Configuration.TradeLists.Add(migrated);
            Configuration.SelectedTradeListId = migrated.Id;
            SaveConfig(Configuration);
        }

        invColumn = new TableColumn<OfflineCharacterData>("Inv.", Width: 90, Alignment: ColumnAlignment.Center, Modes:
                                                          [
                                                                  new ColumnModeOption<OfflineCharacterData>("Inv.", x => x.InventorySpace.ToString()),
                                                                  new ColumnModeOption<OfflineCharacterData>("Tanks", x => x.Ceruleum.ToString()),
                                                                  new ColumnModeOption<OfflineCharacterData>("Repair", x => x.RepairKits.ToString())
                                                          ])
                    {
                            SelectedModeIndex = (int)Configuration.TestyTraderInvColumnMode
                    };

        ARTable = new Table<OfflineCharacterData>(
                                                  "##ARTraderTable",
                                                  new List<TableColumn<OfflineCharacterData>>
                                                  {
                                                          new("##Enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: (x, index) =>
                                                                                                                                     {
                                                                                                                                         if (!Configuration.EnableCharacterForTrade.TryAdd(x.CID, false))
                                                                                                                                         {
                                                                                                                                             var isEnabled = Configuration.EnableCharacterForTrade[x.CID];
                                                                                                                                             ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetStyle().FramePadding.Y);
                                                                                                                                             if (Widgets.Checkbox($"##enabled{x.CID}", ref isEnabled))
                                                                                                                                             {
                                                                                                                                                 Configuration.EnableCharacterForTrade[x.CID] = isEnabled;
                                                                                                                                                 ConfigChanged                                = true;
                                                                                                                                             }
                                                                                                                                         }
                                                                                                                                         else
                                                                                                                                             ConfigChanged = true;
                                                                                                                                     }),
                                                          new("Name", x => x.Name, 135, FilterType.String, ColumnAlignment.Center),
                                                          new("World", x => x.World, 90, FilterType.MultiSelect, ColumnAlignment.Center),
                                                          new("DataCenter", x => Worlds[x.World].DataCenterName(), 90, FilterType.MultiSelect, ColumnAlignment.Center),
                                                          new("Subs", x => x.OfflineSubmarineData.Count.ToString(), 35, Alignment: ColumnAlignment.Center),
                                                          new("AR active", x => x.WorkshopEnabled.ToString(), 75, FilterType.MultiSelect, ColumnAlignment.Center),
                                                          invColumn
                                                  },
                                                  () => Feature.GetCurrentARCharacterData(),
                                                  x => x.CID == Player.CID,
                                                  Vector2.Zero
                                                 );
        manualTable = CreateManualTable();
    }

    public sealed override required Configuration   Configuration { get; init; }

    private TradeList ActivePlan => Configuration.GetActivePlan()!;

    public override                 string          Name          => "Testy Trader";
    public override                 Enum            Category      => Henchman.Category.Economy;
    public override                 FontAwesomeIcon Icon          => FontAwesomeIcon.Handshake;

    public override Action? Help => () =>
                                    {
                                        ImGui.Text(T("HelpText"));
                                        DrawRequirements(Requirements);
                                    };

    public override bool LoginNeeded => false;

    public override IReadOnlyList<string>? Tabs => Configuration.TradeSession == SessionType.Henchman
                                                           ? [T("TabCharacters"), T("TabItems")]
                                                           : null;

    public override ModuleAction PrimaryAction => Configuration.TradeSession == SessionType.Boss
                                                          ? new ModuleAction(T("StartBossMode"), StartBossMode, "Testy Trader - Start (Boss Mode)")
                                                          : new ModuleAction(T("StartHenchmanMode"), StartHenchmanMode, "Testy Trader - Start (Henchman Mode)");

    public override Action? ActionBarInfo => Configuration.TradeSession == SessionType.Henchman
                                                     ? () =>
                                                       {
                                                           Widgets.Dot(V2Palette.Teal, 8f);
                                                           ImGui.SameLine(0, 10 * GlobalFontScale);
                                                           Widgets.TextSoft(string.Format(T("ActionBarHenchman"), EnabledHenchmenCount(), ActivePlan.Name, ActivePlan.Entries.Count(x => x.Enabled)));
                                                       }
                                                     : null;

    [Keybind("Testy Trader - Start")]
    private void StartTask()
    {
        if (IsTaskRunning(Name)) return;
        TryStartTask(Configuration.TradeSession == SessionType.Boss
                             ? new TaskRecord(Feature.Server, "Testy Trader - Boss Mode")
                             : new TaskRecord(Feature.Client, "Testy Trader - Henchman Mode"));
    }

    [Keybind("Testy Trader - Start (Boss Mode)")]
    private void StartBossMode()
    {
        if (IsTaskRunning(Name)) return;
        TryStartTask(new TaskRecord(Feature.Server, "Testy Trader - Boss Mode"));
    }

    [Keybind("Testy Trader - Start (Henchman Mode)")]
    private void StartHenchmanMode()
    {
        if (IsTaskRunning(Name)) return;
        TryStartTask(new TaskRecord(Feature.Client, "Testy Trader - Henchman Mode"));
    }

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true)
    ];

    public override void Draw()
    {
        ConfigChanged = false;
        if (!SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            ConfigChanged                      = Configuration.TestyTraderARSupport || Configuration.UseARItemSell;
            Configuration.TestyTraderARSupport = false;
            Configuration.UseARItemSell        = false;
        }

        ConfigChanged |= Widgets.Segmented("##tradeSession", ref Configuration.TradeSession,
                                           [(SessionType.Boss, T("SessionBoss")), (SessionType.Henchman, T("SessionHenchman"))], activeColor: V2Palette.Teal);
        ImGui.Dummy(new Vector2(0, 4 * GlobalFontScale));

        if (Configuration.TradeSession == SessionType.Boss)
            Widgets.Columns(16f, (0f, DrawBossOptions), (262f, () => Widgets.RequirementsCard(this)));
        else if (SelectedTab == 0)
            DrawCharacterTab();
        else
            DrawItemTab();

        if (CharacterToRemove != null)
        {
            Configuration.TestyTraderImportedCharacters.Remove(CharacterToRemove);
            CharacterToRemove = null;
            ConfigChanged     = true;
        }

        if (planToDelete != null)
        {
            Configuration.TradeLists.Remove(planToDelete);
            if (Configuration.SelectedTradeListId == planToDelete.Id)
                Configuration.SelectedTradeListId = Configuration.TradeLists[0].Id;
            planToDelete  = null;
            ConfigChanged = true;
        }

        if (ConfigChanged) SaveConfig(Configuration);
    }

    private int EnabledHenchmenCount() => Configuration.TestyTraderARSupport && SubscriptionManager.IsInitialized(IPCNames.AutoRetainer)
                                                  ? Feature.GetCurrentARCharacterData()
                                                           .Count(x => Configuration.EnableCharacterForTrade.GetValueOrDefault(x.CID))
                                                  : Configuration.TestyTraderImportedCharacters.Count(x => x.Enabled);

    private void DrawBossOptions()
    {
        Widgets.Card(T("Options"), () =>
                                   {
                                       ConfigChanged |= Widgets.SwitchRow(T("UseARItemSell"), ref Configuration.UseARItemSell,
                                                                          enabled: SubscriptionManager.IsInitialized(IPCNames.AutoRetainer));
                                       ConfigChanged |= Widgets.SwitchRow(T("TransferBossToHenchman"), ref Configuration.MoveBossToHenchman, help: T("TransferBossHelp"));
                                       ConfigChanged |= Widgets.SwitchRow(T("IncludeArmory"), ref Configuration.IncludeArmory);
                                   });
    }

    private void DrawARTable()
    {
        Widgets.FittedTable("##ARTableBox", ARTable.FilteredItems.Count + 1, ARTable.Draw);

        if ((int)Configuration.TestyTraderInvColumnMode != invColumn.SelectedModeIndex)
        {
            Configuration.TestyTraderInvColumnMode = (TestyTraderInvColumnMode)invColumn.SelectedModeIndex;
            ConfigChanged                          = true;
        }
    }

    private void DrawManualTable() => Widgets.FittedTable("##ManualTableBox", Configuration.TestyTraderImportedCharacters.Count + 2, manualTable.Draw);

    private TableReorderable<TestyTraderCharacterData, Guid> CreateManualTable()
    {
        var characterColumns = new List<TableColumn<TestyTraderCharacterData>>
                               {
                                       new("##Enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: (x, index) =>
                                                                                                                  {
                                                                                                                      ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetStyle().FramePadding.Y);
                                                                                                                      ConfigChanged |= Widgets.Checkbox($"##enabled{x.Id}", ref x.Enabled);
                                                                                                                  }),
                                       new(T("ColName"), x => x.Name, 135, FilterType.String, ColumnAlignment.Center),
                                       new(T("ColDataCenter"), x => Svc.Data.GetExcelSheet<WorldDCGroupType>()
                                                                       .GetRow(x.DataCenterId)
                                                                       .Name.ExtractText(), 100, FilterType.MultiSelect, ColumnAlignment.Center),
                                       new(T("ColWorld"), x => Svc.Data.GetExcelSheet<World>()
                                                                  .GetRow(x.WorldId)
                                                                  .Name.ExtractText(), 100, FilterType.MultiSelect, ColumnAlignment.Center),
                                       new("##Remove", Width: 75, Alignment: ColumnAlignment.Center, DrawCustom: (x, index) =>
                                                                                                                 {
                                                                                                                     if (ImGuiComponents.IconButton($"##Remove{x.Name + x.WorldId}", FontAwesomeIcon.Trash)) CharacterToRemove = x;
                                                                                                                 })
                               };

        return new TableReorderable<TestyTraderCharacterData, Guid>(
                                                        "##ManualTraderTable",
                                                        characterColumns,
                                                        () => Configuration.TestyTraderImportedCharacters.ToArray(),
                                                        x => x.Id,
                                                        x => Configuration.TestyTraderImportedCharacters.FindIndex(h => h.Id == x.Id),
                                                        MoveManualCharacter,
                                                        size: Vector2.Zero,
                                                        highlightPredicate: h => Svc.Objects.LocalPlayer != null && h.Name == Player.Name && h.WorldId == Player.HomeWorld.RowId,
                                                        drawExtraRow: () =>
                                                        {
                                                            ImGui.TableNextRow();
                                                            using var row = new ColumnScope(characterColumns.Count + 1);
                                                            row.TableNextColumn();
                                                            row.TableNextColumn();
                                                            row.TableNextColumn();
                                                            DrawCentered("##TraderNewCharacterName", () =>
                                                                                                     {
                                                                                                         ImGui.SetNextItemWidth(135f * GlobalFontScale);
                                                                                                         ImGui.InputText("##newName", ref newCharacter.Name);
                                                                                                     });
                                                            row.TableNextColumn();
                                                            DrawCentered("##TraderNewCharacterDC", () =>
                                                                                                   {
                                                                                                       ImGui.SetNextItemWidth(100f * GlobalFontScale);
                                                                                                        // World.DataCenter retains the old Japanese grouping; Unknown0 has the current data center ID.
                                                                                                       if (ExcelSheetCombo<WorldDCGroupType>("##newdc", out var selectedDC, s => s.GetRowOrDefault(newCharacter.DataCenterId) is { } row
                                                                                                                                                                                         ? row.Name.ExtractText()
                                                                                                                                                                                          : string.Empty, x => x.Name.ExtractText(), x => x.RowId is > 0 and < 12 && Svc.Data.GetExcelSheet<World>().Any(y => y.Unknown0 == x.RowId && y.IsPublic && y.RowId != 3000 && y.RowId != 3001)))
                                                                                                       {
                                                                                                           newCharacter.DataCenterId = selectedDC.RowId;
                                                                                                           newCharacter.WorldId = Svc.Data
                                                                                                                                     .GetExcelSheet<World>()
                                                                                                                                     .First(x => x.Unknown0 == selectedDC.RowId && x.IsPublic && x.RowId != 3000 && x.RowId != 3001)
                                                                                                                                     .RowId;
                                                                                                       }
                                                                                                   });
                                                            row.TableNextColumn();
                                                            DrawCentered("##TraderNewCharacterWorld", () =>
                                                                                                      {
                                                                                                          ImGui.SetNextItemWidth(110f * GlobalFontScale);
                                                                                                          if (ExcelSheetCombo<World>("##world", out var selectedCharacterWorld, s => s.GetRowOrDefault(newCharacter.WorldId) is { } row
                                                                                                                                                                                             ? row.Name.ExtractText()
                                                                                                                                                                                             : string.Empty, x => x.Name.ExtractText(), x => x.IsPublic && x.Unknown0 == newCharacter.DataCenterId && x.RowId != 3000 && x.RowId != 3001))
                                                                                                              newCharacter.WorldId = selectedCharacterWorld.RowId;
                                                                                                      });
                                                            row.TableNextColumn();
                                                            DrawCentered("##TraderNewCharacterAdd", () =>
                                                                                                    {
                                                                                                        if (ImGuiComponents.IconButton("##LightAdd", FontAwesomeIcon.Plus))
                                                                                                        {
                                                                                                            Configuration.TestyTraderImportedCharacters.Add(newCharacter);
                                                                                                            newCharacter  = new TestyTraderCharacterData();
                                                                                                            ConfigChanged = true;
                                                                                                        }
                                                                                                    });
                                                        }
                                                       );
    }

    private void MoveManualCharacter(Guid id, int targetIndex)
    {
        var characters = Configuration.TestyTraderImportedCharacters;
        var sourceIndex = characters.FindIndex(x => x.Id == id);
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= characters.Count || sourceIndex == targetIndex) return;

        var character = characters[sourceIndex];
        characters.RemoveAt(sourceIndex);
        characters.Insert(targetIndex, character);
        ConfigChanged = true;
    }

    private void DrawCharacterTab()
    {
        var arAvailable = SubscriptionManager.IsInitialized(IPCNames.AutoRetainer);
        Widgets.Card(T("TabCharacters"), () =>
                                         {
                                             if (Configuration.TestyTraderARSupport)
                                             {
                                                 if (Widgets.Button(T("SelectAll")))
                                                 {
                                                     foreach (var keyValuePair in Configuration.EnableCharacterForTrade) Configuration.EnableCharacterForTrade[keyValuePair.Key] = true;
                                                     ConfigChanged = true;
                                                 }

                                                 ImGui.SameLine(0, 8 * GlobalFontScale);
                                                 if (Widgets.Button(T("DeselectAll")))
                                                 {
                                                     foreach (var keyValuePair in Configuration.EnableCharacterForTrade) Configuration.EnableCharacterForTrade[keyValuePair.Key] = false;
                                                     ConfigChanged = true;
                                                 }

                                                 ImGui.SameLine(0, 8 * GlobalFontScale);
                                                 if (Widgets.Button(T("SelectAllShown")))
                                                 {
                                                     foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForTrade[character.CID] = true;
                                                     ConfigChanged = true;
                                                 }

                                                 ImGui.SameLine(0, 8 * GlobalFontScale);
                                                 if (Widgets.Button(T("DeselectAllShown")))
                                                 {
                                                     foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForTrade[character.CID] = false;
                                                     ConfigChanged = true;
                                                 }

                                                 DrawARTable();
                                             }
                                             else
                                             {
                                                 if (Widgets.Button(T("ImportFromClipboard"), icon: FontAwesomeIcon.Clipboard))
                                                 {
                                                     ImportCharacters(ImGui.GetClipboardText(), Configuration.TestyTraderImportedCharacters);
                                                     ConfigChanged = true;
                                                 }

                                                 ImGui.SameLine();
                                                 ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8 * GlobalFontScale);
                                                 HelpMarker(() => ImGui.Text(T("ImportHelp")), sameLine: false);
                                                 DrawManualTable();
                                             }
                                         }, 0f, () =>
                                                {
                                                    using (Widgets.Disabled(!arAvailable))
                                                    {
                                                        Widgets.TextSoft(T("ARSupport"));
                                                    }

                                                    ImGui.SameLine(0, 10 * GlobalFontScale);
                                                    ConfigChanged |= Widgets.Switch("##arSupport", ref Configuration.TestyTraderARSupport, arAvailable);
                                                });
    }

    private void DrawItemTab() => Widgets.Columns(16f, (220f, DrawPlansCard), (0f, DrawItemsCard));

    private void DrawPlansCard()
    {
        Widgets.Card(T("TradePlans"), () =>
                                      {
                                          var rowH = ImGui.GetFrameHeight();
                                          foreach (var plan in Configuration.TradeLists)
                                          {
                                              using var id     = ImRaii.PushId(plan.Id.ToString());
                                              var       active = plan == ActivePlan;
                                              using (ImRaii.PushColor(ImGuiCol.Header, V2Palette.Soft(V2Palette.Accent, 0.14f)))
                                              using (ImRaii.PushColor(ImGuiCol.Text, active ? V2Palette.Accent : V2Palette.TextSoft))
                                              using (ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0.5f)))
                                              {
                                                  if (ImGui.Selectable($"{plan.Name}##pick", active, ImGuiSelectableFlags.None, new Vector2(Widgets.Avail - rowH - ImGui.GetStyle().ItemSpacing.X, rowH)))
                                                  {
                                                      Configuration.SelectedTradeListId = plan.Id;
                                                      ConfigChanged                     = true;
                                                  }
                                              }

                                              ImGui.SameLine();
                                              if (Widgets.IconButton("deletePlan", FontAwesomeIcon.Trash, null, ButtonKind.Ghost, rowH / GlobalFontScale, Configuration.TradeLists.Count > 1))
                                                  planToDelete = plan;
                                          }

                                          ImGui.Dummy(new Vector2(0, 4 * GlobalFontScale));
                                          Widgets.SectionLabel(T("RenamePlan"));
                                          ImGui.SetNextItemWidth(Widgets.Avail);
                                          ConfigChanged |= ImGui.InputText("##renamePlan", ref ActivePlan.Name, 100);

                                          Widgets.SectionLabel(T("NewPlan"));
                                          ImGui.SetNextItemWidth(Widgets.Avail - rowH - ImGui.GetStyle().ItemSpacing.X);
                                          ImGui.InputTextWithHint("##newPlanName", T("NewPlanNameHint"), ref newPlanName, 100);
                                          ImGui.SameLine();
                                          if (Widgets.IconButton("addPlan", FontAwesomeIcon.Plus, null, ButtonKind.Primary, rowH / GlobalFontScale))
                                          {
                                              var plan = new TradeList
                                                         {
                                                                 Name = string.IsNullOrWhiteSpace(newPlanName)
                                                                                ? $"Plan {Configuration.TradeLists.Count + 1}"
                                                                                : newPlanName
                                                         };
                                              Configuration.TradeLists.Add(plan);
                                              Configuration.SelectedTradeListId = plan.Id;
                                              newPlanName                       = string.Empty;
                                              ConfigChanged                     = true;
                                          }
                                      }, 0f, () => Widgets.TextMuted(Configuration.TradeLists.Count.ToString()));
    }

    private void DrawItemsCard()
    {
        Widgets.Card(T("TabItems"), () =>
                                    {
                                        DrawItemToolbar();
                                        DrawItemTable();
                                    }, 0f, () => Widgets.TextMuted(ActivePlan.Name));
    }

    private void DrawItemToolbar()
    {
        var rowY = ImGui.GetCursorScreenPos().Y;
        var rowH = ImGui.GetTextLineHeight() + 16 * GlobalFontScale;

        void Center(float height) => ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, rowY + (rowH - height) / 2f));

        Widgets.SearchField("##itemSearch", ref Search, T("SearchHint"), 180 * GlobalFontScale);
        ImGui.SameLine(0, 8 * GlobalFontScale);
        Center(ImGui.GetFrameHeight());
        ImGui.SetNextItemWidth(170 * GlobalFontScale);
        if (SearchableCombo("##category", out var category, selectedSearchCategory?.Name.ExtractText() ?? T("AllCategories"), searchCategories, x => x.Name.ToString(),
                            (p, s) => p.Name.ToString()
                                       .Contains(s, StringComparison.InvariantCultureIgnoreCase)))
            selectedSearchCategory = category;

        ImGui.SameLine(0, 4 * GlobalFontScale);
        Center(rowH);
        if (Widgets.IconButton("resetItemFilter", FontAwesomeIcon.Undo, T("Reset"), ButtonKind.Ghost, rowH / GlobalFontScale, selectedSearchCategory != null || Search.Length > 0))
        {
            selectedSearchCategory = null;
            Search                 = string.Empty;
        }

        ImGui.SameLine();
        Widgets.RightAligned("##itemToolbarRight", () =>
                                                   {
                                                       Center(ImGui.GetTextLineHeight());
                                                       Widgets.TextSoft(T("IncludeArmory"));
                                                       ImGui.SameLine(0, 10 * GlobalFontScale);
                                                       Center(20 * GlobalFontScale);
                                                       ConfigChanged |= Widgets.Switch("##includeArmory", ref Configuration.IncludeArmory);
                                                       ImGui.SameLine(0, 16 * GlobalFontScale);
                                                       Center(rowH);
                                                       if (Widgets.Button($"{T("AddItem")}##addItem", ButtonKind.Primary, FontAwesomeIcon.Plus))
                                                           ImGui.OpenPopup("##addItemPopup");
                                                       DrawAddItemPopup();
                                                   }, rowY);
    }

    private void DrawAddItemPopup()
    {
        using var popup = ImRaii.Popup("##addItemPopup");
        if (!popup) return;

        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        Widgets.SearchField("##addItemSearch", ref Search, T("SearchHint"), 420 * GlobalFontScale);

        var contained     = ActivePlan.Entries.Select(x => x.Id).ToHashSet();
        var filteredItems = expandedItems.Where(x => MatchesFilter(x.Item, x.DisplayName)).ToList();

        using var list = ImRaii.Child("##addItemList", new Vector2(420 * GlobalFontScale, 320 * GlobalFontScale));
        if (!list.Success) return;

        var clipper = new ImGuiListClipper();
        clipper.Begin(filteredItems.Count);

        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var entry = filteredItems[i];

                if (Svc.Texture.GetFromGameIcon(new GameIconLookup(entry.Item.Icon, hiRes: false))
                       .TryGetWrap(out var texture, out _))
                {
                    ImGui.Image(texture.Handle, new Vector2(ImGui.GetTextLineHeight()));
                    ImGui.SameLine();
                }

                if (ImGui.Selectable($"{entry.DisplayName}##{entry.RowId}", contained.Contains(entry.RowId), ImGuiSelectableFlags.DontClosePopups))
                {
                    if (!contained.Contains(entry.RowId))
                    {
                        ActivePlan.Entries.Add(new TradeEntry
                                               {
                                                       Id      = entry.RowId,
                                                       Amount  = 0,
                                                       Mode    = TradeMode.Give,
                                                       Enabled = true
                                               });
                    }

                    ConfigChanged = true;
                }

                GameItemTooltip.ShowOnHover(entry.RowId);
            }
        }

        clipper.End();
    }

    private bool MatchesFilter(Item item, string name) =>
            (selectedSearchCategory == null || item.ItemSearchCategory.RowId == selectedSearchCategory.Value.RowId) &&
            (string.IsNullOrEmpty(Search)   || name.Contains(Search, StringComparison.OrdinalIgnoreCase));

    private void DrawItemTable()
    {
        using var table = ImRaii.Table("##TraderItemTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp, new Vector2(Widgets.Avail, 0));
        if (!table.Success) return;

        ImGui.TableSetupColumn("##enable", ImGuiTableColumnFlags.WidthFixed, 28 * GlobalFontScale);
        ImGui.TableSetupColumn(T("ColName").ToUpperInvariant());
        ImGui.TableSetupColumn(T("ColAmount").ToUpperInvariant(), ImGuiTableColumnFlags.WidthFixed, 130 * GlobalFontScale);
        ImGui.TableSetupColumn(T("ColTradeType").ToUpperInvariant(), ImGuiTableColumnFlags.WidthFixed, 100 * GlobalFontScale);
        ImGui.TableSetupColumn("##remove", ImGuiTableColumnFlags.WidthFixed, 32 * GlobalFontScale);
        using (ImRaii.PushColor(ImGuiCol.Text, V2Palette.TextMuted))
        {
            ImGui.TableHeadersRow();
        }

        TradeEntry? itemToDelete = null;
        foreach (var entry in ActivePlan.Entries)
        {
            var item = Svc.Data.GetExcelSheet<Item>().GetRow(entry.Id % 1_000_000);
            var name = item.GetName() + (entry.Id > 1_000_000 ? " (HQ)" : string.Empty);
            if (!MatchesFilter(item, name)) continue;

            using var id = ImRaii.PushId(entry.Id.ToString());
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetStyle().FramePadding.Y);
            ConfigChanged |= Widgets.Checkbox("##enabled", ref entry.Enabled);

            ImGui.TableNextColumn();
            if (Svc.Texture.GetFromGameIcon(new GameIconLookup(item.Icon, hiRes: false))
                   .TryGetWrap(out var texture, out _))
            {
                ImGui.Image(texture.Handle, new Vector2(ImGui.GetFrameHeight()));
                ImGui.SameLine();
            }

            ImGui.AlignTextToFramePadding();
            Widgets.TextColored(entry.Enabled ? V2Palette.Text : V2Palette.TextDim, name);
            GameItemTooltip.ShowOnHover(entry.Id);

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ConfigChanged |= ImGui.InputUInt("##amount", ref entry.Amount);

            ImGui.TableNextColumn();
            DrawTradeModeChip(entry);

            ImGui.TableNextColumn();
            if (Widgets.IconButton("remove", FontAwesomeIcon.Trash, null, ButtonKind.Ghost, ImGui.GetFrameHeight() / GlobalFontScale)) itemToDelete = entry;
        }

        if (itemToDelete == null) return;
        ActivePlan.Entries.Remove(itemToDelete);
        ConfigChanged = true;
    }

    private void DrawTradeModeChip(TradeEntry entry)
    {
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetFrameHeight() - ImGui.GetTextLineHeight()) / 2f);
        var pos = ImGui.GetCursorScreenPos();
        Widgets.Chip(T($"Trade{entry.Mode}"), TradeModeColor(entry.Mode));
        ImGui.SetCursorScreenPos(pos);
        if (ImGui.InvisibleButton("##tradeModeChip", ImGui.GetItemRectSize())) ImGui.OpenPopup("##tradeMode");
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        using var popup = ImRaii.Popup("##tradeMode");
        if (!popup) return;

        foreach (var mode in Enum.GetValues<TradeMode>())
        {
            using var color = ImRaii.PushColor(ImGuiCol.Text, TradeModeColor(mode));
            if (!ImGui.Selectable(T($"Trade{mode}"), mode == entry.Mode)) continue;

            entry.Mode    = mode;
            ConfigChanged = true;
        }
    }

    private static Vector4 TradeModeColor(TradeMode mode) => mode switch
                                                             {
                                                                     TradeMode.Give     => V2Palette.Teal,
                                                                     TradeMode.Keep     => V2Palette.TextSoft,
                                                                     TradeMode.AskFor   => V2Palette.Blue,
                                                                     TradeMode.AskUntil => V2Palette.Purple,
                                                                     _                  => V2Palette.Warning
                                                             };

    public static void ImportCharacters(string input, List<TestyTraderCharacterData> list)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length < 2 || !input.StartsWith("[") || !input.EndsWith("]"))
            return;

        var content = input.Substring(1, input.Length - 2);
        var entries = content.Split(',');

        foreach (var entry in entries)
        {
            var parts = entry.Split('|');
            if (parts.Length != 2)
                continue;

            var name = parts[0]
                   .Trim();
            var world = parts[1]
                   .Trim();

            var worldRow = Svc.Data.GetExcelSheet<World>()
                              .FirstOrNull(x => string.Equals(x.Name.ExtractText(), world, StringComparison.OrdinalIgnoreCase));

            if (worldRow == null)
            {
                InternalTaskError($"World {world} for {name} is not correct");
                continue;
            }

            var newItem = new TestyTraderCharacterData
                          {
                                  DataCenterId = worldRow.Value.Unknown0,
                                  Enabled      = true,
                                  Name         = name,
                                  WorldId      = worldRow.Value.RowId
                          };

            if (!list.Contains(newItem))
                list.Add(newItem);
        }
    }


    public class TestyTraderCharacterData : IEquatable<TestyTraderCharacterData>
    {
        public Guid   Id           = Guid.NewGuid();
        public uint   DataCenterId = 7;
        public bool   Enabled      = true;
        public string Name         = "";
        public uint   WorldId      = 66;

        public bool Equals(TestyTraderCharacterData other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) && WorldId == other.WorldId;

        public override bool Equals(object obj) => obj is TestyTraderCharacterData other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name?.ToLowerInvariant(), WorldId);
    }
}
