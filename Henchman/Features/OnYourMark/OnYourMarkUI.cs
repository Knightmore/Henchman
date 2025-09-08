using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Helpers;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.OnYourMark;

[Feature]
public class OnYourMarkUi : FeatureUI
{
    private readonly OnYourMark feature = new();
    public override  string     Name => "On Your Mark";

    public override Action Help => () =>
                                   {
                                       ImGui.Text("""
                                                  Tick the Enable checkboxes for your wanted bills and click the 'Start' button.
                                                  OnYourMark will gather all enabled Hunt Bills and finish all marks.

                                                  Bill colors:
                                                  """);
                                       ImGuiEx.Text(ImGuiColors.DalamudGrey, "Grey");
                                       ImGui.SameLine(90);
                                       ImGui.Text("-   Not unlocked.");
                                       ImGuiEx.Text(ImGuiColors.DalamudRed, "Red");
                                       ImGui.SameLine(90);
                                       ImGui.Text("-   Not obtained.");
                                       ImGuiEx.Text(ImGuiColors.DalamudYellow, "Yellow");
                                       ImGui.SameLine(90);
                                       ImGui.Text("-   Obtained uncompleted.");
                                       ImGuiEx.Text(ImGuiColors.DalamudOrange, "Orange");
                                       ImGui.SameLine(90);
                                       ImGui.Text("-   Obtained uncompleted old mark.");
                                       ImGuiEx.Text(ImGuiColors.HealerGreen, "Green");
                                       ImGui.SameLine(90);
                                       ImGui.Text("-   Completed.");

                                       ImGuiHelper.DrawRequirements(Requirements);
                                   };

    public override bool LoginNeeded => true;
    public override Window.WindowSizeConstraints SizeConstraints { get; } = new Window.WindowSizeConstraints
                                                                            {
                                                                                    MinimumSize = new Vector2(700, 500),
                                                                                    MaximumSize = new Vector2(700, 1000)
                                                                            };
    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.BossMod, false),
            (IPCNames.Wrath, false),
            (IPCNames.RotationSolverReborn, false)
    ];

    public override unsafe void Draw()
    {
        var configChanged = false;
        var groupedCategories = BillCategories
               .GroupBy(kvp => new string(kvp.Key.TakeWhile(c => char.IsLetter(c) || c == ' ')
                                             .ToArray()));

        var mobHuntOrderTypeEnumerator = Svc.Data.GetExcelSheet<MobHuntOrderType>()
                                            .GetEnumerator();

        ImGuiEx.LineCentered("###Start", () =>
                                         {
                                             if (ImGui.Button("Start") && !IsTaskEnqueued(Name))
                                                 EnqueueTask(new TaskRecord(feature.Start, Name));
                                         });

        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs)
        {
            foreach (var group in groupedCategories)
            {
                using (var tab = ImRaii.TabItem(group.Key))
                {
                    if (tab)
                    {
                        var indexInEnumerator = HuntBoardOptions
                                               .Select((key, index) => new { key, index })
                                               .FirstOrDefault(x => x.key.Contains(group.Key))
                                              ?.index;

                        for (var i = 0; i <= indexInEnumerator; i++)
                            mobHuntOrderTypeEnumerator.MoveNext();

                        ImGui.Spacing();

                        foreach (var (key, title) in group)
                        {
                            var currentMobHuntType = mobHuntOrderTypeEnumerator.Current;
                            var isMarkBillObtained = MobHunt.Instance()->IsMarkBillObtained(GetTranslatedMobHuntOrderType(currentMobHuntType.RowId));
                            var availableMarkId    = MobHunt.Instance()->GetAvailableHuntOrderRowId((byte)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId));
                            var obtainedMarkId     = MobHunt.Instance()->GetObtainedHuntOrderRowId((byte)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId));
                            var mobHuntOrderTypeOffset = isMarkBillObtained && availableMarkId != obtainedMarkId
                                                                 ? MobHunt.Instance()->ObtainedMarkId.ToArray()
                                                                 : MobHunt.Instance()->AvailableMarkId.ToArray();
                            var mobHuntTargets =
                                    Svc.Data.Excel.GetSubrowSheet<MobHuntOrder>()
                                            [Svc.Data.Excel.GetSheet<MobHuntOrderType>()
                                                .GetRow((uint)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId))
                                                .OrderStart.Value.RowId +
                                             ((uint)mobHuntOrderTypeOffset[GetTranslatedMobHuntOrderType(currentMobHuntType.RowId)] - 1)];
                            var billAmountFinished =
                                    (uint)mobHuntTargets.Count(mark
                                                                       => MobHunt
                                                                                         .Instance()->
                                                                                  GetKillCount((byte)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId),
                                                                                               (byte)mark.SubrowId) ==
                                                                          mark.NeededKills);

                            if (!MobHunt.Instance()->IsMarkBillUnlocked((byte)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId)))
                            {
                                ImGuiEx.TextCentered(ImGuiColors.DalamudGrey, $"{title}");
                                continue;
                            }


                            var allMobsKilled =
                                    mobHuntTargets.All(x => MobHunt.Instance()->GetKillCount((byte)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId),
                                                                                             (byte)x.SubrowId) ==
                                                            x.NeededKills);

                            if ((!isMarkBillObtained && !allMobsKilled) || (obtainedMarkId != availableMarkId && !isMarkBillObtained && allMobsKilled))
                                ImGuiEx.TextCentered(ImGuiColors.DalamudRed, $"{title}");
                            else if (obtainedMarkId == availableMarkId && isMarkBillObtained)
                                ImGuiEx.TextCentered(ImGuiColors.DalamudYellow, $"{title} {billAmountFinished}/{mobHuntTargets.Count}");
                            else if (obtainedMarkId != availableMarkId && isMarkBillObtained)
                                ImGuiEx.TextCentered(ImGuiColors.ParsedOrange, $"{title} {billAmountFinished}/{mobHuntTargets.Count}");
                            else if ((obtainedMarkId == availableMarkId || !isMarkBillObtained) && allMobsKilled)
                                ImGuiEx.TextCentered(ImGuiColors.HealerGreen, $"{title} {billAmountFinished}/{mobHuntTargets.Count}");

                            ImGui.Spacing();

                            var enabled = C.EnableHuntBills[key];
                            if (ImGui.Checkbox($"Enable##{key}", ref enabled))
                            {
                                C.EnableHuntBills[key] = enabled;
                                configChanged          = true;
                            }

                            ImGui.Spacing();
                            if (!isMarkBillObtained)
                            {
                                mobHuntOrderTypeEnumerator.MoveNext();
                                ImGui.Spacing();
                                ImGui.Separator();
                                ImGui.Spacing();
                                continue;
                            }


                            using (var table = ImRaii.Table($"###{key}BillTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
                            {
                                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                                ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("Finished", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableHeadersRow();

                                foreach (var mark in mobHuntTargets)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    ImGuiEx.Text(Utils.ToTitleCaseExtended(mark.Target.Value.Name.Value.Singular, Svc.ClientState.ClientLanguage));
                                    ImGui.TableNextColumn();
                                    ImGuiEx.TextCentered($"{MobHunt.Instance()->GetKillCount((byte)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId), (byte)mark.SubrowId)}/{mark.NeededKills}");
                                    ImGui.TableNextColumn();
                                    var markFinished =
                                            MobHunt.Instance()->GetKillCount((byte)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId),
                                                                             (byte)mark.SubrowId) ==
                                            mark.NeededKills;
                                    FontAwesome.Print(markFinished
                                                              ? ImGuiColors.HealerGreen
                                                              : ImGuiColors.DalamudRed,
                                                      markFinished
                                                              ? FontAwesome.Check
                                                              : FontAwesome.Cross);
                                }
                            }

                            mobHuntOrderTypeEnumerator.MoveNext();
                            ImGui.Spacing();
                            ImGui.Separator();
                            ImGui.Spacing();
                        }
                    }
                }
            }

            using (var tab = ImRaii.TabItem("Settings"))
                if (tab)
                    DrawSettings();

            if (configChanged) EzConfig.Save();
        }
    }

    private void DrawSettings()
    {
        var configChanged = false;
        ImGui.Text("Discard old Hunt Bills");
        ImGui.SameLine(250);
        configChanged |= ImGui.Checkbox("##oldHuntBills", ref C.DiscardOldBills);
        ImGui.Text("Detour if an A-Rank is nearby");
        ImGui.SameLine(250);
        configChanged |= ImGui.Checkbox("##ABDetour", ref C.DetourForARanks);
        ImGui.SameLine();
        ImGuiEx.HelpMarker("""
                           Will only try a detour once per Mark.
                           If your char dies while taking a detour, it will resume to find the original mark.
                           As a safety measure, this will only work up until Stormblood.
                           """);
        ImGui.Text("Skip Fate Marks");
        ImGui.SameLine(250);
        configChanged |= ImGui.Checkbox("##skipFateMarks", ref C.SkipFateMarks);
        if (configChanged) EzConfig.Save();
    }
}
