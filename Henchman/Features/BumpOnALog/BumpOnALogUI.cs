using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Helpers;
using Henchman.Models;
using Henchman.TaskManager;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using Action = System.Action;
using GrandCompany = Lumina.Excel.Sheets.GrandCompany;

namespace Henchman.Features.BumpOnALog;

[Feature]
public class BumpOnALogUi : FeatureUI
{
    internal readonly BumpOnALog          feature = new();
    private           int                 classMonsterNoteId;
    private           MonsterNoteRankInfo classMonsterNoteRankInfo;
    private           int                 currentClassLogRank;
    private           int                 currentGcLogRank;
    private           int                 gcMonsterNoteId;
    private           MonsterNoteRankInfo gcMonsterNoteRankInfo;
    public override   string              Name     => "Bump On A Log";
    public override   string              Category => Henchman.Category.Combat;
    public override   FontAwesomeIcon     Icon     => FontAwesomeIcon.List;

    public override Action Help => () =>
                                   {
                                       ImGui.Text(
                                                  """
                                                  Click the "Start" button for your wanted Hunt Log.
                                                  BumpOnALog will complete all (only non-Duty for now) current mob entries for your rank. 
                                                  """);

                                       DrawRequirements(Requirements);
                                   };

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.AutoDuty, false),
            (IPCNames.Questionable, false),
            (IPCNames.BossMod, false),
            (IPCNames.Wrath, false),
            (IPCNames.RotationSolverReborn, false)
    ];

    public override bool LoginNeeded => true;

    public override unsafe void Draw()
    {
        classMonsterNoteId = Svc.Data.GetExcelSheet<ClassJob>()
                                .GetRow(PlayerState.Instance()->CurrentClassJobId)
                                .MonsterNote.RowId.ToInt();

        gcMonsterNoteId = (int)Svc.Data.GetExcelSheet<GrandCompany>()
                                  .GetRow(PlayerState.Instance()->GrandCompany)
                                  .MonsterNote.RowId;

        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs)
        {
            using (var tab = ImRaii.TabItem("Class"))
            {
                if (tab)
                    DrawJobHuntLog();
            }

            using (var tab = ImRaii.TabItem("Grand Company"))
            {
                if (tab)
                    DrawGcHuntLog();
            }


            using (var tab = ImRaii.TabItem("Settings"))
            {
                if (tab)
                    DrawSettings();
            }
        }
    }

    private unsafe void DrawJobHuntLog()
    {
        var classJobRow = Svc.Data.Excel.GetSheet<ClassJob>()
                             .GetRow(PlayerState.Instance()->CurrentClassJobId);

        classMonsterNoteId = classJobRow.MonsterNote.RowId.ToInt();
        if (classMonsterNoteId is -1 or 127)
        {
            ImGuiEx.TextCentered(ImGuiColors.DalamudRed, "There is no hunt log for your class!");
            return;
        }

        classMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[classMonsterNoteId];
        currentClassLogRank      = classMonsterNoteRankInfo.Rank;

        Layout.DrawInfoBox(() =>
                           {
                               if (StartButton() && !IsTaskEnqueued(Name)) EnqueueTask(new TaskRecord(feature.StartClassRank, "Bump On A Log - Rank Log"));
                           },
                           () =>
                           {
                               ImGui.Text(classJobRow.NameEnglish.ExtractText());
                               ImGui.SameLine();

                               using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary)) ImGui.Text($"- Current Rank: {currentClassLogRank + 1}");
                           });

        ImGui.Spacing();

        DrawHuntLog(classMonsterNoteRankInfo, ClassHuntRanks[(uint)classMonsterNoteId].HuntMarks, false);
    }

    private unsafe void DrawGcHuntLog()
    {
        gcMonsterNoteId = (int)Svc.Data.Excel.GetSheet<GrandCompany>()
                                  .GetRow(PlayerState.Instance()->GrandCompany)
                                  .MonsterNote.RowId;

        if (gcMonsterNoteId == 127)
        {
            ImGuiEx.TextCentered(ImGuiColors.DalamudRed, "You are not in any GrandCompany!");
            return;
        }

        var gcRow = Svc.Data.Excel.GetSheet<GrandCompany>()
                       .GetRow(PlayerState.Instance()->GrandCompany);

        gcMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[gcMonsterNoteId];
        currentGcLogRank      = gcMonsterNoteRankInfo.Rank;

        Layout.DrawInfoBox(() =>
                           {
                               if (StartButton() && !IsTaskEnqueued(Name))
                               {
                                   EnqueueTask(new TaskRecord(token => feature.StartGCRank(token), "Bump On A Log - GC Log", onDone: () =>
                                                                                                                                     {
                                                                                                                                         Bossmod.DisableAI();
                                                                                                                                         AutoRotation.Disable();
                                                                                                                                         ResetCurrentTarget();
                                                                                                                                     }));
                               }
                           }, () =>
                              {
                                  ImGui.Text(gcRow.Name.ExtractText());
                                  ImGui.SameLine();

                                  using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary)) ImGui.Text($"- Current Rank: {currentGcLogRank}");
                              });

        ImGui.Spacing();

        if (currentGcLogRank > 2)
        {
            ImGuiEx.TextCentered(ImGuiColors.HealerGreen, "You finished all hunt log ranks for your grand company!");
            return;
        }

        if ((currentGcLogRank == 1 && GetGrandCompanyRank() < 5) || (currentGcLogRank == 2 && GetGrandCompanyRank() < 9))
        {
            ImGuiEx.TextCentered(ImGuiColors.DalamudRed, "The required Grand Company rank has not yet been unlocked!");
            return;
        }

        DrawHuntLog(gcMonsterNoteRankInfo, GcHuntRanks[PlayerState.Instance()->GrandCompany].HuntMarks, true);
    }

    private void DrawHuntLog(MonsterNoteRankInfo rankInfo, HuntMark?[,] huntMarks, bool gcLog)
    {
        if (rankInfo.Rank > huntMarks.GetLength(0) - 1)
        {
            ImGuiEx.TextCentered(ImGuiColors.HealerGreen, "You finished all hunt log ranks!");
            return;
        }

        var huntMarksArray = Enumerable.Range(0, huntMarks.GetLength(1))
                                       .Select(col => huntMarks[rankInfo.Rank, col])
                                       .Where(mark => mark != null)
                                       .ToArray();

        DrawHuntTable(huntMarksArray);
    }

    private void DrawHuntTable(HuntMark[] marks)
    {
        var table = new Table<HuntMark>(
                                        "##HuntTable",
                                        new List<TableColumn<HuntMark>>
                                        {
                                                new("Name", h => Utils.ToTitleCaseExtended(h.Name, Svc.ClientState.ClientLanguage)),
                                                new("Kills", h => $"{h.GetCurrentMonsterNoteKills}/{h.NeededKills}", 100, ColumnAlignment.Center),
                                                new("Finished", h => h.GetOpenMonsterNoteKills == 0
                                                                             ? FontAwesomeIcon.Check.ToIconString()
                                                                             : FontAwesomeIcon.Times.ToIconString(), 100, ColumnAlignment.Center,
                                                    h => h.GetOpenMonsterNoteKills == 0
                                                                 ? Theme.SuccessGreen
                                                                 : Theme.ErrorRed)
                                        },
                                        () => marks,
                                        h => h.IsCurrentTarget
                                       );

        table.Draw();
    }

    private void DrawSettings()
    {
        var configChanged = false;

        // Preparation once Dzemael and Aurum gets Duty support (in 7.4)
        /*
        ImGui.Text("Stop after Job Log Rank");
        ImGui.SameLine(200);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGui.Combo("##jobRank", ref C.StopAfterJobRank, Enumerable.Range(1, 5)
                                                                                         .Select(x => x.ToString())
                                                                                         .ToArray(), 5);

        ImGui.Text("Stop after GC Log Rank");
        ImGui.SameLine(200);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGui.Combo("##gcRank", ref C.StopAfterGCRank, Enumerable.Range(1, 3)
                                                                                 .Select(x => x.ToString())
                                                                                 .ToArray(), 3);

        ImGui.Text("Progress GC Ranks");
        ImGui.SameLine(200);
        configChanged |= ImGui.Checkbox("##progressGCRanks", ref C.ProgressGCRanks);
        ImGui.SameLine();
        ImGuiEx.HelpMarker("""If your character has enough GrandCompany seals and the appropriate level,
        'Bump On A Log' will progress through your GrankCompany ranks up until 2nd Lieutenant""");
        */

        ImGui.Text("Skip Duty Marks");
        ImGui.SameLine(200);
        configChanged |= ImGui.Checkbox("##skipDutyFateMarks", ref C.SkipDutyMarks);


        if (configChanged) EzConfig.Save();
    }
}
