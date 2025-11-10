using System.Linq;
using AutoRetainerAPI.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using Henchman.Helpers;
using Henchman.Models;
using Henchman.TaskManager;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.TestyTrader;

[Feature]
public class TestyTraderUI : FeatureUI
{
    private readonly TestyTrader feature = new();

    private readonly List<ItemSearchCategory> searchCategories = Svc.Data.GetExcelSheet<ItemSearchCategory>()
                                                                    .Where(x => x.Category > 1)
                                                                    .OrderBy(x => x.Category)
                                                                    .ToList();

    private readonly Item[] tradableItems = Svc.Data.GetExcelSheet<Item>()
                                               .Where(x => x is { IsUntradable: false, RowId: 1 or > 100 } && !string.IsNullOrEmpty(x.Name.GetText()))
                                               .OrderBy(x => x.Name.GetText())
                                               .ToArray();

    internal TestyTraderCharacterData? characterToRemove;

    private bool configChanged;

    private TestyTraderCharacterData newCharacter = new();

    internal        string              Search = string.Empty;
    private         ItemSearchCategory? selectedSearchCategory;
    public override string              Name     => "Testy Trader";
    public override string              Category => Henchman.Category.Economy;
    public override FontAwesomeIcon     Icon     => FontAwesomeIcon.Handshake;

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
                                                   """);

                                        ImGuiHelper.DrawRequirements(Requirements);
                                    };

    public override bool LoginNeeded => false;

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
    ];

    public override void Draw()
    {
        configChanged = false;
        ImGuiHelper.DrawCentered("###TraderStart", () => Layout.DrawButton(() =>
                                                                           {
                                                                               if (ImGui.Button("Start", new Vector2(70, 30)) && !IsTaskEnqueued(Name))
                                                                               {
                                                                                   EnqueueTask(C.TradeSession == TradeSession.Boss
                                                                                                       ? new TaskRecord(feature.Server, "Testy Trader - Boss Mode")
                                                                                                       : new TaskRecord(feature.Client, "Testy Trader - Henchman Mode"));
                                                                               }
                                                                           }));
        ImGuiHelper.DrawCentered("###TradeSessionType", () =>
                                                        {
                                                            ImGui.SetNextItemWidth(150f);
                                                            configChanged |= ImGuiEx.EnumCombo("##tradeSession", ref C.TradeSession);
                                                        });

        if (C.TradeSession == TradeSession.Henchman)
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

        if (characterToRemove != null)
            C.TestyTraderImportedCharacters.Remove(characterToRemove);
        if (configChanged) EzConfig.Save();
    }

    private void DrawARTable()
    {
        var table = new Table<OfflineCharacterData>(
                                                    "##ARTraderTable",
                                                    new List<TableColumn<OfflineCharacterData>>
                                                    {
                                                            new("##Enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: x =>
                                                                                                                                       {
                                                                                                                                           if (!C.EnableCharacterForTrade.TryAdd(x.CID, false))
                                                                                                                                           {
                                                                                                                                               var isEnabled = C.EnableCharacterForTrade[x.CID];
                                                                                                                                               if (isEnabled) ImGui.PushStyleColor(ImGuiCol.Button, 0xFF097000);

                                                                                                                                               if (ImGuiEx.IconButton($"\uf021###{x.CID}"))
                                                                                                                                               {
                                                                                                                                                   C.EnableCharacterForTrade[x.CID] = !isEnabled;
                                                                                                                                                   configChanged                    = true;
                                                                                                                                               }

                                                                                                                                               if (isEnabled) ImGui.PopStyleColor();
                                                                                                                                           }
                                                                                                                                           else
                                                                                                                                               configChanged = true;
                                                                                                                                       }),
                                                            new("Name", x => x.Name, 135, ColumnAlignment.Center),
                                                            new("World", x => x.World, 90, ColumnAlignment.Center),
                                                            new("DataCenter", x => Svc.Data.GetExcelSheet<World>()
                                                                                      .FirstOrDefault(y => y.Name == x.World)
                                                                                      .DataCenter.Value.Name.ExtractText(), 90, ColumnAlignment.Center),
                                                            new("Subs", x => x.OfflineSubmarineData.Count.ToString(), 35, ColumnAlignment.Center),
                                                            new("AR active", x => x.WorkshopEnabled.ToString(), 75, ColumnAlignment.Center),
                                                            new("Inv.", x => x.InventorySpace.ToString(), 75, ColumnAlignment.Center)
                                                    },
                                                    () => feature.GetCurrentARCharacterData(),
                                                    x => x.CID == Player.CID,
                                                    new Vector2(570, 0)
                                                   );

        table.Draw();
    }

    private void DrawManualTable()
    {
        var characterColumns = new List<TableColumn<TestyTraderCharacterData>>
                               {
                                       new("##Enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: x =>
                                                                                                                  {
                                                                                                                      var isEnabled = x.Enabled;
                                                                                                                      if (isEnabled) ImGui.PushStyleColor(ImGuiCol.Button, 0xFF097000);

                                                                                                                      if (ImGuiEx.IconButton($"\uf021###{x.Name + x.WorldId}"))
                                                                                                                      {
                                                                                                                          x.Enabled     = !isEnabled;
                                                                                                                          configChanged = true;
                                                                                                                      }

                                                                                                                      if (isEnabled) ImGui.PopStyleColor();
                                                                                                                  }),
                                       new("Name", x => x.Name, 135, ColumnAlignment.Center),
                                       new("Data Center", x => Svc.Data.GetExcelSheet<WorldDCGroupType>()
                                                                 .GetRow(x.DataCenterId)
                                                                 .Name.ExtractText(), 100, ColumnAlignment.Center),
                                       new("World", x => Svc.Data.GetExcelSheet<World>()
                                                            .GetRow(x.WorldId)
                                                            .Name.ExtractText(), 100, ColumnAlignment.Center),
                                       new("##Remove", Width: 75, Alignment: ColumnAlignment.Center, DrawCustom: x =>
                                                                                                                 {
                                                                                                                     if (ImGuiComponents.IconButton($"##Remove{x.Name + x.WorldId}", FontAwesomeIcon.Trash)) characterToRemove = x;
                                                                                                                 })
                               };
        var table = new Table<TestyTraderCharacterData>(
                                                        "##ARTraderTable",
                                                        characterColumns,
                                                        () => C.TestyTraderImportedCharacters,
                                                        h => h.Name == Player.Name && h.WorldId == Player.HomeWorldId,
                                                        new Vector2(450, 0),
                                                        () =>
                                                        {
                                                            ImGui.TableNextRow();
                                                            using var row = new ImGuiHelper.ColumnScope(characterColumns.Count);
                                                            row.TableNextColumn();
                                                            row.TableNextColumn();
                                                            ImGuiHelper.DrawCentered("##TraderNewCharacterName", () => { ImGui.SetNextItemWidth(135f); ImGui.InputText("##newName", ref newCharacter.Name); });
                                                            row.TableNextColumn();
                                                            ImGuiHelper.DrawCentered("##TraderNewCharacterDC", () =>
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
                                                            ImGuiHelper.DrawCentered("##TraderNewCharacterWorld", () =>
                                                                                                                  {
                                                                                                                      ImGui.SetNextItemWidth(110f);
                                                                                                                      if (ImGuiEx.ExcelSheetCombo<World>("##world", out var selectedCharacterWorld, s => s.GetRowOrDefault(newCharacter.WorldId) is { } row
                                                                                                                                                                                                                 ? row.Name.ExtractText()
                                                                                                                                                                                                                 : string.Empty, x => x.Name.ExtractText(), x => x.IsPublic && x.RowId != 3000 && x.RowId != 3001))
                                                                                                                          newCharacter.WorldId = selectedCharacterWorld.RowId;
                                                                                                                  });
                                                            row.TableNextColumn();
                                                            ImGuiHelper.DrawCentered("##TraderNewCharacterAdd", () =>
                                                                                                                {
                                                                                                                    if (ImGuiComponents.IconButton("##LightAdd", FontAwesomeIcon.Plus))
                                                                                                                    {
                                                                                                                        C.TestyTraderImportedCharacters.Add(newCharacter);
                                                                                                                        newCharacter        = new TestyTraderCharacterData();
                                                                                                                    }
                                                                                                                });
                                                        }
                                                       );

        table.Draw();
    }

    private void DrawCharacterTab()
    {
        ImGuiHelper.DrawCentered("##TraderARSupport", () => configChanged |= ImGui.Checkbox("AR Support", ref C.TestyTraderARSupport));
        if (C.TestyTraderARSupport)
        {
            ImGuiHelper.DrawCentered("##ARankSelector", () =>
                                                        {
                                                            if (ImGui.Button("Select All"))
                                                            {
                                                                foreach (var keyValuePair in C.EnableCharacterForTrade) C.EnableCharacterForTrade[keyValuePair.Key] = true;
                                                                configChanged = true;
                                                            }

                                                            ImGui.SameLine();
                                                            if (ImGui.Button("Deselect All"))
                                                            {
                                                                foreach (var keyValuePair in C.EnableCharacterForTrade) C.EnableCharacterForTrade[keyValuePair.Key] = false;
                                                                configChanged = true;
                                                            }
                                                        });
            ImGuiHelper.DrawCentered("##CenteredARTraderTable", () => DrawARTable());
        }
        else
        {
            ImGuiHelper.DrawCentered("##ImportTraders", () =>
                                                        {
                                                            if (ImGui.Button("Import from Clipboard"))
                                                            {
                                                                var charString = ImGui.GetClipboardText();
                                                                ImportCharacters(charString, C.TestyTraderImportedCharacters);
                                                            }

                                                            ImGuiHelper.HelpMarker(() =>
                                                                                   {
                                                                                       ImGui.Text("""
                                                                                                  Import multiple characters through the following format:
                                                                                                  [Name1|World1,Name2|World2,Name3|World3]
                                                                                                  """);
                                                                                   }, sameLine: true);
                                                        });

            ImGuiHelper.DrawCentered("##CenteredManualTraderTable", () => DrawManualTable());
        }

        if (configChanged) EzConfig.Save();
    }

    private void DrawItemTab()
    {
        ImGuiHelper.DrawCentered("##CenteredTraderItemSelector", () =>
                                                                 {
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

                                                                         var filteredItems = tradableItems.Where(item =>
                                                                                                                         (selectedSearchCategory == null || item.ItemSearchCategory.RowId == selectedSearchCategory.Value.RowId) &&
                                                                                                                         (string.IsNullOrEmpty(Search) ||
                                                                                                                          item.Name.ToString()
                                                                                                                              .Contains(Search, StringComparison.OrdinalIgnoreCase))
                                                                                                                )
                                                                                                          .ToList();

                                                                         var clipper = new ImGuiListClipper();
                                                                         clipper.Begin(filteredItems.Count);

                                                                         while (clipper.Step())
                                                                         {
                                                                             for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                                                                             {
                                                                                 var item = filteredItems[i];
                                                                                 var cont = C.TradeEntries.Select(s => s.Id)
                                                                                             .ToArray();
                                                                                 if (ThreadLoadImageHandler.TryGetIconTextureWrap(item.Icon, false, out var texture))
                                                                                 {
                                                                                     ImGui.Image(texture.Handle, new Vector2(ImGui.GetTextLineHeight()));
                                                                                     ImGui.SameLine();
                                                                                 }

                                                                                 if (ImGui.Selectable($"{item.GetName()}##{item.RowId}", cont.Contains(item.RowId), ImGuiSelectableFlags.DontClosePopups))
                                                                                 {
                                                                                     if (!cont.Contains(item.RowId))
                                                                                     {
                                                                                         C.TradeEntries.Add(new TradeEntry
                                                                                                            {
                                                                                                                    Id     = item.RowId,
                                                                                                                    Amount = 0,
                                                                                                                    Mode   = TradeMode.Give
                                                                                                            });
                                                                                     }

                                                                                     configChanged = true;
                                                                                 }
                                                                             }
                                                                         }

                                                                         clipper.End();
                                                                         ImGui.EndCombo();
                                                                     }
                                                                 });

        ImGuiHelper.DrawCentered("##CenteredTraderItemTable", () => DrawItemTable());
    }

    private void DrawItemTable()
    {
        TradeEntry? itemToDelete = null;
        var table = new Table<TradeEntry>(
                                          "##TraderItemTable",
                                          new List<TableColumn<TradeEntry>>
                                          {
                                                  new("##Enable", Width: 25, DrawCustom: x => { configChanged |= ImGui.Checkbox($"##enable{x.Id}", ref x.Enabled); }),
                                                  new("Name", Width: 300, DrawCustom: x =>
                                                                                      {
                                                                                          if (ThreadLoadImageHandler.TryGetIconTextureWrap(Svc.Data.GetExcelSheet<Item>()
                                                                                                                                              .GetRow(x.Id)
                                                                                                                                              .Icon, false, out var texture))
                                                                                          {
                                                                                              ImGui.Image(texture.Handle, new Vector2(ImGui.GetTextLineHeight()));
                                                                                              ImGui.SameLine();
                                                                                          }

                                                                                          ImGuiEx.Text(Svc.Data.GetExcelSheet<Item>()
                                                                                                          .GetRow(x.Id)
                                                                                                          .GetName());
                                                                                      }),
                                                  new("Amount", Width: 120, DrawCustom: x =>
                                                                                        {
                                                                                            ImGui.SetNextItemWidth(120);
                                                                                            configChanged |= ImGui.InputUInt($"##{x.Id}Amount", ref x.Amount);
                                                                                        }),
                                                  new("TradeType", Width: 120, DrawCustom: x =>
                                                                                           {
                                                                                               ImGui.SetNextItemWidth(120);
                                                                                               configChanged |= ImGuiEx.EnumCombo($"##tradeType{x.Id}", ref x.Mode);
                                                                                           }),
                                                  new("##Controls", Width: 35, Alignment: ColumnAlignment.Center, DrawCustom: x =>
                                                                                                                              {
                                                                                                                                  if (ImGuiEx.IconButton(FontAwesomeIcon.Trash, x.Id.ToString())) itemToDelete = x;
                                                                                                                              })
                                          },
                                          () => C.TradeEntries,
                                          size: new Vector2(625, 0)
                                         );

        table.Draw();

        if (itemToDelete != null)
        {
            C.TradeEntries.Remove(itemToDelete);
            configChanged = true;
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
                InternalError($"World {world} for {name} is not correct");
                continue;
            }

            var newItem = new TestyTraderCharacterData()
                          {
                                  DataCenterId =  worldRow.Value.DataCenter.Value.RowId,
                                  Enabled = true,
                                  Name = name,
                                  WorldId = worldRow.Value.RowId
                          };

            if (!list.Contains(newItem))
                list.Add(newItem);
        }
    }


    public class TestyTraderCharacterData() : IEquatable<TestyTraderCharacterData>
    {
        public bool   Enabled      = true;
        public string Name         = "";
        public uint   DataCenterId = 7;
        public uint   WorldId      = 66;

        public bool Equals(TestyTraderCharacterData other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) && WorldId == other.WorldId;

        public override bool Equals(object obj) => obj is TestyTraderCharacterData other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name?.ToLowerInvariant(), WorldId);
    }
}
