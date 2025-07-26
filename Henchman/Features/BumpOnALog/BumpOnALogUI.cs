using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Helpers;
using Henchman.Models;
using Henchman.TaskManager;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System.Linq;
using Action = System.Action;
using GrandCompany = Lumina.Excel.Sheets.GrandCompany;

namespace Henchman.Features.BumpOnALog;

[Feature]
public class BumpOnALogUi : FeatureUI
{
    private readonly BumpOnALog feature = new();
    private int classMonsterNoteId;
    private MonsterNoteRankInfo classMonsterNoteRankInfo;
    private int currentClassRank;
    private int currentGcRank;
    private int gcMonsterNoteId;
    private MonsterNoteRankInfo gcMonsterNoteRankInfo;
    public override string Name => "Bump On A Log";

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

        gcMonsterNoteId = Svc.Data.GetExcelSheet<GrandCompany>()
                             .GetRow(PlayerState.Instance()->GrandCompany)
                             .Unknown8;

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
        currentClassRank = classMonsterNoteRankInfo.Rank;

        ImGuiEx.TextCentered($"Current Rank: {currentClassRank + 1}");

        DrawHuntLog(classMonsterNoteRankInfo, ClassHuntRanks[(uint)classMonsterNoteId].HuntMarks, false);
    }

    private unsafe void DrawGcHuntLog()
    {
        gcMonsterNoteId = Svc.Data.Excel.GetSheet<GrandCompany>()
                             .GetRow(PlayerState.Instance()->GrandCompany)
                             .Unknown8;

        if (gcMonsterNoteId == 127)
        {
            ImGuiEx.TextCentered(ImGuiColors.DalamudRed, "You are not in any GrandCompany!");
            return;
        }

        var gcRow = Svc.Data.Excel.GetSheet<GrandCompany>()
                       .GetRow(PlayerState.Instance()->GrandCompany);
        ImGuiEx.TextCentered(gcRow.Name.ExtractText());

        gcMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[gcMonsterNoteId];
        currentGcRank = gcMonsterNoteRankInfo.Rank;

        ImGuiEx.TextCentered($"Current Rank: {currentGcRank + 1}");

        if (currentGcRank > 2)
        {
            ImGuiEx.TextCentered(ImGuiColors.HealerGreen, "You finished all hunt log ranks for your grand company!");
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
}
