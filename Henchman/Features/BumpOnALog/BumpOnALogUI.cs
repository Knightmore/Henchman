using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Helpers;
using Henchman.Models;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;
using Action = System.Action;
using GrandCompany = Lumina.Excel.Sheets.GrandCompany;

namespace Henchman.Features.BumpOnALog;

[Feature]
public class BumpOnALogUi : FeatureUI
{
    private readonly BumpOnALog          feature = new();
    private          int                 classMonsterNoteId;
    private          MonsterNoteRankInfo classMonsterNoteRankInfo;
    private          int                 currentClassLogRank;
    private          int                 currentGcLogRank;
    private          int                 gcMonsterNoteId;
    private          MonsterNoteRankInfo gcMonsterNoteRankInfo;
    public override  string              Name => "Bump On A Log";

    public override Action Help => () =>
                                   {
                                       ImGui.Text(
                                                  """
                                                  Click the "Start" button for your wanted Hunt Log.
                                                  BumpOnALog will complete all (only non-Duty for now) current mob entries for your rank. 
                                                  """);

                                       ImGuiHelper.DrawRequirements(Requirements);
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
        ImGuiEx.TextCentered(classJobRow.NameEnglish.ExtractText());

        classMonsterNoteId = classJobRow.MonsterNote.RowId.ToInt();
        if (classMonsterNoteId is -1 or 127)
        {
            ImGuiEx.TextCentered(ImGuiColors.DalamudRed, "There is no hunt log for your class!");
            return;
        }

        classMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[classMonsterNoteId];
        currentClassLogRank      = classMonsterNoteRankInfo.Rank;

        ImGuiEx.TextCentered($"Current Log Rank: {currentClassLogRank + 1}");

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
        ImGuiEx.TextCentered(gcRow.Name.ExtractText());

        gcMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[gcMonsterNoteId];
        currentGcLogRank      = gcMonsterNoteRankInfo.Rank;

        ImGuiEx.TextCentered($"Current Log Rank: {currentGcLogRank + 1}");

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

        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail()
                                  .X /
                             2) -
                            10);
        if (ImGui.Button("Start"))
        {
            EnqueueTask(gcLog
                                ? new TaskRecord(feature.StartGCRank, "Bump On A Log - GC Log")
                                : new TaskRecord(feature.StartClassRank, "Bump On A Log - Rank Log"));
        }

        var huntMarksArray = Enumerable.Range(0, huntMarks.GetLength(1))
                                       .Select(col => huntMarks[rankInfo.Rank, col])
                                       .Where(mark => mark != null)
                                       .ToArray();


        using (var table = ImRaii.Table("###HuntRankTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Kills", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Finished", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();

            foreach (var huntMark in huntMarksArray)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.Text(Utils.ToTitleCaseExtended(huntMark?.Name, Svc.ClientState.ClientLanguage));
                ImGui.TableNextColumn();
                if (huntMark != null)
                {
                    ImGuiEx.TextCentered($"{huntMark.GetCurrentMonsterNoteKills}/{huntMark.NeededKills}");
                    ImGui.TableNextColumn();
                    var markFinished = huntMark.GetOpenMonsterNoteKills == 0;
                    FontAwesome.Print(markFinished
                                              ? ImGuiColors.HealerGreen
                                              : ImGuiColors.DalamudRed,
                                      markFinished
                                              ? FontAwesome.Check
                                              : FontAwesome.Cross);
                }
            }
        }
    }

    private void DrawSettings()
    {
        var configChanged = false;

        // Preparation if Dzemael and Aurum ever gets Duty support (in 7.5?)
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
