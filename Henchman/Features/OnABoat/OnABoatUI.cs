using System.Linq;
using AutoRetainerAPI.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using Henchman.Abstractions;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.OnABoat;

[Feature]
internal class OnABoatUI : FeatureUI<OnABoat, Configuration>
{
    private static bool ConfigChanged;

    private static readonly Dictionary<string, World> Worlds =
            Svc.Data.GetExcelSheet<World>()
               .DistinctBy(x => x.Name.ExtractText())
               .ToDictionary(x => x.Name.ExtractText(), x => x);

    private readonly Table<OfflineCharacterData> ARTable;

    public OnABoatUI()
    {
        Configuration = LoadConfig<Configuration>() ?? new Configuration();

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

                                                                                                                                             if (ImGuiEx.IconButton($"\uf021###{x.CID}"))
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
    }

    public sealed override Configuration   Configuration { get; init; }
    public override        string          Name          => "On A Boat";
    public override        Category        Category      => Category.Economy;
    public override        FontAwesomeIcon Icon          => FontAwesomeIcon.Sailboat;

    public override Action? Help => () =>
                                    {
                                        ImGui.Text("""
                                                   AutoRetainer Mode:
                                                   - Enable all your characters that you want to cycle through for ocean fishing.
                                                   - On A Boat will always log into the character with the lowest fisher level.
                                                   - If you don't have enough characters with retainers running to end in PostProcess,
                                                     setup your AutoRetainer to wait on Titlescreen.
                                                        
                                                   Single Character Mode:
                                                   - Just enter your character name, pick its world and On A Boat will level only with that character.

                                                   If you don't have completed the ocean fishing quest already, it will be done for you. (Requires Questionable)
                                                   To take less travelling time, it will attune to the Arcanists' Guild aetheryte to use the athernet next time.
                                                   """);

                                        DrawRequirements(Requirements);
                                    };

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.AutoHook, true),
            (IPCNames.AutoRetainer, false),
            (IPCNames.Questionable, false)
    ];

    public override bool LoginNeeded => false;

    public void Start() => Feature.RunTask();

    public override void Draw()
    {
        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs)
        {
            using (var tab = ImRaii.TabItem("Main"))
            {
                if (tab)
                    DrawMain();
            }

            using (var tab = ImRaii.TabItem("Settings"))
            {
                if (tab)
                    DrawSettings();
            }
        }
    }

    private void DrawMain()
    {
        ConfigChanged = false;
        var utcNow = DateTime.UtcNow;
        var hour   = utcNow.Hour;
        var minute = utcNow.Minute;
        var second = utcNow.Second;

        Layout.DrawInfoBox(() =>
                           {
                               if (StartButton() && !IsTaskEnqueued(Name)) Start();
                           }, () =>
                              {
                                  if (Feature.IsRegistrationOpen)
                                  {
                                      var remaining = new TimeSpan(0, 14 - minute, 59 - second);
                                      ImGui.Text($"Registration open for {remaining.Minutes:D2}:{remaining.Seconds:D2} minutes");
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
                                      ImGui.Text($"Next Voyage in {waitTime.Hours:D2}:{waitTime.Minutes:D2}:{waitTime.Seconds:D2}");
                                  }
                              });

        if (SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            DrawCentered("##boatArCharacters", () =>
                                               {
                                                   ImGui.Text("Use with AutoRetainer Multimode:");
                                                   ImGui.SameLine(200 * GlobalFontScale);
                                                   ConfigChanged |= ImGui.Checkbox("##HandleAR", ref Configuration.OCFishingHandleAR);
                                               });

            DrawCentered("##boatArStopAt100", () =>
                                              {
                                                  ImGui.Text("Stop if selected chars are lvl 100:");
                                                  ImGui.SameLine(200 * GlobalFontScale);
                                                  ConfigChanged |= ImGui.Checkbox("##stopAt100", ref Configuration.OCFishingStop100);
                                              });
        }

        if (Configuration.OCFishingHandleAR && SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            DrawCentered("##BoatCharSelector", () =>
                                               {
                                                   if (ImGui.Button("Select All"))
                                                   {
                                                       foreach (var keyValuePair in Configuration.EnableCharacterForOCFishing) Configuration.EnableCharacterForOCFishing[keyValuePair.Key] = true;
                                                       ConfigChanged = true;
                                                   }

                                                   ImGui.SameLine();
                                                   if (ImGui.Button("Deselect All"))
                                                   {
                                                       foreach (var keyValuePair in Configuration.EnableCharacterForOCFishing) Configuration.EnableCharacterForOCFishing[keyValuePair.Key] = false;
                                                       ConfigChanged = true;
                                                   }
                                               });
            DrawCentered("##BoatFilteredCharSelector", () =>
                                                       {
                                                           if (ImGui.Button("Select All Shown"))
                                                           {
                                                               foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForOCFishing[character.CID] = true;
                                                               ConfigChanged = true;
                                                           }

                                                           ImGui.SameLine();
                                                           if (ImGui.Button("Deselect All Shown"))
                                                           {
                                                               foreach (var character in ARTable.FilteredItems) Configuration.EnableCharacterForOCFishing[character.CID] = false;
                                                               ConfigChanged = true;
                                                           }
                                                       });
            DrawCentered("##CenteredARFisherTable", () => DrawARTable());
        }
        else
        {
            DrawCentered("##boatSingleCharName", () =>
                                                 {
                                                     ImGui.Text("Character Name:");
                                                     ImGui.SameLine(200 * GlobalFontScale);
                                                     ImGui.SetNextItemWidth(150f);
                                                     ConfigChanged |= ImGui.InputText("##character", ref Configuration.OceanChar, 21);
                                                 });
            DrawCentered("##boatWorld", () =>
                                        {
                                            ImGui.Text("World:");
                                            ImGui.SameLine(200 * GlobalFontScale);
                                            ImGui.SetNextItemWidth(150f);
                                            if (ImGuiEx.ExcelSheetCombo<World>("##world", out var selectedWorld, s => s.FirstOrDefault(x => x.Name.ExtractText() == Configuration.OceanWorld) is { } row
                                                                                                                              ? row.Name.ExtractText()
                                                                                                                              : string.Empty, x => x.Name.ExtractText(), x => x is { IsPublic: true, RowId: < 500 }))
                                            {
                                                Configuration.OceanWorld = selectedWorld.Name.ExtractText();
                                                ConfigChanged            = true;
                                            }
                                        });
        }

        if (ConfigChanged) SaveConfig(Configuration);
    }

    private void DrawSettings()
    {
        DrawCentered("##boatVersatile", () =>
                                        {
                                            ImGui.Text("Use only Versatile Lure");
                                            ImGui.SameLine(200 * GlobalFontScale);
                                            ConfigChanged |= ImGui.Checkbox("##onlyVLure", ref Configuration.UseOnlyVersatile);
                                        });

        if (SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            DrawCentered("##boatArSelling", () =>
                                            {
                                                ImGui.Text("Use AR ItemSell after Voyage");
                                                ImGui.SameLine();
                                                HelpMarker(() => ImGui.Text("This will only work if you return to a destination with a retainer bell/vendor NPC nearby."));
                                                ImGui.SameLine(200 * GlobalFontScale);
                                                ConfigChanged |= ImGui.Checkbox("##ARItemSell", ref Configuration.SellAfterVoyage);
                                            });

            DrawCentered("##boatArDiscard", () =>
                                            {
                                                ImGui.Text("Use AR Discard after Voyage");
                                                ImGui.SameLine(200 * GlobalFontScale);
                                                ConfigChanged |= ImGui.Checkbox("##ARDiscard", ref Configuration.DiscardAfterVoyage);
                                            });
        }

        if (ConfigChanged) SaveConfig(Configuration);
    }

    private void DrawARTable()
    {
        ARTable.Draw();
    }
}
