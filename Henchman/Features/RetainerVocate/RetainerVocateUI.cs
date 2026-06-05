using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Henchman.Abstractions;
using Henchman.Features.IntoTheLight;
using Henchman.TaskManager;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using System.Linq;
using Action = System.Action;

namespace Henchman.Features.RetainerVocate;

[Feature]
public class RetainerVocateUI : FeatureUI<RetainerVocate, Configuration>
{
    internal readonly RetainerVocate Feature = new();
    private bool configChanged;

    public RetainerVocateUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public sealed override required Configuration Configuration { get; init; }
    public override string Name => "Retainer Vocate";
    public override Category Category => Category.Economy;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.ConciergeBell;

    public override Action Help => () =>
                                   {
                                       ImGui.Text(T("HelpText"));
                                       DrawRequirements(Requirements);
                                   };

    public override bool LoginNeeded => true;

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.Questionable, true)
    ];

    public override unsafe void Draw()
    {
        configChanged = false;

        if (!QuestManager.IsQuestComplete(66196))
            ImGuiEx.Text(EzColor.Red, T("RetainersLocked"));
        else
        {
            DrawCentered("###StartRetainerVocate", () => Layout.DrawButton(() =>
                                                                           {
                                                                               if (StartButton() && !IsTaskEnqueued(Name))
                                                                               {
                                                                                   EnqueueTask(new TaskRecord(token => Feature.RunFullCreation(token, Configuration.UseMaxRetainerAmount
                                                                                                                                                              ? 10
                                                                                                                                                              : (uint)Configuration.RetainerAmount + 1, Configuration.RetainerClass, Configuration.QstClassJob), Name));
                                                                               }
                                                                           }));

            if (!Configuration.UseMaxRetainerAmount)
            {
                if (RetainerManager.Instance()->MaxRetainerEntitlement == 0)
                {
                    DrawCentered("##RetainerVocateNoEntitlement", () =>
                                                                  {
                                                                      ImGui.Text(T("FillAllSlots"));
                                                                      ImGui.SameLine(150 * GlobalFontScale);
                                                                      configChanged |= ImGui.Checkbox("##fillAllSlots", ref Configuration.UseMaxRetainerAmount);
                                                                      ImGuiEx.Text(EzColor.Red, T("CantReadMaxRetainers"));
                                                                      ImGuiEx.Text(EzColor.Red, T("InteractWithVocate"));
                                                                  });
                }
                else
                {
                    DrawCentered("##RetainerVocateSlots", () =>
                                                          {
                                                              ImGui.Text(T("FillAllSlots"));
                                                              ImGui.SameLine(150 * GlobalFontScale);
                                                              configChanged |= ImGui.Checkbox("##fillAllSlots", ref Configuration.UseMaxRetainerAmount);
                                                              ImGui.Text(T("RetainerAmount"));
                                                              ImGui.SameLine(150 * GlobalFontScale);
                                                              ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                              configChanged |= ImGui.Combo("##retainerAmount", ref Configuration.RetainerAmount, Enumerable.Range(1, 10)
                                                                                                                                                           .Select(x => x.ToString())
                                                                                                                                                           .ToArray(), 10);
                                                              ImGui.Text(T("City"));
                                                              ImGui.SameLine(150 * GlobalFontScale);
                                                              ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                              configChanged |= ImGuiEx.EnumCombo("##retainerCity", ref Configuration.RetainerCity);

                                                              ImGui.Text(T("RetainerClass"));
                                                              ImGui.SameLine(150 * GlobalFontScale);
                                                              ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                              if (ImGuiEx.ExcelSheetCombo<ClassJob>("##retainerJob", out var selected, s => s.GetRowOrDefault(Configuration.RetainerClass) is { } row
                                                                                                                                                    ? s.GetRow(Configuration.RetainerClass)
                                                                                                                                                       .Abbreviation.ExtractText()
                                                                                                                                                    : string.Empty, x => x.Abbreviation.ExtractText(),
                                                                                                    x => x.RowId is >= 1 and <= 7 or >= 16 and <= 18 or 26))
                                                              {
                                                                  Configuration.RetainerClass = selected.RowId;
                                                                  configChanged = true;
                                                              }

                                                              ImGui.Text(T("AssignExploration"));
                                                              ImGui.SameLine(150 * GlobalFontScale);
                                                              configChanged |= ImGui.Checkbox("##firstExploration", ref Configuration.SendOnFirstExploration);
                                                          });

                    DrawCentered("##RetainerVocateNameList", () => DrawRetainerVocateTable());
                }
            }
            else
            {
                DrawCentered("##RetainerVocateRandomizeDetails", () =>
                                                                 {
                                                                     ImGui.Text(T("FillAllSlots"));
                                                                     ImGui.SameLine(150 * GlobalFontScale);
                                                                     configChanged |= ImGui.Checkbox("##fillAllSlots", ref Configuration.UseMaxRetainerAmount);
                                                                     ImGui.Text(T("Race"));
                                                                     ImGui.SameLine(150 * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= ImGuiEx.EnumCombo("##retainerRace", ref Configuration.RetainerRace);
                                                                     ImGui.Text(T("Gender"));
                                                                     ImGui.SameLine(150 * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= ImGuiEx.EnumCombo("##retainerGender", ref Configuration.RetainerGender);
                                                                     ImGui.Text(T("Personality"));
                                                                     ImGui.SameLine(150 * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= ImGuiEx.EnumCombo("##retainerPersonality", ref Configuration.RetainerPersonality);
                                                                     ImGui.Text(T("City"));
                                                                     ImGui.SameLine(150 * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= ImGuiEx.EnumCombo("##retainerCity", ref Configuration.RetainerCity);
                                                                     ImGui.Text(T("RetainerClass"));
                                                                     ImGui.SameLine(150 * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     if (ImGuiEx.ExcelSheetCombo<ClassJob>("##retainerJob", out var selected, s => s.GetRowOrDefault(Configuration.RetainerClass) is { } row
                                                                                                                                                           ? s.GetRow(Configuration.RetainerClass)
                                                                                                                                                              .Abbreviation.ExtractText()
                                                                                                                                                           : string.Empty, x => x.Abbreviation.ExtractText(),
                                                                                                           x => x.RowId is >= 1 and <= 7 or >= 16 and <= 18 or 26))
                                                                     {
                                                                         Configuration.RetainerClass = selected.RowId;
                                                                         configChanged = true;
                                                                     }

                                                                     ImGui.Text(T("AssignExploration"));
                                                                     ImGui.SameLine(150 * GlobalFontScale);
                                                                     configChanged |= ImGui.Checkbox("##firstExploration", ref Configuration.SendOnFirstExploration);
                                                                 });
            }

            ImGui.Separator();

            ImGui.NewLine();

            DrawCentered("##RetainerVocateQstClass", () =>
                                                     {
                                                         ImGui.Text(T("ClassJobForQuest"));
                                                         ImGui.SameLine(150 * GlobalFontScale);
                                                         ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                         if (ImGuiEx.ExcelSheetCombo<ClassJob>("##qstCombatJob", out var classJobSheet, s => s.GetRowOrDefault(Configuration.QstClassJob) is { } row
                                                                                                                                                     ? s.GetRow(Configuration.QstClassJob)
                                                                                                                                                        .Abbreviation.ExtractText()
                                                                                                                                                     : string.Empty,
                                                                                               x => x.Abbreviation.ExtractText(), x => x.RowId is >= 1 and <= 7 or >= 19 and <= 42))
                                                         {
                                                             Configuration.QstClassJob = classJobSheet.RowId;
                                                             configChanged = true;
                                                         }
                                                     });

            if (ImGui.CollapsingHeader($"{T("SingleBackupTasks")}##singleTasks"))
            {
                if (RetainerManager.Instance()->MaxRetainerEntitlement == 0 ||
                    RetainerManager.Instance()->MaxRetainerEntitlement - RetainerManager.Instance()->GetRetainerCount() > 0)
                {
                    if (ImGui.Button(Configuration.UseMaxRetainerAmount ? T("CreateRetainers") :
                                     RetainerManager.Instance()->MaxRetainerEntitlement == 0 ? T("GoToVocate") : T("CreateRetainers")) &&
                        !IsPluginBusy)
                    {
                        EnqueueTask(new TaskRecord(Feature.GoToRetainerVocate, "Go to Retainer Vocate"));
                        if (Configuration.UseMaxRetainerAmount || RetainerManager.Instance()->MaxRetainerEntitlement != 0)
                        {
                            EnqueueTask(new TaskRecord(token => Feature.CreateRetainers(token, Configuration.UseMaxRetainerAmount
                                                                                                       ? 10
                                                                                                       : Configuration.RetainerAmount + 1), "Create Retainers"));
                        }
                    }
                }
                else
                    ImGuiEx.Text(EzColor.Red, T("CannotCreateMoreRetainers"));

                if (!QuestManager.IsQuestComplete(66968) && !QuestManager.IsQuestComplete(66969) && !QuestManager.IsQuestComplete(66970))
                {
                    var classJob = Svc.Data.GetExcelSheet<ClassJob>()
                                      .GetRow(Configuration.QstClassJob);
                    var gearset = GetFirstGearsetForClassJob(classJob);
                    ImGui.NewLine();
                    ImGui.Text(T("Questionable"));
                    ImGui.Text(T("IllConceivedVenture"));
                    if (gearset == null)
                        ImGuiEx.Text(EzColor.Red, T("NoGearsetForClass"));
                    else if (ImGui.Button(T("RunQuest")) && !Questionable.IsRunning() && !IsPluginBusy)
                    {
                        ErrorIf(!ChangeToHighestGearsetForClassJobId(Configuration.QstClassJob), $"No gearset for {Configuration.QstClassJob} found!");
                        if (!SubscriptionManager.IsInitialized(IPCNames.Questionable))
                        {
                            FullError("'Questionable' not available. Skipping Venture Quest and equipping Retainers.");
                            return;
                        }

                        EnqueueTask(new TaskRecord(token => Feature.StartVentureQuest(token, Configuration.QstClassJob), "Do Retainer Venture Quest"));
                    }
                }

                if (ImGui.Button(T("AssignClass")))
                {
                    if (RetainerManager.Instance()->MaxRetainerEntitlement == 0)
                        EnqueueTask(new TaskRecord(Feature.GoToRetainerVocate, "Go to Retainer Vocate"));
                    EnqueueTask(new TaskRecord(token => Feature.BuyAndEquipRetainerGear(token, Configuration.UseMaxRetainerAmount
                                                                                                       ? 10
                                                                                                       : (uint)Configuration.RetainerAmount + 1, Configuration.UseMaxRetainerAmount
                                                                                                                                                         ? Configuration.RetainerClass
                                                                                                                                                         : 0), "Buy and Equip Retainer Gear"));
                }
            }
        }

        if (configChanged) SaveConfig(Configuration);
    }

    private unsafe void DrawRetainerVocateTable()
    {
        var table = new Table<RetainerCharacter>(
                                                 "##RetainerVocateTable",
                                                 new List<TableColumn<RetainerCharacter>>
                                                 {
                                                         new(T("ColName"), Width: 160, DrawCustom: (retainer, index) =>
                                                                                             {
                                                                                                 if (index > Configuration.RetainerAmount) return;
                                                                                                 var oldName = retainer.Name;
                                                                                                 ImGui.SetNextItemWidth(160f);
                                                                                                 if (ImGui.InputText($"##newFirstName{index}", ref retainer.Name, 20))
                                                                                                 {
                                                                                                     var duplicate = Configuration.RetainerCharacters
                                                                                                                                  .Where((name, idx) => idx != index)
                                                                                                                                  .Any(name => name.Name == Configuration.RetainerCharacters[index].Name);
                                                                                                     if (duplicate)
                                                                                                         retainer.Name = oldName;
                                                                                                     else
                                                                                                         configChanged = true;
                                                                                                 }
                                                                                             }),
                                                         new(T("ColRace"), Width: 110, DrawCustom: (retainer, index) =>
                                                                                             {
                                                                                                 ImGui.SetNextItemWidth(110f);
                                                                                                 configChanged |= ImGuiEx.EnumCombo($"##retainerRace{index}", ref retainer.Race);
                                                                                             }),
                                                         new(T("ColGender"), Width: 110, DrawCustom: (retainer, index) =>
                                                                                               {
                                                                                                   ImGui.SetNextItemWidth(110f);
                                                                                                   configChanged |= ImGuiEx.EnumCombo($"##retainerGender{index}", ref retainer.Gender);
                                                                                               }),
                                                         new(T("ColClan"), Width: 150, DrawCustom: (retainer, index) =>
                                                                                             {
                                                                                                 ImGui.SetNextItemWidth(150f);
                                                                                                 configChanged |= ClanCombo($"##retainerTribe{index}", retainer.Race - 10, ref retainer.Clan);
                                                                                             }),
                                                         new(T("ColPersonality"), Width: 110, DrawCustom: (retainer, index) =>
                                                                                                    {
                                                                                                        ImGui.SetNextItemWidth(110f);
                                                                                                        configChanged |= ImGuiEx.EnumCombo($"##retainerPersonality{index}", ref retainer.Personality);
                                                                                                    }),
                                                         new(T("ColPreset"), Width: 130, DrawCustom: (retainer, index) =>
                                                                                               {
                                                                                                   var presetId = retainer.PresetId;

                                                                                                   var presets = Framework.Instance()->CharamakeAvatarSaveData->Release.Slots.ToArray()
                                                                                                                                                                       .Where(x => x.Timestamp > 0)
                                                                                                                                                                       .OrderBy(x => x.SlotIndex)
                                                                                                                                                                       .ToArray();

                                                                                                   var realIndices = presets.Select(p => p.SlotIndex)
                                                                                                                            .ToArray();

                                                                                                   var denseIds = Enumerable.Range(0, realIndices.Length)
                                                                                                                            .Select(i => (byte)i)
                                                                                                                            .ToList();

                                                                                                   denseIds.Insert(0, 255);

                                                                                                   var denseToReal = new Dictionary<byte, byte>();
                                                                                                   denseToReal[255] = 255;
                                                                                                   for (byte i = 0; i < realIndices.Length; i++)
                                                                                                       denseToReal[i] = realIndices[i];

                                                                                                   var names = new Dictionary<byte, string>();
                                                                                                   names[255] = T("None");
                                                                                                   for (byte i = 0; i < realIndices.Length; i++)
                                                                                                   {
                                                                                                       var real = realIndices[i];
                                                                                                       var label = presets.First(p => p.SlotIndex == real)
                                                                                                                          .LabelString;
                                                                                                       names[i] = $"{real} - {label}";
                                                                                                   }

                                                                                                   var denseSelected =
                                                                                                           presetId.RealIndex == 255
                                                                                                                   ? (byte)255
                                                                                                                   : denseToReal.First(x => x.Value == presetId.RealIndex)
                                                                                                                                .Key;

                                                                                                   ImGui.SetNextItemWidth(130f);
                                                                                                   if (ImGuiEx.Combo($"##preset{index}", ref denseSelected, denseIds, names: names))
                                                                                                   {
                                                                                                       retainer.PresetId = denseSelected == 255
                                                                                                                                   ? ((byte)255, (byte)255)
                                                                                                                                   : (denseSelected, denseToReal[denseSelected]);


                                                                                                       configChanged = true;
                                                                                                   }


                                                                                                   /*var presetId = retainer.PresetId;

                                                                                                   var presets = Framework.Instance()->CharamakeAvatarSaveData->Release.Slots
                                                                                                                                                                       .ToArray()
                                                                                                                                                                       .Where(x => x.Timestamp > 0)
                                                                                                                                                                       .ToArray();

                                                                                                   var presetIds = new[] { (byte)255 }
                                                                                                          .Concat(presets.Select(x => x.SlotIndex));

                                                                                                   var names = presets.ToDictionary(
                                                                                                                                    y => y.SlotIndex,
                                                                                                                                    y => $"{y.SlotIndex} - {y.LabelString}"
                                                                                                                                   );
                                                                                                   names[255] = T("None");

                                                                                                   ImGui.SetNextItemWidth(130f);
                                                                                                   if (ImGuiEx.Combo($"##preset{index}", ref presetId.Item1, presetIds, names: names))
                                                                                                   {
                                                                                                       retainer.PresetId = presetId;
                                                                                                       configChanged         = true;
                                                                                                   }*/
                                                                                               })
                                                         /*new("Class", Width: 70, DrawCustom: (retainer, index) =>
                                                                                             {
                                                                                                 ImGui.SetNextItemWidth(70f);
                                                                                                 if (ImGuiEx.ExcelSheetCombo<ClassJob>($"##retainerJob{index}", out var selected, s => s.GetRowOrDefault(retainer.Class) is { } row
                                                                                                                                                                                               ? s.GetRow(retainer.Class)
                                                                                                                                                                                                  .Abbreviation.ExtractText()
                                                                                                                                                                                               : string.Empty, x => x.Abbreviation.ExtractText(),
                                                                                                                                       x => x.RowId is >= 1 and <= 7 or >= 16 and <= 18 or 26))
                                                                                                 {
                                                                                                     retainer.Class = selected.RowId;
                                                                                                     configChanged  = true;
                                                                                                 }
                                                                                             })*/
                                                 },
                                                 () => Configuration.RetainerCharacters,
                                                 Configuration.RetainerAmount + 1,
                                                 size: new Vector2(840, 27 + ((Configuration.RetainerAmount + 1) * 27))
                                                );

        table.Draw();
    }

    private static bool ClanCombo(string id, RetainerDetails.RetainerRace race, ref int clan)
    {
        var raceIndex = (int)race; // 0-7

        if (raceIndex < 0 || raceIndex > 7)
            return false;

        clan = Math.Clamp(clan, 1, 2);

        var tribeSheet = Svc.Data.GetExcelSheet<Tribe>();
        var firstTribeRow = (raceIndex * 2) + 1;

        var names = new Dictionary<int, string>
        {
            [1] = tribeSheet.GetRow((uint)firstTribeRow)
                                            .Masculine.ExtractText(),
            [2] = tribeSheet.GetRow((uint)(firstTribeRow + 1))
                                            .Masculine.ExtractText()
        };

        return ImGuiEx.Combo(id, ref clan, [1, 2], names: names);
    }
}
