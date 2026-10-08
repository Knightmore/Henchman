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
                                                                                                                                             if (isEnabled) ImGui.PushStyleColor(ImGuiCol.Button, 0xFF097000);

                                                                                                                                             if (IconButton($"\uf021###{x.CID}"))
                                                                                                                                             {
                                                                                                                                                 Configuration.EnableCharacterForOCFishing[x.CID] = !isEnabled;
                                                                                                                                                 ConfigChanged                                    = true;
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
                                                          new("Lvl", x => x.ClassJobLevelArray[17]
                                                                           .ToString(), 35, Alignment: ColumnAlignment.Center),
                                                          new("Inv.", x => x.InventorySpace.ToString(), 75, Alignment: ColumnAlignment.Center)
                                                  },
                                                  () => Feature.GetCurrentARCharacterData(),
                                                  x => x.CID == Player.CID,
                                                  new Vector2(500, 0)
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

        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs)
        {
            using (var tab = ImRaii.TabItem(T("TabMain")))
            {
                if (tab)
                    DrawMain();
            }

            using (var tab = ImRaii.TabItem(T("TabSettings")))
            {
                if (tab)
                    DrawSettings();
            }
        }
        if (ConfigChanged) SaveConfig(Configuration);
    }

    private void DrawMain()
    {
        var utcNow = DateTime.UtcNow;
        var hour   = utcNow.Hour;
        var minute = utcNow.Minute;
        var second = utcNow.Second;

        Layout.DrawInfoBox(() =>
                           {
                               if (StartButton() && !IsTaskRunning(Name)) Start();
                           }, () =>
                              {
                                  if (Feature.IsRegistrationOpen)
                                  {
                                      var remaining = new TimeSpan(0, 14 - minute, 59 - second);
                                      ImGui.Text(string.Format(T("RegistrationOpenFmt"), remaining.Minutes, remaining.Seconds));
                                  }
                                  else
                                  {
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
                                      ImGui.Text(string.Format(T("NextVoyageFmt"), waitTime.Hours, waitTime.Minutes, waitTime.Seconds));
                                  }
                              });

        using (ImRaii.Disabled(!SubscriptionManager.IsInitialized(IPCNames.AutoRetainer)))
        {
            DrawCentered("##boatArCharacters", () =>
                                               {
                                                   ImGui.Text(T("UseWithARMultimode"));
                                                   ImGui.SameLine();
                                                   ConfigChanged |= ImGui.Checkbox("##HandleAR", ref Configuration.OCFishingHandleAR);
                                               });

            if (Configuration.OCFishingHandleAR)
                DrawCentered("##boatArStopAt100", () =>
                                              {
                                                  ImGui.Text(T("StopAt"));
                                                  //ImGui.SameLine(200 * GlobalFontScale);
                                                  ImGui.SameLine();
                                                  ImGui.SetNextItemWidth(40 * GlobalFontScale);
                                                  ImGui.InputInt("##fisherMaxLevel", ref Configuration.MaxLevel);
                                                  ImGui.SameLine();
                                                  ConfigChanged |= ImGui.Checkbox("##stopAt", ref Configuration.OCFishingStopLevel);
                                              });
        }

        if (Configuration.OCFishingHandleAR && SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            DrawCentered("##BoatCharSelector", () =>
                                               {
                                                   if (ImGui.Button(T("SelectAll")))
                                                   {
                                                       foreach (var keyValuePair in Configuration.EnableCharacterForOCFishing) Configuration.EnableCharacterForOCFishing[keyValuePair.Key] = true;
                                                       ConfigChanged = true;
                                                   }

                                                   ImGui.SameLine();
                                                   if (ImGui.Button(T("DeselectAll")))
                                                   {
                                                       foreach (var keyValuePair in Configuration.EnableCharacterForOCFishing) Configuration.EnableCharacterForOCFishing[keyValuePair.Key] = false;
                                                       ConfigChanged = true;
                                                   }
                                               });
            DrawCentered("##BoatFilteredCharSelector", () =>
                                                       {
                                                           if (ImGui.Button(T("SelectAllShown")))
                                                           {
                                                               foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForOCFishing[character.CID] = true;
                                                               ConfigChanged = true;
                                                           }

                                                           ImGui.SameLine();
                                                           if (ImGui.Button(T("DeselectAllShown")))
                                                           {
                                                               foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForOCFishing[character.CID] = false;
                                                               ConfigChanged = true;
                                                           }
                                                       });
            DrawCentered("##CenteredARFisherTable", () => DrawARTable());
        }
        else
        {
            using var disabled = ImRaii.Disabled(IsTaskRunning(Name));
            DrawManualCharacters();
        }
    }

    private TableReorderable<BoatCharacter, Guid> CreateManualTable() => new(
            "##ManualBoatTable",
            [
                    new(T("CharacterName"), x => x.Name, 150, FilterType.String, ColumnAlignment.Center),
                    new(T("World"), x => x.World, 110, FilterType.MultiSelect, ColumnAlignment.Center),
                    new(T("DataCenter"), x => Worlds.TryGetValue(x.World, out var world) ? world.DataCenter.Value.Name.ExtractText() : string.Empty,
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
            size: new Vector2(500, 0),
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
        DrawCentered("##boatManualCharacters", manualTable.Draw);
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
                                                                               ? world.DataCenter.Value.Name.ExtractText()
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

    private void DrawSettings()
    {
        DrawCentered("##boatRoute", () =>
                                    {
                                        var route = Configuration.PreferRuby ? 1 : 0;
                                        if (ImGui.Combo(T("Route"), ref route, new[] { "Indigo", "Ruby" }, 2))
                                        {
                                            Configuration.PreferRuby = route == 1;
                                            ConfigChanged = true;
                                        }
                                    });

        using (ImRaii.Disabled(!SubscriptionManager.IsInitialized(IPCNames.AutoRetainer)))
        {
            DrawCentered("##boatArSelling", () =>
                                            {
                                                ConfigChanged |= ImGui.Checkbox(T("UseARItemSell"), ref Configuration.SellAfterVoyage);
                                                ImGui.SameLine();
                                                HelpMarker(() => ImGui.Text(T("UseARItemSellHelp")));
                                            });

            DrawCentered("##boatArLocalSelling", () =>
                                                 {
                                                     ConfigChanged |= ImGui.Checkbox(T("UseARLocalSell"), ref Configuration.SellAtLocalVendor);
                                                     ImGui.SameLine();
                                                     HelpMarker(() => ImGui.Text(T("UseARLocalSellHelp")));
                                                 });


        }

        DrawCentered("##boatDiscard", () => { ConfigChanged |= ImGui.Checkbox(T("DiscardAfterVoyage"), ref Configuration.DiscardAfterVoyage); });
        DrawCentered("##boatDiscardProvider", () =>
                                              {
                                                  var provider = Configuration.UseFeeshDiscard ? 1 : 0;
                                                  if (ImGui.Combo(T("DiscardProvider"), ref provider, new[] { "AutoRetainer", "WahTools (Feesh)" }, 2))
                                                  {
                                                      Configuration.UseFeeshDiscard = provider == 1;
                                                      ConfigChanged = true;
                                                  }
                                                  ImGui.SameLine();
                                                  HelpMarker(() => ImGui.Text(T("DiscardProviderHelp")));
                                              });
    }

    private void DrawARTable()
    {
        ARTable.Draw();
    }
}
