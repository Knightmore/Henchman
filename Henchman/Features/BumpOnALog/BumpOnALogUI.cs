using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Abstractions;
using Henchman.Models;
using Henchman.TaskManager;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using System.Linq;
using Action = System.Action;
using GrandCompany = Lumina.Excel.Sheets.GrandCompany;

namespace Henchman.Features.BumpOnALog;

[Feature]
public class BumpOnALogUI : FeatureUI<BumpOnALog, Configuration>
{
    internal readonly BumpOnALog Feature = new();
    private int classMonsterNoteId;
    private MonsterNoteRankInfo classMonsterNoteRankInfo;
    private int currentClassLogRank;
    private int currentGcLogRank;
    private int gcMonsterNoteId;
    private MonsterNoteRankInfo gcMonsterNoteRankInfo;

    public BumpOnALogUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public override string Name => "Bump On A Log";
    public override Category Category => Category.Combat;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.List;


    public override Action Help => () =>
                                   {
                                       ImGui.Text(
                                                  """
                                                  Click the "Start" button for your wanted Hunt Log.
                                                  BumpOnALog will complete all current mob entries for your rank. 

                                                  If your character has enough GrandCompany seals and the appropriate level,
                                                  'Bump On A Log' will progress through your GrankCompany ranks up until 2nd Lieutenant.

                                                  Change your settings if you want it to stop earlier.

                                                  If you want to use multiboxing (high level carry) for GC logs,
                                                  please check and adjust your general Multiboxing settings.
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

    public sealed override required Configuration Configuration { get; init; }

    public override void Draw()
    {
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
        currentClassLogRank = classMonsterNoteRankInfo.Rank;

        Layout.DrawInfoBox(() =>
                           {
                               if (StartButton() && !IsTaskEnqueued(Name))
                               {
                                   EnqueueTask(new TaskRecord(Feature.StartClassRank, "Bump On A Log - Rank Log", onDone: () =>
                                                                                                                          {
                                                                                                                              Bossmod.DisableAI();
                                                                                                                              AutoRotation.Disable();
                                                                                                                              ResetCurrentTarget();
                                                                                                                          }, onAbort: () =>
                                                                                                                                      {
                                                                                                                                          Bossmod.DisableAI();
                                                                                                                                          AutoRotation.Disable();
                                                                                                                                          ResetCurrentTarget();
                                                                                                                                      }));
                               }
                           },
                           () =>
                           {
                               ImGui.Text(classJobRow.NameEnglish.ExtractText());
                               ImGui.SameLine();

                               using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary)) ImGui.Text($"- Current Difficulty: {currentClassLogRank + 1}");
                           });

        ImGui.Spacing();

        DrawHuntLog(classMonsterNoteRankInfo, ClassHuntRanks[(uint)classMonsterNoteId].HuntMarks, false);
    }

    private unsafe void DrawGcHuntLog()
    {
        gcMonsterNoteId = (int)Svc.Data.GetExcelSheet<GrandCompany>()
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
        currentGcLogRank = gcMonsterNoteRankInfo.Rank;

        Layout.DrawInfoBox(() =>
                           {
                               if (StartButton() && !IsTaskEnqueued(Name))
                               {
                                   EnqueueTask(new TaskRecord(token => Feature.StartGCRank(token), "Bump On A Log - GC Log", onDone: () =>
                                                                                                                                     {
                                                                                                                                         Bossmod.DisableAI();
                                                                                                                                         AutoRotation.Disable();
                                                                                                                                         ResetCurrentTarget();
                                                                                                                                     }, onAbort: () =>
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

                                  using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary)) ImGui.Text($"- Current Rank: {GetGrandCompanyRank()} {GetGCRankTitle()} - Difficulty: {currentGcLogRank}");
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
                                                new("Name", h => ToTitleCaseExtended(h.Name, Svc.ClientState.ClientLanguage)),
                                                new("Kills", h => $"{h.GetCurrentMonsterNoteKills}/{h.NeededKills}", 100, Alignment: ColumnAlignment.Center),
                                                new("Finished", h => h.GetOpenMonsterNoteKills == 0
                                                                             ? FontAwesomeIcon.Check.ToIconString()
                                                                             : FontAwesomeIcon.Times.ToIconString(), 100, Alignment: ColumnAlignment.Center,
                                                    GetTextColor: h => h.GetOpenMonsterNoteKills == 0
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

        DrawCentered(() => { });
        ImGui.Text("Stop after Job Log Rank");
        ImGui.SameLine(200);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGui.Combo("##jobRank", ref Configuration.StopAfterJobRank, Enumerable.Range(1, 5)
                                                                                                .Select(x => x.ToString())
                                                                                                .ToArray(), 5);

        ImGui.Text("Stop after GC Rank");
        ImGui.SameLine(200);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGui.Combo("##gcRank", ref Configuration.StopAfterGCRank, Enumerable.Range(1, 9)
                                                                                              .Select(x => x.ToString())
                                                                                              .ToArray(), 9);

        ImGui.Text("Order Mobs by Territory");
        ImGui.SameLine(200);
        configChanged |= ImGui.Checkbox("##orderByTerritory", ref Configuration.OrderByTerritory);

        ImGui.Text("Skip Duty Marks");
        ImGui.SameLine(200);
        configChanged |= ImGui.Checkbox("##skipDutyMarks", ref Configuration.SkipDutyMarks);

        ImGui.Text("Solo Unsync Duty");
        ImGui.SameLine(200);
        configChanged |= ImGui.Checkbox("##soloUnsyncDuty", ref C.SoloUnsyncLogDuty);

        ImGui.Text("Rank Up GC");
        ImGui.SameLine(200);
        configChanged |= ImGui.Checkbox("##autoGCRankUp", ref Configuration.AutoGCRankUp);


        if (configChanged)
        {
            EzConfig.Save();
            SaveConfig(Configuration);
        }
    }

    public unsafe string GetGCRankTitle()
    {
        var playerState = PlayerState.Instance();
        var playerSex = playerState->Sex;
        var playerGC = playerState->GrandCompany;
        switch (playerGC)
        {
            case 1:
                if (playerSex == 0)
                {
                    return Svc.Data.GetExcelSheet<GCRankLimsaMaleText>()
                              .GetRow(playerState->GCRanks[0])
                              .Singular.ExtractText();
                }

                return Svc.Data.GetExcelSheet<GCRankLimsaFemaleText>()
                          .GetRow(playerState->GCRanks[0])
                          .Singular.ExtractText();
            case 2:
                if (playerSex == 0)
                {
                    return Svc.Data.GetExcelSheet<GCRankGridaniaMaleText>()
                              .GetRow(playerState->GCRanks[1])
                              .Singular.ExtractText();
                }

                return Svc.Data.GetExcelSheet<GCRankGridaniaFemaleText>()
                          .GetRow(playerState->GCRanks[1])
                          .Singular.ExtractText();
            case 3:
                if (playerSex == 0)
                {
                    return Svc.Data.GetExcelSheet<GCRankUldahMaleText>()
                              .GetRow(playerState->GCRanks[2])
                              .Singular.ExtractText();
                }

                return Svc.Data.GetExcelSheet<GCRankUldahFemaleText>()
                          .GetRow(playerState->GCRanks[2])
                          .Singular.ExtractText();
            default:
                return "None";
        }
    }
}
