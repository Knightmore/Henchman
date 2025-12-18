using System.Linq;
using AutoRetainerAPI.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.Configuration;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using Henchman.TaskManager;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.OnABoat;

[Feature]
internal class OnABoatUI : FeatureUI
{
    internal static readonly OnABoat feature = new();

    private static  bool            configChanged;
    public override string          Name     => "On A Boat";
    public override string          Category => Henchman.Category.Economy;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Sailboat;

    // TODO: Fill help
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

    private Table<OfflineCharacterData> ARTable = new (
                                                    "##ARFisherTable",
                                                    new List<TableColumn<OfflineCharacterData>>
                                                    {
                                                            new("##Enabled", Alignment: ColumnAlignment.Center, Width: 35, DrawCustom: (x, index) =>
                                                                                                                                       {
                                                                                                                                           if (!C.EnableCharacterForOCFishing.TryAdd(x.CID, false))
                                                                                                                                           {
                                                                                                                                               var isEnabled = C.EnableCharacterForOCFishing[x.CID];
                                                                                                                                               if (isEnabled) ImGui.PushStyleColor(ImGuiCol.Button, 0xFF097000);

                                                                                                                                               if (ImGuiEx.IconButton($"\uf021###{x.CID}"))
                                                                                                                                               {
                                                                                                                                                   C.EnableCharacterForOCFishing[x.CID] = !isEnabled;
                                                                                                                                                   configChanged                        = true;
                                                                                                                                               }

                                                                                                                                               if (isEnabled) ImGui.PopStyleColor();
                                                                                                                                           }
                                                                                                                                           else
                                                                                                                                               configChanged = true;
                                                                                                                                       }),
                                                            new("Name", x => x.Name, 135, FilterType.String, Alignment : ColumnAlignment.Center),
                                                            new("World", x => x.World, 90, FilterType.MultiSelect, Alignment : ColumnAlignment.Center),
                                                            new("DataCenter", x => Worlds[x.World].DataCenter.Value.Name.ExtractText(), 90, FilterType.MultiSelect, Alignment : ColumnAlignment.Center),
                                                            new("Lvl", x => x.ClassJobLevelArray[17]
                                                                             .ToString(), 35, Alignment : ColumnAlignment.Center),
                                                            new("Inv.", x => x.InventorySpace.ToString(), 75, Alignment : ColumnAlignment.Center)
                                                    },
                                                    () => feature.GetCurrentARCharacterData(),
                                                    highlightPredicate: x => x.CID == Player.CID,
                                                    size: new Vector2(500, 0)
                                                   );

    private static Dictionary<string, World> Worlds =
            Svc.Data.GetExcelSheet<World>()
               .DistinctBy(x => x.Name.ExtractText())
               .ToDictionary(x => x.Name.ExtractText(), x => x);

    public override void Draw()
    {
        configChanged = false;
        var utcNow = DateTime.UtcNow;
        var hour   = utcNow.Hour;
        var minute = utcNow.Minute;
        var second = utcNow.Second;

        Layout.DrawInfoBox(() =>
                           {
                               if (StartButton() && !IsTaskEnqueued(Name)) EnqueueTask(new TaskRecord(feature.Start, "On A Boat", onDone: () => feature.UnsubscribeEvents(), onAbort: feature.UnsubscribeEvents, onError: feature.OnError));
                           }, () =>
                              {
                                  if (feature.IsRegistrationOpen)
                                  {
                                      var remaining = new TimeSpan(0, 14 - minute, 59 - second);
                                      ImGui.Text($"Registration open for {remaining.Minutes:D2}:{remaining.Seconds:D2} minutes");
                                  }
                                  else
                                  {
                                      var nextEvenHour = hour % 2 == 0
                                                                 ? hour + 2
                                                                 : hour + 1;
                                      var nextWindowStart = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc).AddHours(nextEvenHour);

                                      if (nextWindowStart <= utcNow)
                                          nextWindowStart = nextWindowStart.AddHours(2);

                                      var waitTime = nextWindowStart - utcNow;
                                      ImGui.Text($"Next Voyage in {waitTime.Hours:D2}:{waitTime.Minutes:D2}:{waitTime.Seconds:D2}");
                                  }
                              });

        DrawCentered("##boatVersatile", () =>
                                        {
                                            ImGui.Text("Use only Versatile Lure:");
                                            ImGui.SameLine(200 * GlobalFontScale);
                                            configChanged |= ImGui.Checkbox("##onlyVLure", ref C.UseOnlyVersatile);
                                        });

        if (SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            DrawCentered("##boatArDiscard", () =>
                                            {
                                                ImGui.Text("Use AR Discard after Voyage:");
                                                ImGui.SameLine(200 * GlobalFontScale);
                                                configChanged |= ImGui.Checkbox("##ARDiscard", ref C.DiscardAfterVoyage);
                                            });

            DrawCentered("##boatArCharacters", () =>
                                               {
                                                   ImGui.Text("Use with AutoRetainer Multimode:");
                                                   ImGui.SameLine(200 * GlobalFontScale);
                                                   configChanged |= ImGui.Checkbox("##HandleAR", ref C.OCFishingHandleAR);
                                               });

            DrawCentered("##boatArStopAt100", () =>
                                               {
                                                   ImGui.Text("Stop if selected chars are lvl 100:");
                                                   ImGui.SameLine(200 * GlobalFontScale);
                                                   configChanged |= ImGui.Checkbox("##stopAt100", ref C.OCFishingStop100);
                                               });
        }

        if (C.OCFishingHandleAR && SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            DrawCentered("##TradeCharSelector", () =>
                                                {
                                                    if (ImGui.Button("Select All"))
                                                    {
                                                        foreach (var keyValuePair in C.EnableCharacterForOCFishing) C.EnableCharacterForOCFishing[keyValuePair.Key] = true;
                                                        configChanged = true;
                                                    }

                                                    ImGui.SameLine();
                                                    if (ImGui.Button("Deselect All"))
                                                    {
                                                        foreach (var keyValuePair in C.EnableCharacterForOCFishing) C.EnableCharacterForOCFishing[keyValuePair.Key] = false;
                                                        configChanged = true;
                                                    }
                                                });
            DrawCentered("##TradeFilteredCharSelector", () =>
                                                        {
                                                            if (ImGui.Button("Select All Shown"))
                                                            {
                                                                foreach (var character in ARTable.FilteredItems) C.EnableCharacterForOCFishing[character.CID] = true;
                                                                configChanged = true;
                                                            }

                                                            ImGui.SameLine();
                                                            if (ImGui.Button("Deselect All Shown"))
                                                            {
                                                                foreach (var character in ARTable.FilteredItems) C.EnableCharacterForOCFishing[character.CID] = false;
                                                                configChanged = true;
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
                                                     configChanged |= ImGui.InputText("##character", ref C.OceanChar, 21);
                                                 });
            DrawCentered("##boatWorld", () =>
                                        {
                                            ImGui.Text("World:");
                                            ImGui.SameLine(200 * GlobalFontScale);
                                            ImGui.SetNextItemWidth(150f);
                                            if (ImGuiEx.ExcelSheetCombo<World>("##world", out var selectedWorld, s => s.FirstOrDefault(x => x.Name.ExtractText() == C.OceanWorld) is { } row
                                                                                                                              ? row.Name.ExtractText()
                                                                                                                              : string.Empty, x => x.Name.ExtractText(), x => x is { IsPublic: true, RowId: < 500 }))
                                            {
                                                C.OceanWorld  = selectedWorld.Name.ExtractText();
                                                configChanged = true;
                                            }
                                        });
        }

        if (configChanged) EzConfig.Save();
    }

    private void DrawARTable()
    {
        ARTable.Draw();
    }
}
