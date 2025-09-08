using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using Henchman.Helpers;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.RetainerVocate;

[Feature]
public class RetainerVocateUI : FeatureUI
{
    private readonly RetainerVocate feature = new();
    public override   string         Name => "Retainer Vocate";

    public override Action Help => () =>
                                   {
                                       ImGui.Text("""
                                                  Set up how you want your retainer to be and click 'Create Retainers'
                                                  If you haven't already done the Venture Quest, it will run after creating retainers.

                                                  If any task fails or you somehow messed up midway, 
                                                  you can try to continue through one of the 'Single Backup Tasks' 
                                                  """);

                                       ImGuiHelper.DrawRequirements(Requirements);
                                   };

    public override bool LoginNeeded => true;

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.Questionable, true)
    ];
    public override Window.WindowSizeConstraints SizeConstraints { get; } = new Window.WindowSizeConstraints
                                                                            {
                                                                                    MinimumSize = new Vector2(400, 500),
                                                                                    MaximumSize = new Vector2(400, 500)
                                                                            };
    public override unsafe void Draw()
    {
        var configChanged = false;

        if (!QuestManager.IsQuestComplete(66196))
            ImGuiEx.Text(EzColor.Red, "Retainers are not unlocked. Proceed with MSQ and finish \"The Scions of the Seventh Dawn\".");
        else
        {
            ImGuiEx.LineCentered("###Start", () =>
                                             {
                                                 if (ImGui.Button("Create Retainers") && !IsTaskEnqueued(Name))
                                                 {
                                                     EnqueueTask(new TaskRecord((token) => feature.RunFullCreation(token, C.UseMaxRetainerAmount
                                                                                                                                  ? 10
                                                                                                                                  : (uint)C.RetainerAmount + 1, C.RetainerClass, C.QstClassJob), Name));
                                                 }
                                             });


            ImGui.Text("Fill all retainer slots");
            ImGui.SameLine(150);
            configChanged |= ImGui.Checkbox("##fillAllSlots", ref C.UseMaxRetainerAmount);
            if (!C.UseMaxRetainerAmount)
            {
                ImGui.Text("Retainer amount");

                if (RetainerManager.Instance()->MaxRetainerEntitlement == 0)
                {
                    ImGuiEx.Text(EzColor.Red, "Could not read the maximum allowed amount of retainers on your account.");
                    ImGuiEx.Text(EzColor.Red, "Please interact with a \"Retainer Vocate\" to progress.");
                }
                else
                {
                    ImGui.SameLine(150);
                    ImGui.SetNextItemWidth(120f);
                    configChanged |= ImGui.Combo("##retainerAmount", ref C.RetainerAmount, Enumerable.Range(1, 10)
                                                                                                     .Select(x => x.ToString())
                                                                                                     .ToArray(), 10);
                }
            }

            ImGui.Text("City");
            ImGui.SameLine(150);
            ImGui.SetNextItemWidth(120f);
            configChanged |= ImGuiEx.EnumCombo("##retainerCity", ref C.RetainerCity);
            ImGui.Text("Race");
            ImGui.SameLine(150);
            ImGui.SetNextItemWidth(120f);
            configChanged |= ImGuiEx.EnumCombo("##retainerRace", ref C.RetainerRace);
            ImGui.Text("Gender");
            ImGui.SameLine(150);
            ImGui.SetNextItemWidth(120f);
            configChanged |= ImGuiEx.EnumCombo("##retainerGender", ref C.RetainerGender);
            ImGui.Text("Personality");
            ImGui.SameLine(150);
            ImGui.SetNextItemWidth(120f);
            configChanged |= ImGuiEx.EnumCombo("##retainerPersonality", ref C.RetainerPersonality);

            ImGui.Text("Retainer Class");
            ImGui.SameLine(150);
            ImGui.SetNextItemWidth(120f);
            if (ImGuiEx.ExcelSheetCombo<ClassJob>("##retainerJob", out var selected, s => s.GetRowOrDefault(C.RetainerClass) is { } row
                                                                                                  ? s.GetRow(C.RetainerClass)
                                                                                                     .Abbreviation.ExtractText()
                                                                                                  : string.Empty, x => x.Abbreviation.ExtractText(),
                                                  x => x.RowId is >= 1 and <= 7 or >= 16 and <= 18 or 26 or 29))
            {
                C.RetainerClass = selected.RowId;
                configChanged   = true;
            }

            ImGui.Text("Assign Exploration");
            ImGui.SameLine(150);
            configChanged |= ImGui.Checkbox("##firstExploration", ref C.SendOnFirstExploration);

            ImGui.Separator();

            ImGui.NewLine();
            ImGui.Text("Class/Job for Quest:");
            ImGui.SameLine(150);
            ImGui.SetNextItemWidth(120f);
            if (ImGuiEx.ExcelSheetCombo<ClassJob>("##qstCombatJob", out var classJobSheet, s => s.GetRowOrDefault(C.QstClassJob) is { } row
                                                                                                        ? s.GetRow(C.QstClassJob)
                                                                                                           .Abbreviation.ExtractText()
                                                                                                        : string.Empty,
                                                  x => x.Abbreviation.ExtractText(), x => x.RowId is >= 1 and <= 7 or >= 19 and <= 42))
            {
                C.QstClassJob = classJobSheet.RowId;
                configChanged = true;
            }

            if (ImGui.CollapsingHeader("Single Backup Tasks##singleTasks"))
            {
                if (RetainerManager.Instance()->MaxRetainerEntitlement                                                  == 0 ||
                    RetainerManager.Instance()->MaxRetainerEntitlement - RetainerManager.Instance()->GetRetainerCount() > 0)
                {
                    if (ImGui.Button(C.UseMaxRetainerAmount                                  ? "Create Retainers" :
                                     RetainerManager.Instance()->MaxRetainerEntitlement == 0 ? "Go To Vocate" : "Create Retainers") &&
                        !Utils.IsPluginBusy)
                    {
                        EnqueueTask(new TaskRecord(feature.GoToRetainerVocate, "Go to Retainer Vocate"));
                        if (C.UseMaxRetainerAmount || RetainerManager.Instance()->MaxRetainerEntitlement != 0)
                        {
                            EnqueueTask(new TaskRecord(token => feature.CreateRetainers(token, C.UseMaxRetainerAmount
                                                                                                       ? 10
                                                                                                       : C.RetainerAmount + 1), "Create Retainers"));
                        }
                    }
                }
                else
                    ImGuiEx.Text(EzColor.Red, "You can not create more Retainers on this character!");

                if (!QuestManager.IsQuestComplete(66968) && !QuestManager.IsQuestComplete(66969) && !QuestManager.IsQuestComplete(66970))
                {
                    var classJob = Svc.Data.GetExcelSheet<ClassJob>()
                                      .GetRow(C.QstClassJob);
                    var gearset = GetGearsetForClassJob(classJob);
                    ImGui.NewLine();
                    ImGui.Text("Questionable:");
                    ImGui.Text("An Ill-conceived Venture");
                    if (gearset == null)
                        ImGuiEx.Text(EzColor.Red, "You have no gearset registered for your chosen class.");
                    else if (ImGui.Button("Run Quest") && !Questionable.IsRunning() && !Utils.IsPluginBusy)
                    {
                        Chat.Instance.SendMessage($"/gearset change {gearset.Value + 1}");
                        if (!SubscriptionManager.IsInitialized(IPCNames.Questionable))
                        {
                            FullError("'Questionable' not available. Skipping Venture Quest and equipping Retainers.");
                            return;
                        }

                        EnqueueTask(new TaskRecord(token => feature.StartVentureQuest(token, C.QstClassJob), "Do Retainer Venture Quest"));
                    }
                }

                if (ImGui.Button("Assign Class"))
                {
                    if (RetainerManager.Instance()->MaxRetainerEntitlement == 0)
                        EnqueueTask(new TaskRecord(feature.GoToRetainerVocate, "Go to Retainer Vocate"));
                    EnqueueTask(new TaskRecord(token => feature.BuyAndEquipRetainerGear(token, C.UseMaxRetainerAmount
                                                                                                       ? 10
                                                                                                       : (uint)C.RetainerAmount + 1, C.RetainerClass), "Buy and Equip Retainer Gear"));
                }
            }
        }

        if (configChanged) EzConfig.Save();
    }
}
