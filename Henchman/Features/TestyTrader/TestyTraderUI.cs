using System.Linq;
using AutoRetainerAPI.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using Henchman.Abstractions;
using Henchman.Models;
using Henchman.Multiboxing;
using Henchman.TaskManager;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.TestyTrader;

[Feature]
public class TestyTraderUI : FeatureUI<TestyTrader, Configuration>
{
    private static readonly Item[] TradableItems = Svc.Data.GetExcelSheet<Item>()
                                                      .Where(x => x is { IsUntradable: false, RowId: > 0 and < 20 or > 100 } && !string.IsNullOrEmpty(x.Name.GetText()))
                                                      .OrderBy(x => x.Name.GetText())
                                                      .ToArray();

    private static readonly TestyTrader Feature = new();
    private static          bool        ConfigChanged;

    private static readonly Dictionary<string, World> Worlds =
            Svc.Data.GetExcelSheet<World>()
               .DistinctBy(x => x.Name.ExtractText())
               .ToDictionary(x => x.Name.ExtractText(), x => x);

    private readonly Table<OfflineCharacterData> ARTable;

    private readonly List<(Item Item, uint RowId, string DisplayName)> expandedItems = TradableItems
                                                                                      .SelectMany(x =>
                                                                                                  {
                                                                                                      var baseEntry = (Item: x, x.RowId, DisplayName: x.Name.GetText());
                                                                                                      if (x.CanBeHq)
                                                                                                      {
                                                                                                          var hqEntry = (Item: x, RowId: x.RowId + 1_000_000, DisplayName: x.Name.GetText() + " (HQ)");
                                                                                                          return [baseEntry, hqEntry];
                                                                                                      }

                                                                                                      return new[] { baseEntry };
                                                                                                  })
                                                                                      .ToList();

    private readonly List<ItemSearchCategory> searchCategories = Svc.Data.GetExcelSheet<ItemSearchCategory>()
                                                                    .Where(x => x.Category > 1)
                                                                    .OrderBy(x => x.Category)
                                                                    .ToList();

    internal TestyTraderCharacterData? CharacterToRemove;

    private TestyTraderCharacterData newCharacter = new();

    internal string              Search = string.Empty;
    private  ItemSearchCategory? selectedSearchCategory;

    public TestyTraderUI()
    {
        Configuration = LoadConfig<Configuration>() ?? new Configuration();

        ARTable = new Table<OfflineCharacterData>(
                                                  "##ARTraderTable",
                                                  new List<TableColumn<OfflineCharacterData>>
                                                  {
                                                          new("##Enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: (x, index) =>
                                                                                                                                     {
                                                                                                                                         if (!Configuration.EnableCharacterForTrade.TryAdd(x.CID, false))
                                                                                                                                         {
                                                                                                                                             var isEnabled = Configuration.EnableCharacterForTrade[x.CID];
                                                                                                                                             if (isEnabled) ImGui.PushStyleColor(ImGuiCol.Button, 0xFF097000);

                                                                                                                                             if (ImGuiEx.IconButton($"\uf021###{x.CID}"))
                                                                                                                                             {
                                                                                                                                                 Configuration.EnableCharacterForTrade[x.CID] = !isEnabled;
                                                                                                                                                 ConfigChanged                                = true;
                                                                                                                                             }

                                                                                                                                             if (isEnabled) ImGui.PopStyleColor();
                                                                                                                                         }
                                                                                                                                         else
                                                                                                                                             ConfigChanged = true;
                                                                                                                                     }),
                                                          new("Name", x => x.Name, 135, FilterType.String, ColumnAlignment.Center),
                                                          new("World", x => x.World, 90, FilterType.MultiSelect, ColumnAlignment.Center),
                                                          new("DataCenter", x => Worlds[x.World]
                                                                                .DataCenter.Value.Name.ExtractText(), 90, FilterType.MultiSelect, ColumnAlignment.Center),
                                                          new("Subs", x => x.OfflineSubmarineData.Count.ToString(), 35, Alignment: ColumnAlignment.Center),
                                                          new("AR active", x => x.WorkshopEnabled.ToString(), 75, Alignment: ColumnAlignment.Center),
                                                          new("Inv.", x => x.InventorySpace.ToString(), 75, Alignment: ColumnAlignment.Center)
                                                  },
                                                  () => Feature.GetCurrentARCharacterData(),
                                                  x => x.CID == Player.CID,
                                                  new Vector2(570, 0)
                                                 );
    }

    public sealed override required Configuration   Configuration { get; init; }
    public override                 string          Name          => "Testy Trader";
    public override                 Category        Category      => Category.Economy;
    public override                 FontAwesomeIcon Icon          => FontAwesomeIcon.Handshake;

    public override Action? Help => () =>
                                    {
                                        ImGui.Text("""
                                                   Boss Mode:
                                                        - Single character to which all Henchmen will travel to and trade with.
                                                        - Make sure, that your Henchmen can get to the boss. (e.g. won't work in housing areas)
                                                        
                                                   Henchman Mode:
                                                        - Selected characters will travel each to the Boss and give/ask for configured items.
                                                        - If set up in System -> Settings, your char will teleport back to your given Home.
                                                        
                                                   Trading Modes:
                                                        - Give:         Will give the specified amount to the Boss.
                                                        - Keep:         Will keep the specified amount and give the rest to the Boss.
                                                        - Ask For:      Will ask the Boss for the specified amount.
                                                        - Ask Until:    Will ask the Boss for items until the specified amount is reached.
                                                        - PAR Level:    Will try to stay on the specified amount, regardless if it has to ask for or give away.
                                                   """);

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
        ConfigChanged = false;
        DrawCentered("###TraderStart", () => Layout.DrawButton(() =>
                                                               {
                                                                   if (StartButton() && !IsTaskEnqueued(Name))
                                                                   {
                                                                       EnqueueTask(Configuration.TradeSession == SessionType.Boss
                                                                                           ? new TaskRecord(Feature.Server, "Testy Trader - Boss Mode")
                                                                                           : new TaskRecord(Feature.Client, "Testy Trader - Henchman Mode"));
                                                                   }
                                                               }));
        DrawCentered("###TradeSessionType", () =>
                                            {
                                                ImGui.SetNextItemWidth(150f);
                                                ConfigChanged |= ImGuiEx.EnumCombo("##tradeSession", ref Configuration.TradeSession);
                                            });

        if (Configuration.TradeSession == SessionType.Henchman)
        {
            using var tabs = ImRaii.TabBar("Tabs");
            if (tabs)
            {
                using (var tab = ImRaii.TabItem("Characters"))
                {
                    if (tab)
                        DrawCharacterTab();
                }

                using (var tab = ImRaii.TabItem("Items"))
                {
                    if (tab)
                        DrawItemTab();
                }
            }
        }
        else
        {
            //TODO: Move to release after tests
            DrawCentered("##UseARItemSell", () => ConfigChanged |= ImGui.Checkbox("Use AR ItemSell", ref Configuration.UseARItemSell));
            DrawCentered("##BossToHenchmanWorld", () =>
                                                  {
                                                      ConfigChanged |= ImGui.Checkbox("Transfer Boss to Henchman World", ref Configuration.MoveBossToHenchman);
                                                      ImGui.SameLine();
                                                      HelpMarker(() => ImGui.Text("""
                                                                                  If your next henchman is on another world, your boss will transfer to it and move back to exact same position where he started.
                                                                                  It won't check if you can travel to another datacenter!

                                                                                  Example:  
                                                                                  Started on Shiva in Western La Noscea at the Shop in Aleport.
                                                                                  Second Henchman on Alpha.
                                                                                  Your boss will travel and then move to the exact same position on Alpha.
                                                                                  """));
                                                  });
        }

        if (CharacterToRemove != null)
            Configuration.TestyTraderImportedCharacters.Remove(CharacterToRemove);
        if (ConfigChanged) SaveConfig(Configuration);
    }

    private void DrawARTable()
    {
        ARTable.Draw();
    }

    private void DrawManualTable()
    {
        var characterColumns = new List<TableColumn<TestyTraderCharacterData>>
                               {
                                       new("##Enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: (x, index) =>
                                                                                                                  {
                                                                                                                      var isEnabled = x.Enabled;
                                                                                                                      if (isEnabled) ImGui.PushStyleColor(ImGuiCol.Button, 0xFF097000);

                                                                                                                      if (ImGuiEx.IconButton($"\uf021###{x.Name + x.WorldId}"))
                                                                                                                      {
                                                                                                                          x.Enabled     = !isEnabled;
                                                                                                                          ConfigChanged = true;
                                                                                                                      }

                                                                                                                      if (isEnabled) ImGui.PopStyleColor();
                                                                                                                  }),
                                       new("Name", x => x.Name, 135, FilterType.String, ColumnAlignment.Center),
                                       new("Data Center", x => Svc.Data.GetExcelSheet<WorldDCGroupType>()
                                                                  .GetRow(x.DataCenterId)
                                                                  .Name.ExtractText(), 100, FilterType.MultiSelect, ColumnAlignment.Center),
                                       new("World", x => Svc.Data.GetExcelSheet<World>()
                                                            .GetRow(x.WorldId)
                                                            .Name.ExtractText(), 100, FilterType.MultiSelect, ColumnAlignment.Center),
                                       new("##Remove", Width: 75, Alignment: ColumnAlignment.Center, DrawCustom: (x, index) =>
                                                                                                                 {
                                                                                                                     if (ImGuiComponents.IconButton($"##Remove{x.Name + x.WorldId}", FontAwesomeIcon.Trash)) CharacterToRemove = x;
                                                                                                                 })
                               };

        var table = new Table<TestyTraderCharacterData>(
                                                        "##ManualTraderTable",
                                                        characterColumns,
                                                        () => Configuration.TestyTraderImportedCharacters,
                                                        h => h.Name == Player.Name && h.WorldId == Player.HomeWorld.RowId,
                                                        new Vector2(450, 0),
                                                        () =>
                                                        {
                                                            ImGui.TableNextRow();
                                                            using var row = new ColumnScope(characterColumns.Count);
                                                            row.TableNextColumn();
                                                            row.TableNextColumn();
                                                            DrawCentered("##TraderNewCharacterName", () =>
                                                                                                     {
                                                                                                         ImGui.SetNextItemWidth(135f);
                                                                                                         ImGui.InputText("##newName", ref newCharacter.Name);
                                                                                                     });
                                                            row.TableNextColumn();
                                                            DrawCentered("##TraderNewCharacterDC", () =>
                                                                                                   {
                                                                                                       ImGui.SetNextItemWidth(100f);
                                                                                                       if (ImGuiEx.ExcelSheetCombo<WorldDCGroupType>("##newdc", out var selectedDC, s => s.GetRowOrDefault(newCharacter.DataCenterId) is { } row
                                                                                                                                                                                                 ? row.Name.ExtractText()
                                                                                                                                                                                                 : string.Empty, x => x.Name.ExtractText(), x => x.RowId is > 0 and < 12))
                                                                                                       {
                                                                                                           newCharacter.DataCenterId = selectedDC.RowId;
                                                                                                           newCharacter.WorldId = Svc.Data
                                                                                                                                     .GetExcelSheet<World>()
                                                                                                                                     .First(x => x.DataCenter.Value.RowId == selectedDC.RowId && x.IsPublic)
                                                                                                                                     .RowId;
                                                                                                       }
                                                                                                   });
                                                            row.TableNextColumn();
                                                            DrawCentered("##TraderNewCharacterWorld", () =>
                                                                                                      {
                                                                                                          ImGui.SetNextItemWidth(110f);
                                                                                                          if (ImGuiEx.ExcelSheetCombo<World>("##world", out var selectedCharacterWorld, s => s.GetRowOrDefault(newCharacter.WorldId) is { } row
                                                                                                                                                                                                     ? row.Name.ExtractText()
                                                                                                                                                                                                     : string.Empty, x => x.Name.ExtractText(), x => x.IsPublic && x.RowId != 3000 && x.RowId != 3001))
                                                                                                              newCharacter.WorldId = selectedCharacterWorld.RowId;
                                                                                                      });
                                                            row.TableNextColumn();
                                                            DrawCentered("##TraderNewCharacterAdd", () =>
                                                                                                    {
                                                                                                        if (ImGuiComponents.IconButton("##LightAdd", FontAwesomeIcon.Plus))
                                                                                                        {
                                                                                                            Configuration.TestyTraderImportedCharacters.Add(newCharacter);
                                                                                                            newCharacter = new TestyTraderCharacterData();
                                                                                                        }
                                                                                                    });
                                                        }
                                                       );

        table.Draw();
    }

    private void DrawCharacterTab()
    {
        if (SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            DrawCentered("##TraderARSupport", () => ConfigChanged |= ImGui.Checkbox("AR Support", ref Configuration.TestyTraderARSupport));
            if (Configuration.TestyTraderARSupport)
            {
                DrawCentered("##TradeCharSelector", () =>
                                                    {
                                                        if (ImGui.Button("Select All"))
                                                        {
                                                            foreach (var keyValuePair in Configuration.EnableCharacterForTrade) Configuration.EnableCharacterForTrade[keyValuePair.Key] = true;
                                                            ConfigChanged = true;
                                                        }

                                                        ImGui.SameLine();
                                                        if (ImGui.Button("Deselect All"))
                                                        {
                                                            foreach (var keyValuePair in Configuration.EnableCharacterForTrade) Configuration.EnableCharacterForTrade[keyValuePair.Key] = false;
                                                            ConfigChanged = true;
                                                        }
                                                    });
                DrawCentered("##TradeFilteredCharSelector", () =>
                                                            {
                                                                if (ImGui.Button("Select All Shown"))
                                                                {
                                                                    foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForTrade[character.CID] = true;
                                                                    ConfigChanged = true;
                                                                }

                                                                ImGui.SameLine();
                                                                if (ImGui.Button("Deselect All Shown"))
                                                                {
                                                                    foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForTrade[character.CID] = false;
                                                                    ConfigChanged = true;
                                                                }
                                                            });
                DrawCentered("##CenteredARTraderTable", () => DrawARTable());
            }
        }
        else
            Configuration.TestyTraderARSupport = false;

        if (!Configuration.TestyTraderARSupport)
        {
            DrawCentered("##ImportTraders", () =>
                                            {
                                                if (ImGui.Button("Import from Clipboard"))
                                                {
                                                    var charString = ImGui.GetClipboardText();
                                                    ImportCharacters(charString, Configuration.TestyTraderImportedCharacters);
                                                }

                                                HelpMarker(() =>
                                                           {
                                                               ImGui.Text("""
                                                                          Import multiple characters through the following format:
                                                                          [Name1|World1,Name2|World2,Name3|World3]
                                                                          """);
                                                           }, sameLine: true);
                                            });

            DrawCentered("##ManualTraderTable", () => DrawManualTable());
        }

        if (ConfigChanged) SaveConfig(Configuration);
    }

    private void DrawItemTab()
    {
        DrawCentered("##TraderItemSelector", () =>
                                             {
                                                 ImGui.SetNextItemWidth(500 * GlobalFontScale);
                                                 if (ImGui.BeginCombo("##addItem", "Add Item", ImGuiComboFlags.HeightLarge))
                                                 {
                                                     ImGuiEx.InputWithRightButtonsArea(() => { ImGui.InputTextWithHint("##itemSearch", "Search...", ref Search, 100); },
                                                                                       () =>
                                                                                       {
                                                                                           ImGui.SetNextItemWidth(200f);
                                                                                           if (ImGuiEx.SearchableCombo("##category", out var category,
                                                                                                                       selectedSearchCategory != null
                                                                                                                               ? selectedSearchCategory.Value.Name.GetText()
                                                                                                                               : "All Categories",
                                                                                                                       searchCategories,
                                                                                                                       x => x.Name.ToString(),
                                                                                                                       (p, s) => p.Name.ToString()
                                                                                                                                  .Contains(s, StringComparison.InvariantCultureIgnoreCase)))
                                                                                               selectedSearchCategory = category;

                                                                                           ImGui.SameLine();
                                                                                           if (ImGui.Button("Reset"))
                                                                                           {
                                                                                               selectedSearchCategory = null;
                                                                                               Search                 = string.Empty;
                                                                                           }
                                                                                       });

                                                     var filteredItems = expandedItems
                                                                        .Where(entry =>
                                                                                       (selectedSearchCategory == null || entry.Item.ItemSearchCategory.RowId == selectedSearchCategory.Value.RowId) &&
                                                                                       (string.IsNullOrEmpty(Search)   || entry.DisplayName.Contains(Search, StringComparison.OrdinalIgnoreCase)))
                                                                        .ToList();

                                                     var clipper = new ImGuiListClipper();
                                                     clipper.Begin(filteredItems.Count);

                                                     while (clipper.Step())
                                                     {
                                                         for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                                                         {
                                                             var entry = filteredItems[i];
                                                             var cont = Configuration.TradeEntries.Select(s => s.Id)
                                                                                     .ToArray();

                                                             if (ThreadLoadImageHandler.TryGetIconTextureWrap(entry.Item.Icon, false, out var texture))
                                                             {
                                                                 ImGui.Image(texture.Handle, new Vector2(ImGui.GetTextLineHeight()));
                                                                 ImGui.SameLine();
                                                             }

                                                             if (ImGui.Selectable($"{entry.DisplayName}##{entry.RowId}", cont.Contains(entry.RowId), ImGuiSelectableFlags.DontClosePopups))
                                                             {
                                                                 if (!cont.Contains(entry.RowId))
                                                                 {
                                                                     Configuration.TradeEntries.Add(new TradeEntry
                                                                                                    {
                                                                                                            Id      = entry.RowId,
                                                                                                            Amount  = 0,
                                                                                                            Mode    = TradeMode.Give,
                                                                                                            Enabled = true
                                                                                                    });
                                                                 }

                                                                 ConfigChanged = true;
                                                             }
                                                         }
                                                     }

                                                     clipper.End();

                                                     ImGui.EndCombo();
                                                 }
                                             });

        DrawCentered("##TraderItemTable", () => DrawItemTable());
    }

    private void DrawItemTable()
    {
        TradeEntry? itemToDelete = null;
        var table = new Table<TradeEntry>(
                                          "##TraderItemTable",
                                          new List<TableColumn<TradeEntry>>
                                          {
                                                  new("##Enable", Width: 25, DrawCustom: (x, index) => { ConfigChanged |= ImGui.Checkbox($"##enable{x.Id}", ref x.Enabled); }),
                                                  new("Name", Width: 300, DrawCustom: (x, index) =>
                                                                                      {
                                                                                          if (ThreadLoadImageHandler.TryGetIconTextureWrap(Svc.Data.GetExcelSheet<Item>()
                                                                                                                                              .GetRow(x.Id % 1_000_000)
                                                                                                                                              .Icon, false, out var texture))
                                                                                          {
                                                                                              ImGui.Image(texture.Handle, new Vector2(ImGui.GetTextLineHeight()));
                                                                                              ImGui.SameLine();
                                                                                          }

                                                                                          ImGuiEx.Text(
                                                                                                       Svc.Data.GetExcelSheet<Item>()
                                                                                                          .GetRow(x.Id % 1_000_000)
                                                                                                          .GetName() +
                                                                                                       (x.Id > 1_000_000
                                                                                                                ? " (HQ)"
                                                                                                                : "")
                                                                                                      );
                                                                                      }),
                                                  new("Amount", Width: 120, DrawCustom: (x, index) =>
                                                                                        {
                                                                                            ImGui.SetNextItemWidth(120);
                                                                                            ConfigChanged |= ImGui.InputUInt($"##{x.Id}Amount", ref x.Amount);
                                                                                        }),
                                                  new("TradeType", Width: 120, DrawCustom: (x, index) =>
                                                                                           {
                                                                                               ImGui.SetNextItemWidth(120);
                                                                                               ConfigChanged |= ImGuiEx.EnumCombo($"##tradeType{x.Id}", ref x.Mode);
                                                                                           }),
                                                  new("##Controls", Width: 45, Alignment: ColumnAlignment.Center, DrawCustom: (x, index) =>
                                                                                                                              {
                                                                                                                                  if (ImGuiEx.IconButton(FontAwesomeIcon.Trash, x.Id.ToString())) itemToDelete = x;
                                                                                                                              })
                                          },
                                          () => Configuration.TradeEntries,
                                          size: new Vector2(635, 0)
                                         );

        table.Draw();

        if (itemToDelete != null)
        {
            Configuration.TradeEntries.Remove(itemToDelete);
            ConfigChanged = true;
        }
    }

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
                                  DataCenterId = worldRow.Value.DataCenter.Value.RowId,
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
        public uint   DataCenterId = 7;
        public bool   Enabled      = true;
        public string Name         = "";
        public uint   WorldId      = 66;

        public bool Equals(TestyTraderCharacterData other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) && WorldId == other.WorldId;

        public override bool Equals(object obj) => obj is TestyTraderCharacterData other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name?.ToLowerInvariant(), WorldId);
    }
}
