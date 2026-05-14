using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Abstractions;
using Henchman.Helpers;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using System.Linq;
using Action = System.Action;

namespace Henchman.Features.OnYourMark;

[Feature]
public class OnYourMarkUI : FeatureUI<OnYourMark, Configuration>
{
    private const uint AlliedSealsItemId = 27;
    private const uint CenturioSealsItemId = 10307;
    private const uint SacksOfNutsItemId = 26533;
    private const uint HuntCurrencyCap = 4000;

    private static readonly HuntCurrencyInfo[] HuntCurrencies =
    [
            new("Allied Seals", AlliedSealsItemId, HuntCurrencyCap),
            new("Centurio Seals", CenturioSealsItemId, HuntCurrencyCap),
            new("Sacks of Nuts", SacksOfNutsItemId, HuntCurrencyCap)
    ];

    public OnYourMarkUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public override string Name => "On Your Mark";
    public override Category Category => Category.Combat;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Bullseye;

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

                                       DrawRequirements(Requirements);
                                   };

    public override bool LoginNeeded => true;
    public sealed override required Configuration Configuration { get; init; }

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

        DrawCentered("##MarkStart", () => Layout.DrawButton(() =>
                                                            {
                                                                if (StartButton() && !IsTaskEnqueued(Name)) Feature.RunTask();
                                                            }));

        DrawHuntCurrencyOverview();

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
                            var availableMarkId = MobHunt.Instance()->GetAvailableHuntOrderRowId((byte)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId));
                            var obtainedMarkId = MobHunt.Instance()->GetObtainedHuntOrderRowId((byte)GetTranslatedMobHuntOrderType(currentMobHuntType.RowId));
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

                            var enabled = Configuration.EnableHuntBills[key];
                            if (ImGui.Checkbox($"Enable##{key}", ref enabled))
                            {
                                Configuration.EnableHuntBills[key] = enabled;
                                configChanged = true;
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
                                    ImGuiEx.Text(ToTitleCaseExtended(mark.Target.Value.Name.Value.Singular, Svc.ClientState.ClientLanguage));
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
            {
                if (tab)
                    DrawSettings();
            }

            if (configChanged) SaveConfig(Configuration);
        }
    }

    private unsafe void DrawMarkTable(SubrowCollection<MobHuntOrder> mobHunt, MobHuntOrderType currentType)
    {
        var table = new Table<MobHuntOrder>(
                                            "##HuntTable",
                                            new List<TableColumn<MobHuntOrder>>
                                            {
                                                    new("Name", h => ToTitleCaseExtended(h.Target.Value.Name.Value.Singular, Svc.ClientState.ClientLanguage)),
                                                    new("Amount", h => $"{MobHunt.Instance()->GetKillCount((byte)GetTranslatedMobHuntOrderType(currentType.RowId), (byte)h.SubrowId)}/{h.NeededKills}", 100, Alignment: ColumnAlignment.Center),
                                                    new("Finished", h => MobHunt.Instance()->GetKillCount((byte)GetTranslatedMobHuntOrderType(currentType.RowId),
                                                                                                          (byte)h.SubrowId) ==
                                                                         h.NeededKills
                                                                                 ? FontAwesomeIcon.Check.ToIconString()
                                                                                 : FontAwesomeIcon.Times.ToIconString(), 100, Alignment: ColumnAlignment.Center,
                                                        GetTextColor: h => MobHunt.Instance()->GetKillCount((byte)GetTranslatedMobHuntOrderType(currentType.RowId),
                                                                                                            (byte)h.SubrowId) ==
                                                                           h.NeededKills
                                                                                   ? Theme.SuccessGreen
                                                                                   : Theme.ErrorRed)
                                            },
                                            () => mobHunt
                                           );

        table.Draw();
    }

    private void DrawSettings()
    {
        var configChanged = false;
        ImGui.Text("Discard old Hunt Bills");
        ImGui.SameLine(250 * GlobalFontScale);
        configChanged |= ImGui.Checkbox("##oldHuntBills", ref C.DiscardOldBills);
        ImGui.Text("Detour if an A-Rank is nearby");
        ImGui.SameLine(250 * GlobalFontScale);
        configChanged |= ImGui.Checkbox("##ABDetour", ref C.DetourForARanks);
        ImGui.SameLine();
        ImGuiEx.HelpMarker("""
                           Will only try a detour once per Mark.
                           If your char dies while taking a detour, it will resume to find the original mark.
                           As a safety measure, this will only work up until Stormblood.
                           """);
        ImGui.Text("Skip Fate Marks");
        ImGui.SameLine(250 * GlobalFontScale);
        configChanged |= ImGui.Checkbox("##skipFateMarks", ref C.SkipFateMarks);

        if (configChanged)
        {
            EzConfig.Save();
            SaveConfig(Configuration);
        }
    }

    private unsafe void DrawAvailableBillsDebugTable()
    {
        if (!ImGui.CollapsingHeader("Available Hunt Bills Debug")) return;

        using var table = ImRaii.Table("##AvailableHuntBillsDebugTable",
                                       12,
                                       ImGuiTableFlags.RowBg |
                                       ImGuiTableFlags.Borders |
                                       ImGuiTableFlags.Resizable |
                                       ImGuiTableFlags.ScrollY,
                                       new Vector2(0, 420 * GlobalFontScale));
        if (!table) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Expansion", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Bill", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Type Row", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Available Row", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Offset", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Order Row", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Subrow", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Mob", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Target Id", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("BNpcName Id", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Reward", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();

        var mobHuntOrderTypes = GetCorrectedMobHuntOrderTypes();
        for (var i = 0; i < HuntBoardOptions.Count && i < mobHuntOrderTypes.Count; i++)
        {
            var key = HuntBoardOptions[i];
            var currentMobHuntType = mobHuntOrderTypes[i];
            var mobHuntOrderType = (byte)currentMobHuntType.RowId;
            var expansion = new string(key.TakeWhile(c => char.IsLetter(c) || c == ' ')
                                          .ToArray());
            var title = BillCategories.GetValueOrDefault(key, key);
            var enabled = Configuration.EnableHuntBills.TryGetValue(key, out var isEnabled) && isEnabled;
            var unlocked = MobHunt.Instance()->IsMarkBillUnlocked(mobHuntOrderType);
            var availableRowId = unlocked
                                         ? MobHunt.Instance()->GetAvailableHuntOrderRowId(mobHuntOrderType)
                                         : 0;
            var availableOffset = unlocked
                                          ? MobHunt.Instance()->AvailableMarkId[mobHuntOrderType]
                                          : (byte)0;

            if (!unlocked || availableOffset == 0)
            {
                DrawAvailableBillDebugRow(enabled,
                                          expansion,
                                          title,
                                          currentMobHuntType.RowId,
                                          availableRowId,
                                          availableOffset,
                                          0,
                                          null);
                continue;
            }

            var orderRowId = Svc.Data.GetExcelSheet<MobHuntOrderType>()
                                .GetRow(mobHuntOrderType)
                                .OrderStart.Value.RowId +
                             availableOffset -
                             1;
            var mobHuntTargets = Svc.Data.GetSubrowExcelSheet<MobHuntOrder>()[orderRowId];

            foreach (var mark in mobHuntTargets)
            {
                DrawAvailableBillDebugRow(enabled,
                                          expansion,
                                          title,
                                          currentMobHuntType.RowId,
                                          availableRowId,
                                          availableOffset,
                                          orderRowId,
                                          mark);
            }
        }
    }

    private static void DrawAvailableBillDebugRow(
            bool enabled,
            string expansion,
            string title,
            uint typeRowId,
            int availableRowId,
            byte availableOffset,
            uint orderRowId,
            MobHuntOrder? mark)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(enabled
                                      ? "Yes"
                                      : "No");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(expansion);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(title);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(typeRowId.ToString());
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(availableRowId.ToString());
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(availableOffset.ToString());
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(orderRowId == 0
                                      ? "-"
                                      : orderRowId.ToString());

        if (mark == null)
        {
            for (var i = 0; i < 5; i++)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("-");
            }

            return;
        }

        var target = mark.Value.Target.Value;
        var reward = mark.Value.MobHuntReward.Value;

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(mark.Value.SubrowId.ToString());
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(ToTitleCaseExtended(target.Name.Value.Singular, Svc.ClientState.ClientLanguage));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(target.RowId.ToString());
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(target.Name.RowId.ToString());
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{reward.RowId}: {reward.CurrencyReward}");
    }

    private void DrawHuntCurrencyOverview()
    {
        var projections = GetHuntCurrencyProjections();
        if (projections.Count == 0) return;

        var spacing = ImGui.GetStyle()
                           .ItemSpacing.X *
                      3;
        var iconSize = 24 * GlobalFontScale;
        var totalWidth = projections.Sum(projection =>
                                         {
                                             var pendingText = $"+{projection.PendingReward:N0}";
                                             var textWidth = ImGui.CalcTextSize($"{projection.Current:N0}/{projection.Max:N0} ")
                                                                  .X +
                                                             ImGui.CalcTextSize(pendingText)
                                                                  .X;
                                             return iconSize +
                                                    ImGui.GetStyle()
                                                         .ItemSpacing.X +
                                                    textWidth;
                                         }) +
                         (spacing * (projections.Count - 1));

        ImGui.Spacing();
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), (ImGui.GetContentRegionAvail()
                                                                  .X -
                                                             totalWidth) /
                                                            2));

        for (var i = 0; i < projections.Count; i++)
        {
            var projection = projections[i];
            var item = Svc.Data.GetExcelSheet<Item>()
                          .GetRow(projection.Currency.ItemId);
            DrawIcon(item.Icon, 0.6f);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{projection.Current:N0}/{projection.Max:N0} ");
            ImGui.SameLine(0, 0);
            ImGuiEx.Text(projection.WillExceedCap
                                 ? ImGuiColors.DalamudRed
                                 : ImGuiColors.HealerGreen,
                         $"+{projection.PendingReward:N0}");

            if (projection.Overflow > 0)
            {
                ImGui.SameLine();
                ImGuiEx.Text(ImGuiColors.DalamudRed, $"({projection.Overflow:N0} over)");
            }

            if (i < projections.Count - 1)
                ImGui.SameLine(0, spacing);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private unsafe List<HuntCurrencyProjection> GetHuntCurrencyProjections()
    {
        var pendingRewards = HuntCurrencies.ToDictionary(currency => currency.ItemId, _ => 0u);
        var mobHuntOrderTypes = GetCorrectedMobHuntOrderTypes();

        for (var i = 0; i < HuntBoardOptions.Count && i < mobHuntOrderTypes.Count; i++)
        {
            var key = HuntBoardOptions[i];
            if (!Configuration.EnableHuntBills.TryGetValue(key, out var enabled) || !enabled) continue;

            var currentMobHuntType = mobHuntOrderTypes[i];
            var mobHuntOrderType = (byte)currentMobHuntType.RowId;
            if (!MobHunt.Instance()->IsMarkBillUnlocked(mobHuntOrderType)) continue;

            var hasObtainedBill = MobHunt.Instance()->IsMarkBillObtained(mobHuntOrderType);
            var availableMarkId = MobHunt.Instance()->GetAvailableHuntOrderRowId(mobHuntOrderType);
            var obtainedMarkId = MobHunt.Instance()->GetObtainedHuntOrderRowId(mobHuntOrderType);
            var availableMarkOffset = MobHunt.Instance()->AvailableMarkId[mobHuntOrderType];
            var obtainedMarkOffset = MobHunt.Instance()->ObtainedMarkId[mobHuntOrderType];
            var currencyItemId = GetHuntCurrencyItemId(key);

            if (hasObtainedBill && obtainedMarkOffset > 0)
                AddBillRewards(pendingRewards, mobHuntOrderType, obtainedMarkOffset, currencyItemId, true);

            if ((!hasObtainedBill || obtainedMarkId != availableMarkId) && availableMarkOffset > 0)
                AddBillRewards(pendingRewards, mobHuntOrderType, availableMarkOffset, currencyItemId, false);
        }

        return HuntCurrencies.Select(currency =>
                                     {
                                         var current = (uint)Math.Max(0, InventoryHelper.GetInventoryItemCount(currency.ItemId));
                                         return new HuntCurrencyProjection(currency, current, pendingRewards[currency.ItemId]);
                                     })
                             .Where(projection => projection.Current > 0 || projection.PendingReward > 0)
                             .ToList();
    }

    private unsafe void AddBillRewards(Dictionary<uint, uint> pendingRewards, byte mobHuntOrderType, uint markOffset, uint currencyItemId, bool onlyOpenMarks)
    {
        var mobHuntTargets = Svc.Data.GetSubrowExcelSheet<MobHuntOrder>()
                [Svc.Data.GetExcelSheet<MobHuntOrderType>()
                    .GetRow(mobHuntOrderType)
                    .OrderStart.Value.RowId +
                 (markOffset - 1)];

        foreach (var mark in mobHuntTargets)
        {
            if (onlyOpenMarks && MobHunt.Instance()->GetKillCount(mobHuntOrderType, (byte)mark.SubrowId) >= mark.NeededKills)
                continue;

            var reward = mark.MobHuntReward.Value;
            if (currencyItemId == 0 || !pendingRewards.ContainsKey(currencyItemId)) continue;

            pendingRewards[currencyItemId] += reward.CurrencyReward;
        }
    }

    private static uint GetHuntCurrencyItemId(string huntBoardOption)
    {
        var expansion = new string(huntBoardOption.TakeWhile(c => char.IsLetter(c) || c == ' ')
                                                  .ToArray());

        return expansion switch
        {
            "A Realm Reborn" => AlliedSealsItemId,
            "Heavensward" or "Stormblood" => CenturioSealsItemId,
            "Shadowbringers" or "Endwalker" or "Dawntrail" => SacksOfNutsItemId,
            _ => 0
        };
    }

    private sealed record HuntCurrencyInfo(string Name, uint ItemId, uint Max);

    private sealed record HuntCurrencyProjection(HuntCurrencyInfo Currency, uint Current, uint PendingReward)
    {
        public uint Max => Currency.Max;
        public uint Projected => Current + PendingReward;
        public bool WillExceedCap => Projected > Max;

        public uint Overflow => WillExceedCap
                                        ? Projected - Max
                                        : 0;
    }
}
