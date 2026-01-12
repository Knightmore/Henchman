using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using Henchman.Features.IntoTheLight;
using Henchman.Helpers;
using Henchman.TaskManager;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.RetainerVocate;

[Feature]
public class RetainerVocateUI : FeatureUI<Configuration>
{
    internal readonly               RetainerVocate  Feature = new();
    private                         bool            configChanged;
    public sealed override required Configuration   Configuration { get; init; }
    public override                 string          Name          => "Retainer Vocate";
    public override                 string          Category      => Henchman.Category.Economy;
    public override                 FontAwesomeIcon Icon          => FontAwesomeIcon.ConciergeBell;

    public override Action Help => () =>
                                   {
                                       ImGui.Text("""
                                                  Set up how you want your retainer to be and click 'Create Retainers'
                                                  If you haven't already done the Venture Quest, it will run after creating retainers.

                                                  If any task fails or you somehow messed up midway, 
                                                  you can try to continue through one of the 'Single Backup Tasks' 
                                                  """);

                                       DrawRequirements(Requirements);
                                   };

    public override bool LoginNeeded => true;

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.Questionable, true)
    ];

    public RetainerVocateUI()
    {
        Configuration = LoadConfig<Configuration>() ?? new Configuration();
    }

    public override unsafe void Draw()
    {
        configChanged = false;

        if (!QuestManager.IsQuestComplete(66196))
            ImGuiEx.Text(EzColor.Red, "Retainers are not unlocked. Proceed with MSQ and finish \"The Scions of the Seventh Dawn\".");
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


            DrawCentered("##RetainerVocateSlots", () =>
                                                  {
                                                      if (!Configuration.UseMaxRetainerAmount)
                                                      {
                                                          if (RetainerManager.Instance()->MaxRetainerEntitlement == 0)
                                                          {
                                                              ImGuiEx.Text(EzColor.Red, "Could not read the maximum allowed amount of retainers on your account.");
                                                              ImGuiEx.Text(EzColor.Red, "Please interact with a \"Retainer Vocate\" to progress.");
                                                          }
                                                          else
                                                          {
                                                              ImGui.Text("Fill all retainer slots");
                                                              ImGui.SameLine(150 * GlobalFontScale);
                                                              configChanged |= ImGui.Checkbox("##fillAllSlots", ref Configuration.UseMaxRetainerAmount);
                                                              ImGui.Text("Retainer amount");
                                                              ImGui.SameLine(150          * GlobalFontScale);
                                                              ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                              configChanged |= ImGui.Combo("##retainerAmount", ref Configuration.RetainerAmount, Enumerable.Range(1, 10)
                                                                                                                                                           .Select(x => x.ToString())
                                                                                                                                                           .ToArray(), 10);
                                                              ImGui.Text("City");
                                                              ImGui.SameLine(150          * GlobalFontScale);
                                                              ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                              configChanged |= ImGuiEx.EnumCombo("##retainerCity", ref Configuration.RetainerCity);
                                                              ImGui.Text("Assign Exploration");
                                                              ImGui.SameLine(150 * GlobalFontScale);
                                                              configChanged |= ImGui.Checkbox("##firstExploration", ref Configuration.SendOnFirstExploration);
                                                          }
                                                      }
                                                  });
            if (!Configuration.UseMaxRetainerAmount)
            {
                if (RetainerManager.Instance()->MaxRetainerEntitlement != 0)
                    DrawCentered("##RetainerVocateNameList", () => DrawRetainerVocateTable());
            }
            else
            {
                DrawCentered("##RetainerVocateRandomizeDetails", () =>
                                                                 {
                                                                     ImGui.Text("Fill all retainer slots");
                                                                     ImGui.SameLine(150 * GlobalFontScale);
                                                                     configChanged |= ImGui.Checkbox("##fillAllSlots", ref Configuration.UseMaxRetainerAmount);
                                                                     ImGui.Text("Race");
                                                                     ImGui.SameLine(150          * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= ImGuiEx.EnumCombo("##retainerRace", ref Configuration.RetainerRace);
                                                                     ImGui.Text("Gender");
                                                                     ImGui.SameLine(150          * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= ImGuiEx.EnumCombo("##retainerGender", ref Configuration.RetainerGender);
                                                                     ImGui.Text("Personality");
                                                                     ImGui.SameLine(150          * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     configChanged |= ImGuiEx.EnumCombo("##retainerPersonality", ref Configuration.RetainerPersonality);

                                                                     ImGui.Text("Retainer Class");
                                                                     ImGui.SameLine(150          * GlobalFontScale);
                                                                     ImGui.SetNextItemWidth(120f * GlobalFontScale);
                                                                     if (ImGuiEx.ExcelSheetCombo<ClassJob>("##retainerJob", out var selected, s => s.GetRowOrDefault(Configuration.RetainerClass) is { } row
                                                                                                                                                           ? s.GetRow(Configuration.RetainerClass)
                                                                                                                                                              .Abbreviation.ExtractText()
                                                                                                                                                           : string.Empty, x => x.Abbreviation.ExtractText(),
                                                                                                           x => x.RowId is >= 1 and <= 7 or >= 16 and <= 18 or 26))
                                                                     {
                                                                         Configuration.RetainerClass = selected.RowId;
                                                                         configChanged   = true;
                                                                     }

                                                                     ImGui.Text("Assign Exploration");
                                                                     ImGui.SameLine(150 * GlobalFontScale);
                                                                     configChanged |= ImGui.Checkbox("##firstExploration", ref Configuration.SendOnFirstExploration);
                                                                 });
            }

            ImGui.Separator();

            ImGui.NewLine();

            DrawCentered("##RetainerVocateQstClass", () =>
                                                     {
                                                         ImGui.Text("Class/Job for Quest:");
                                                         ImGui.SameLine(150          * GlobalFontScale);
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

            if (ImGui.CollapsingHeader("Single Backup Tasks##singleTasks"))
            {
                if (RetainerManager.Instance()->MaxRetainerEntitlement                                                  == 0 ||
                    RetainerManager.Instance()->MaxRetainerEntitlement - RetainerManager.Instance()->GetRetainerCount() > 0)
                {
                    if (ImGui.Button(Configuration.UseMaxRetainerAmount                                  ? "Create Retainers" :
                                     RetainerManager.Instance()->MaxRetainerEntitlement == 0 ? "Go To Vocate" : "Create Retainers") &&
                        !Utils.IsPluginBusy)
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
                    ImGuiEx.Text(EzColor.Red, "You can not create more Retainers on this character!");

                if (!QuestManager.IsQuestComplete(66968) && !QuestManager.IsQuestComplete(66969) && !QuestManager.IsQuestComplete(66970))
                {
                    var classJob = Svc.Data.GetExcelSheet<ClassJob>()
                                      .GetRow(Configuration.QstClassJob);
                    var gearset = GetFirstGearsetForClassJob(classJob);
                    ImGui.NewLine();
                    ImGui.Text("Questionable:");
                    ImGui.Text("An Ill-conceived Venture");
                    if (gearset == null)
                        ImGuiEx.Text(EzColor.Red, "You have no gearset registered for your chosen class.");
                    else if (ImGui.Button("Run Quest") && !Questionable.IsRunning() && !Utils.IsPluginBusy)
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

                if (ImGui.Button("Assign Class"))
                {
                    if (RetainerManager.Instance()->MaxRetainerEntitlement == 0)
                        EnqueueTask(new TaskRecord(Feature.GoToRetainerVocate, "Go to Retainer Vocate"));
                    EnqueueTask(new TaskRecord(token => Feature.BuyAndEquipRetainerGear(token, Configuration.UseMaxRetainerAmount
                                                                                                       ? 10
                                                                                                       : (uint)Configuration.RetainerAmount + 1, Configuration.UseMaxRetainerAmount ? Configuration.RetainerClass : 0), "Buy and Equip Retainer Gear"));
                }
            }
        }

        if (configChanged) SaveConfig(Configuration);
    }

    private void DrawRetainerVocateTable()
    {
        var table = new Table<RetainerCharacter>(
                                      "##RetainerVocateTable",
                                      new List<TableColumn<RetainerCharacter>>
                                      {
                                              new("Name", Width: 160, DrawCustom: (retainer, index) =>
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
                                              new("Race", Width: 110, DrawCustom: (retainer, index) =>
                                                                                 {
                                                                                     ImGui.SetNextItemWidth(110f);
                                                                                     configChanged |= ImGuiEx.EnumCombo($"##retainerRace{index}", ref retainer.Race);
                                                                                 }),
                                              new("Gender", Width: 110, DrawCustom: (retainer, index) =>
                                                                                 {
                                                                                     ImGui.SetNextItemWidth(110f);
                                                                                     configChanged |= ImGuiEx.EnumCombo($"##retainerGender{index}", ref retainer.Gender);
                                                                                 }),
                                              new("Personality", Width: 110, DrawCustom: (retainer, index) =>
                                                                                   {
                                                                                       ImGui.SetNextItemWidth(110f);
                                                                                       configChanged |= ImGuiEx.EnumCombo($"##retainerPersonality{index}", ref retainer.Personality);
                                                                                   }),
                                              new("Class", Width: 70, DrawCustom: (retainer, index) =>
                                                                                  {
                                                                                      ImGui.SetNextItemWidth(70f);
                                                                                      if (ImGuiEx.ExcelSheetCombo<ClassJob>($"##retainerJob{index}", out var selected, s => s.GetRowOrDefault(retainer.Class) is { } row
                                                                                                                                                                            ? s.GetRow(retainer.Class)
                                                                                                                                                                               .Abbreviation.ExtractText()
                                                                                                                                                                            : string.Empty, x => x.Abbreviation.ExtractText(),
                                                                                                                            x => x.RowId is >= 1 and <= 7 or >= 16 and <= 18 or 26))
                                                                                      {
                                                                                          retainer.Class       = selected.RowId;
                                                                                          configChanged = true;
                                                                                      }
                                                                                  })
                                      },
                                      () => Configuration.RetainerCharacters,
                                      Configuration.RetainerAmount + 1,
                                      size: new Vector2(600, 27 + ((Configuration.RetainerAmount + 1) * 27))
                                     );

        table.Draw();
    }
}
