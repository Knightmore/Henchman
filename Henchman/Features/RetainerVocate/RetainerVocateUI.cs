using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Henchman.Features.IntoTheLight;
using Lumina.Excel.Sheets;
using Underlings.Keybinds;
using Underlings.Modules;
using Underlings.TaskManager;
using Action = System.Action;

namespace Henchman.Features.RetainerVocate;

[Module]
public class RetainerVocateUI : ModuleUI<RetainerVocate, Configuration>
{
    internal readonly RetainerVocate Feature = new();
    private           bool           configChanged;

    public RetainerVocateUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public sealed override required Configuration   Configuration { get; init; }
    public override                 string          Name          => "Retainer Vocate";
    public override                 Enum            Category      => Henchman.Category.Economy;
    public override                 FontAwesomeIcon Icon          => FontAwesomeIcon.ConciergeBell;

    public override Action Help => () =>
                                   {
                                       ImGui.Text(T("HelpText"));
                                       DrawRequirements(Requirements);
                                   };

    public override bool LoginNeeded => true;

    [Keybind("Retainer Vocate - Start")]
    private void StartTask()
    {
        if (IsTaskRunning(Name)) return;
        TryStartTask(new TaskRecord(token => Feature.RunFullCreation(token, Configuration.UseMaxRetainerAmount
                                                                                    ? 10
                                                                                    : (uint)Configuration.RetainerAmount + 1, Configuration.RetainerClass, Configuration.QstClassJob), Name));
    }

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
            Text(Theme.ErrorRed, T("RetainersLocked"));
        else
        {
            DrawCentered("###StartRetainerVocate", () => Layout.DrawButton(() =>
                                                                           {
                                                                               if (StartButton()) StartTask();
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
                                                                      Text(Theme.ErrorRed, T("CantReadMaxRetainers"));
                                                                      Text(Theme.ErrorRed, T("InteractWithVocate"));
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
                                                              ImGui.SameLine(150          * GlobalFontScale);
                                                              ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                              configChanged |= ImGui.Combo("##retainerAmount", ref Configuration.RetainerAmount, Enumerable.Range(1, 10)
                                                                                                                                                           .Select(x => x.ToString())
                                                                                                                                                           .ToArray(), 10);
                                                              ImGui.Text(T("City"));
                                                              ImGui.SameLine(150          * GlobalFontScale);
                                                              ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                              configChanged |= EnumCombo("##retainerCity", ref Configuration.RetainerCity);

                                                              ImGui.Text(T("RetainerClass"));
                                                              ImGui.SameLine(150          * GlobalFontScale);
                                                              ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                              if (ExcelSheetCombo<ClassJob>("##retainerJob", out var selected, s => s.GetRowOrDefault(Configuration.RetainerClass) is { } row
                                                                                                                                            ? s.GetRow(Configuration.RetainerClass)
                                                                                                                                               .Abbreviation.ExtractText()
                                                                                                                                            : string.Empty, x => x.Abbreviation.ExtractText(),
                                                                                            x => x.RowId is >= 1 and <= 7 or >= 16 and <= 18 or 26))
                                                              {
                                                                  Configuration.RetainerClass = selected.RowId;
                                                                  configChanged               = true;
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
                                                                     ImGui.SameLine(150          * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= EnumCombo("##retainerRace", ref Configuration.RetainerRace);
                                                                     ImGui.Text(T("Gender"));
                                                                     ImGui.SameLine(150          * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= EnumCombo("##retainerGender", ref Configuration.RetainerGender);
                                                                     ImGui.Text(T("Personality"));
                                                                     ImGui.SameLine(150          * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= EnumCombo("##retainerPersonality", ref Configuration.RetainerPersonality);
                                                                     ImGui.Text(T("City"));
                                                                     ImGui.SameLine(150          * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= EnumCombo("##retainerCity", ref Configuration.RetainerCity);
                                                                     ImGui.Text(T("RetainerClass"));
                                                                     ImGui.SameLine(150          * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     if (ExcelSheetCombo<ClassJob>("##retainerJob", out var selected, s => s.GetRowOrDefault(Configuration.RetainerClass) is { } row
                                                                                                                                                   ? s.GetRow(Configuration.RetainerClass)
                                                                                                                                                      .Abbreviation.ExtractText()
                                                                                                                                                   : string.Empty, x => x.Abbreviation.ExtractText(),
                                                                                                   x => x.RowId is >= 1 and <= 7 or >= 16 and <= 18 or 26))
                                                                     {
                                                                         Configuration.RetainerClass = selected.RowId;
                                                                         configChanged               = true;
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
                                                         ImGui.SameLine(150          * GlobalFontScale);
                                                         ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                         if (ExcelSheetCombo<ClassJob>("##qstCombatJob", out var classJobSheet, s => s.GetRowOrDefault(Configuration.QstClassJob) is { } row
                                                                                                                                             ? s.GetRow(Configuration.QstClassJob)
                                                                                                                                                .Abbreviation.ExtractText()
                                                                                                                                             : string.Empty,
                                                                                       x => x.Abbreviation.ExtractText(), x => x.RowId is >= 1 and <= 7 or >= 19 and <= 42))
                                                         {
                                                             Configuration.QstClassJob = classJobSheet.RowId;
                                                             configChanged             = true;
                                                         }
                                                     });

            if (ImGui.CollapsingHeader($"{T("SingleBackupTasks")}##singleTasks"))
            {
                if (RetainerManager.Instance()->MaxRetainerEntitlement                                                  == 0 ||
                    RetainerManager.Instance()->MaxRetainerEntitlement - RetainerManager.Instance()->GetRetainerCount() > 0)
                {
                    if (ImGui.Button(Configuration.UseMaxRetainerAmount                      ? T("CreateRetainers") :
                                     RetainerManager.Instance()->MaxRetainerEntitlement == 0 ? T("GoToVocate") : T("CreateRetainers")) &&
                        !IsPluginBusy)
                    {
                        TryStartTask(new TaskRecord(Feature.GoToRetainerVocate, "Go to Retainer Vocate"));
                        if (Configuration.UseMaxRetainerAmount || RetainerManager.Instance()->MaxRetainerEntitlement != 0)
                        {
                            TryStartTask(new TaskRecord(token => Feature.CreateRetainers(token, Configuration.UseMaxRetainerAmount
                                                                                                        ? 10
                                                                                                        : Configuration.RetainerAmount + 1), "Create Retainers"));
                        }
                    }
                }
                else
                    Text(Theme.ErrorRed, T("CannotCreateMoreRetainers"));

                if (!QuestManager.IsQuestComplete(66968) && !QuestManager.IsQuestComplete(66969) && !QuestManager.IsQuestComplete(66970))
                {
                    var classJob = Svc.Data.GetExcelSheet<ClassJob>()
                                      .GetRow(Configuration.QstClassJob);
                    var gearset = GetFirstGearsetForClassJob(classJob);
                    ImGui.NewLine();
                    ImGui.Text(T("Questionable"));
                    ImGui.Text(T("IllConceivedVenture"));
                    if (gearset == null)
                        Text(Theme.ErrorRed, T("NoGearsetForClass"));
                    else if (ImGui.Button(T("RunQuest")) && !Questionable.IsRunning.Invoke() && !IsPluginBusy)
                    {
                        ErrorIf(!ChangeToHighestGearsetForClassJobId(Configuration.QstClassJob), $"No gearset for {Configuration.QstClassJob} found!");
                        if (!SubscriptionManager.IsLoaded(IPCNames.Questionable))
                        {
                            FullError("'Questionable' not available. Skipping Venture Quest and equipping Retainers.");
                            return;
                        }

                        TryStartTask(new TaskRecord(token => Feature.StartVentureQuest(token, Configuration.QstClassJob), "Do Retainer Venture Quest"));
                    }
                }

                if (ImGui.Button(T("AssignClass")))
                {
                    if (RetainerManager.Instance()->MaxRetainerEntitlement == 0)
                        TryStartTask(new TaskRecord(Feature.GoToRetainerVocate, "Go to Retainer Vocate"));
                    TryStartTask(new TaskRecord(token => Feature.BuyAndEquipRetainerGear(token, Configuration.UseMaxRetainerAmount
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
                                                                                                       ImGui.SetNextItemWidth(160f * GlobalFontScale);
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
                                                                                                       ImGui.SetNextItemWidth(110f * GlobalFontScale);
                                                                                                       configChanged |= EnumCombo($"##retainerRace{index}", ref retainer.Race);
                                                                                                   }),
                                                         new(T("ColGender"), Width: 110, DrawCustom: (retainer, index) =>
                                                                                                     {
                                                                                                         ImGui.SetNextItemWidth(110f * GlobalFontScale);
                                                                                                         configChanged |= EnumCombo($"##retainerGender{index}", ref retainer.Gender);
                                                                                                     }),
                                                         new(T("ColClan"), Width: 150, DrawCustom: (retainer, index) =>
                                                                                                   {
                                                                                                       ImGui.SetNextItemWidth(150f * GlobalFontScale);
                                                                                                       configChanged |= ClanCombo($"##retainerTribe{index}", retainer.Race - 10, ref retainer.Clan);
                                                                                                   }),
                                                         new(T("ColPersonality"), Width: 110, DrawCustom: (retainer, index) =>
                                                                                                          {
                                                                                                              ImGui.SetNextItemWidth(110f * GlobalFontScale);
                                                                                                              configChanged |= EnumCombo($"##retainerPersonality{index}", ref retainer.Personality);
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

                                                                                                         ImGui.SetNextItemWidth(130f * GlobalFontScale);
                                                                                                         if (Combo($"##preset{index}", ref denseSelected, denseIds, names: names))
                                                                                                         {
                                                                                                             retainer.PresetId = denseSelected == 255
                                                                                                                                         ? ((byte)255, (byte)255)
                                                                                                                                         : (denseSelected, denseToReal[denseSelected]);


                                                                                                             configChanged = true;
                                                                                                         }
                                                                                                     })
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

        var tribeSheet    = Svc.Data.GetExcelSheet<Tribe>();
        var firstTribeRow = (raceIndex * 2) + 1;

        var names = new Dictionary<int, string>
                    {
                            [1] = tribeSheet.GetRow((uint)firstTribeRow)
                                            .Masculine.ExtractText(),
                            [2] = tribeSheet.GetRow((uint)(firstTribeRow + 1))
                                            .Masculine.ExtractText()
                    };

        return Combo(id, ref clan, [1, 2], names: names);
    }
}
