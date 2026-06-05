using AutoRetainerAPI.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using Henchman.Abstractions;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using System.Linq;
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

    public sealed override Configuration Configuration { get; init; }
    public override string Name => "On A Boat";
    public override Category Category => Category.Economy;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Sailboat;

    public override Action? Help => () =>
                                    {
                                        ImGui.Text(T("HelpText"));
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
    }

    private void DrawMain()
    {
        ConfigChanged = false;
        var utcNow = DateTime.UtcNow;
        var hour = utcNow.Hour;
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

        if (SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            DrawCentered("##boatArCharacters", () =>
                                               {
                                                   ImGui.Text(T("UseWithARMultimode"));
                                                   ImGui.SameLine();
                                                   ConfigChanged |= ImGui.Checkbox("##HandleAR", ref Configuration.OCFishingHandleAR);
                                               });

            DrawCentered("##boatArStopAt100", () =>
                                              {
                                                  ImGui.Text(T("StopAt100"));
                                                  //ImGui.SameLine(200 * GlobalFontScale);
                                                  ImGui.SameLine();
                                                  ConfigChanged |= ImGui.Checkbox("##stopAt100", ref Configuration.OCFishingStop100);
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
            DrawCentered("##boatSingleCharName", () =>
                                                 {
                                                     ImGui.Text(T("CharacterName"));
                                                     //ImGui.SameLine(200 * GlobalFontScale);
                                                     ImGui.SameLine();
                                                     ImGui.SetNextItemWidth(150f);
                                                     ConfigChanged |= ImGui.InputText("##character", ref Configuration.OceanChar, 21);
                                                 });
            DrawCentered("##boatWorld", () =>
                                        {
                                            ImGui.Text(T("World"));
                                            //ImGui.SameLine(200 * GlobalFontScale);
                                            ImGui.SameLine();
                                            ImGui.SetNextItemWidth(150f);
                                            if (ImGuiEx.ExcelSheetCombo<World>("##world", out var selectedWorld, s => s.FirstOrDefault(x => x.Name.ExtractText() == Configuration.OceanWorld) is { } row
                                                                                                                              ? row.Name.ExtractText()
                                                                                                                              : string.Empty, x => x.Name.ExtractText(), x => x is { IsPublic: true, RowId: < 500 }))
                                            {
                                                Configuration.OceanWorld = selectedWorld.Name.ExtractText();
                                                ConfigChanged = true;
                                            }
                                        });
        }

        if (ConfigChanged) SaveConfig(Configuration);
    }

    private void DrawSettings()
    {
        DrawCentered("##boatVersatile", () =>
                                        {
                                            ConfigChanged |= ImGui.Checkbox(T("UseOnlyVersatileLure"), ref Configuration.UseOnlyVersatile);
                                        });

        if (SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            DrawCentered("##boatArSelling", () =>
                                            {
                                                ConfigChanged |= ImGui.Checkbox(T("UseARItemSell"), ref Configuration.SellAfterVoyage);
                                                ImGui.SameLine();
                                                HelpMarker(() => ImGui.Text(T("UseARItemSellHelp")));
                                            });

            DrawCentered("##boatArLocalSelling", () =>
                                            {
                                                ConfigChanged |= ImGui.Checkbox(T("UseARLocalSell"), ref Configuration.SellAfterVoyage);
                                                ImGui.SameLine();
                                                HelpMarker(() => ImGui.Text(T("UseARLocalSellHelp")));
                                            });

            DrawCentered("##boatArDiscard", () =>
                                            {
                                                ConfigChanged |= ImGui.Checkbox(T("UseARDiscard"), ref Configuration.DiscardAfterVoyage);
                                            });
        }

        if (ConfigChanged) SaveConfig(Configuration);
    }

    private void DrawARTable()
    {
        ARTable.Draw();
    }
}
