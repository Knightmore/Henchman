using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Underlings.Configuration;
using Underlings.Keybinds;
using Underlings.Modules;
using Action = System.Action;

namespace Henchman.Features.OnYourMark;

[Module]
public class OnYourMarkUI : ModuleUI<OnYourMark, Configuration>
{
    private const uint AlliedSealsItemId   = 27;
    private const uint CenturioSealsItemId = 10307;
    private const uint SacksOfNutsItemId   = 26533;
    private const uint HuntCurrencyCap     = 4000;

    private static readonly HuntCurrencyInfo[] HuntCurrencies =
    [
            new("Allied Seals", AlliedSealsItemId, HuntCurrencyCap),
            new("Centurio Seals", CenturioSealsItemId, HuntCurrencyCap),
            new("Sacks of Nuts", SacksOfNutsItemId, HuntCurrencyCap)
    ];

    private string selectedExpansion = ExpansionOf(HuntBoardOptions[0]);

    public OnYourMarkUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public override string          Name     => "On Your Mark";
    public override Enum            Category => Henchman.Category.Combat;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Bullseye;

    public override Action Help => () =>
                                   {
                                       ImGui.Text(T("HelpText"));
                                       Text(ImGuiColors.DalamudGrey, T("ColorGrey"));
                                       ImGui.SameLine(90 * GlobalFontScale);
                                       ImGui.Text(T("ColorGreyDesc"));
                                       Text(ImGuiColors.DalamudRed, T("ColorRed"));
                                       ImGui.SameLine(90 * GlobalFontScale);
                                       ImGui.Text(T("ColorRedDesc"));
                                       Text(ImGuiColors.DalamudYellow, T("ColorYellow"));
                                       ImGui.SameLine(90 * GlobalFontScale);
                                       ImGui.Text(T("ColorYellowDesc"));
                                       Text(ImGuiColors.DalamudOrange, T("ColorOrange"));
                                       ImGui.SameLine(90 * GlobalFontScale);
                                       ImGui.Text(T("ColorOrangeDesc"));
                                       Text(ImGuiColors.HealerGreen, T("ColorGreen"));
                                       ImGui.SameLine(90 * GlobalFontScale);
                                       ImGui.Text(T("ColorGreenDesc"));

                                       DrawRequirements(Requirements);
                                   };

    public override                 bool          LoginNeeded   => true;
    public sealed override required Configuration Configuration { get; init; }

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.BossMod, false),
            (IPCNames.Wrath, false),
            (IPCNames.RotationSolverReborn, false)
    ];

    public override ModuleAction PrimaryAction => new(Loc.G("UI.V2.Start", "Start"), StartTask, "On Your Mark - Start");

    public override string? Summary => string.Format(T("SummaryEnabled"), HuntBoardOptions.Count(IsEnabled), HuntBoardOptions.Count);

    [Keybind("On Your Mark - Start")]
    private void StartTask()
    {
        if (IsTaskRunning(Name)) return;
        Feature.RunTask();
    }

    public override void Draw()
    {
        Widgets.Columns(16f, (0f, () =>
                                  {
                                      DrawHuntCurrencyOverview();
                                      DrawExpansionPicker();
                                      DrawBills();
                                  }),
                        (262f, () =>
                               {
                                   Widgets.RequirementsCard(this);
                                   DrawSettings();
                               }));
    }

    private void DrawExpansionPicker()
    {
        var expansions = HuntBoardOptions.GroupBy(ExpansionOf)
                                         .ToList();
        var rowY = ImGui.GetCursorScreenPos().Y;

        Widgets.Segmented("huntExpansion", ref selectedExpansion, expansions.Select(group => (group.Key, T(group.Key))).ToList(), 0f, null,
                          expansions.Select(group => $"{group.Count(IsEnabled)}/{group.Count()}").ToList());
        ImGui.SameLine();
        Widgets.RightAligned("##expansionToggles", () =>
                                                   {
                                                       if (Widgets.Button(T("EnableAll"), ButtonKind.Ghost)) SetExpansionEnabled(true);
                                                       ImGui.SameLine(0, 4 * GlobalFontScale);
                                                       if (Widgets.Button(T("DisableAll"), ButtonKind.Ghost)) SetExpansionEnabled(false);
                                                   }, rowY + 2 * GlobalFontScale);
    }

    private void SetExpansionEnabled(bool enabled)
    {
        foreach (var key in HuntBoardOptions.Where(key => ExpansionOf(key) == selectedExpansion))
            Configuration.EnableHuntBills[key] = enabled;
        SaveConfig(Configuration);
    }

    private void DrawBills()
    {
        var mobHuntOrderTypes = GetCorrectedMobHuntOrderTypes();
        var huntedType = IsTaskRunning(Name)
                                 ? HuntMarks.FirstOrDefault(mark => mark.IsCurrentTarget)?.MobHuntRowId
                                 : null;
        var cells = new List<Action<float>>();

        for (var i = 0; i < HuntBoardOptions.Count && i < mobHuntOrderTypes.Count; i++)
        {
            var key = HuntBoardOptions[i];
            if (ExpansionOf(key) != selectedExpansion) continue;

            var typeRow = (byte)mobHuntOrderTypes[i].RowId;
            var bill    = GetBill(typeRow);
            cells.Add(width => DrawBillCard(key, bill, typeRow == huntedType, width));
        }

        Widgets.Grid(2, 10f, cells);
    }

    private void DrawBillCard(string key, BillInfo bill, bool hunted, float width)
    {
        var enabled        = IsEnabled(key);
        var (label, color) = GetStatus(bill.Status);
        var chipId         = $"##billStatus_{key}";

        Widgets.Card(null, () =>
                           {
                               var rowY = ImGui.GetCursorScreenPos().Y;
                               if (Widgets.Switch($"##bill_{key}", ref enabled))
                               {
                                   Configuration.EnableHuntBills[key] = enabled;
                                   SaveConfig(Configuration);
                               }

                               ImGui.SameLine(0, 10 * GlobalFontScale);
                               ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (20 * GlobalFontScale - ImGui.GetTextLineHeight()) / 2f);
                               ImGui.BeginGroup();
                               ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Widgets.Avail - Widgets.LastWidth(chipId) - 8 * GlobalFontScale);
                               Widgets.TextColored(enabled ? V2Palette.Text : V2Palette.TextDim, BillCategories[key], true);
                               ImGui.PopTextWrapPos();
                               ImGui.EndGroup();
                               ImGui.SameLine();
                               Widgets.RightAligned(chipId, () => Widgets.Chip(label, color), rowY);

                               if (bill.Status is BillStatus.Locked or BillStatus.NotObtained)
                               {
                                   Widgets.TextMuted(T(bill.Status == BillStatus.Locked ? "NoteLocked" : "NoteNotObtained"), true);
                                   return;
                               }

                               Widgets.ProgressBar(bill.Finished / (float)bill.Marks.Count, color, 6f);
                               Widgets.TextMuted(string.Format(T("MarksDone"), bill.Finished, bill.Marks.Count));
                               if (bill.Status != BillStatus.Complete) DrawMarkTable(key, bill.Marks);
                           }, width, null, hunted ? V2Palette.Accent : null, enabled ? null : V2Palette.HeaderBg);
    }

    private void DrawMarkTable(string key, List<BillMark> marks)
    {
        using var table = ImRaii.Table($"##{key}BillTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp, new Vector2(Widgets.Avail, 0));
        if (!table.Success) return;

        ImGui.TableSetupColumn(T("ColName").ToUpperInvariant());
        ImGui.TableSetupColumn(T("ColAmount").ToUpperInvariant(), ImGuiTableColumnFlags.WidthFixed);
        using (ImRaii.PushColor(ImGuiCol.Text, V2Palette.TextMuted))
        {
            ImGui.TableHeadersRow();
        }

        foreach (var mark in marks)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            Widgets.StatusCircle(mark.Finished ? V2Palette.Success : V2Palette.Danger, mark.Finished ? FontAwesomeIcon.Check : FontAwesomeIcon.Times);
            ImGui.SameLine(0, 8 * GlobalFontScale);
            Widgets.TextColored(mark.Finished ? V2Palette.TextDim : V2Palette.Text, mark.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{mark.Kills}/{mark.NeededKills}");
        }
    }

    private (string Label, Vector4 Color) GetStatus(BillStatus status) => status switch
                                                                          {
                                                                                  BillStatus.Locked      => (T("StatusLocked"), V2Palette.TextMuted),
                                                                                  BillStatus.NotObtained => (T("StatusNotObtained"), V2Palette.Danger),
                                                                                  BillStatus.InProgress  => (T("StatusInProgress"), V2Palette.Warning),
                                                                                  BillStatus.OldBill     => (T("StatusOldBill"), ImGuiColors.ParsedOrange),
                                                                                  _                      => (T("StatusComplete"), V2Palette.Success)
                                                                          };

    private static unsafe BillInfo GetBill(byte typeRow)
    {
        var mobHunt = MobHunt.Instance();
        if (!mobHunt->IsMarkBillUnlocked(typeRow)) return new BillInfo(BillStatus.Locked, []);

        var obtained        = mobHunt->IsMarkBillObtained(typeRow);
        var availableMarkId = mobHunt->GetAvailableHuntOrderRowId(typeRow);
        var obtainedMarkId  = mobHunt->GetObtainedHuntOrderRowId(typeRow);
        var markOffset = obtained && availableMarkId != obtainedMarkId
                                 ? mobHunt->ObtainedMarkId[typeRow]
                                 : mobHunt->AvailableMarkId[typeRow];
        var marks = new List<BillMark>();

        if (markOffset > 0)
        {
            var orderRowId = Svc.Data.GetExcelSheet<MobHuntOrderType>()
                                .GetRow(typeRow)
                                .OrderStart.Value.RowId +
                             markOffset -
                             1u;
            foreach (var mark in Svc.Data.GetSubrowExcelSheet<MobHuntOrder>()[orderRowId])
                marks.Add(new BillMark(ToTitleCaseExtended(mark.Target.Value.Name.Value.Singular, Svc.ClientState.ClientLanguage),
                                       mobHunt->GetKillCount(typeRow, (byte)mark.SubrowId),
                                       mark.NeededKills));
        }

        var allMobsKilled = marks.All(mark => mark.Finished);
        var status = obtained
                             ? obtainedMarkId == availableMarkId
                                       ? BillStatus.InProgress
                                       : BillStatus.OldBill
                             : allMobsKilled && obtainedMarkId == availableMarkId
                                     ? BillStatus.Complete
                                     : BillStatus.NotObtained;
        return new BillInfo(status, marks);
    }

    private bool IsEnabled(string key) => Configuration.EnableHuntBills.GetValueOrDefault(key);

    private static string ExpansionOf(string huntBoardOption) => new(huntBoardOption.TakeWhile(c => char.IsLetter(c) || c == ' ')
                                                                                    .ToArray());

    private void DrawSettings()
    {
        Widgets.Card(T("TabSettings"), () =>
                                       {
                                           var configChanged = Widgets.SwitchRow(T("DiscardOldBills"), ref C.DiscardOldBills);
                                           configChanged |= Widgets.SwitchRow(T("DetourForARanks"), ref C.DetourForARanks, help: T("DetourHelp"));
                                           configChanged |= Widgets.SwitchRow(T("SkipFateMarks"), ref C.SkipFateMarks);
                                           if (!configChanged) return;

                                           PluginConfig.Save();
                                           SaveConfig(Configuration);
                                       });
    }

    private unsafe void DrawAvailableBillsDebugTable()
    {
        if (!ImGui.CollapsingHeader("Available Hunt Bills Debug")) return;

        using var table = ImRaii.Table("##AvailableHuntBillsDebugTable",
                                       12,
                                       ImGuiTableFlags.RowBg     |
                                       ImGuiTableFlags.Borders   |
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
            var key                = HuntBoardOptions[i];
            var currentMobHuntType = mobHuntOrderTypes[i];
            var mobHuntOrderType   = (byte)currentMobHuntType.RowId;
            var expansion          = ExpansionOf(key);
            var title              = BillCategories.GetValueOrDefault(key, key);
            var enabled            = Configuration.EnableHuntBills.TryGetValue(key, out var isEnabled) && isEnabled;
            var unlocked           = MobHunt.Instance()->IsMarkBillUnlocked(mobHuntOrderType);
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
            bool          enabled,
            string        expansion,
            string        title,
            uint          typeRowId,
            int           availableRowId,
            byte          availableOffset,
            uint          orderRowId,
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
        var cells = GetHuntCurrencyProjections()
                   .Select<HuntCurrencyProjection, Action<float>>(projection => width => DrawHuntCurrency(projection, width))
                   .ToList();
        Widgets.Grid(3, 10f, cells);
    }

    private void DrawHuntCurrency(HuntCurrencyProjection projection, float width)
    {
        var item = Svc.Data.GetExcelSheet<Item>()
                      .GetRow(projection.Currency.ItemId);
        var hint = projection.WillExceedCap
                           ? string.Format(T("PendingOverCap"), projection.PendingReward, projection.Overflow)
                           : string.Format(T("Pending"), projection.PendingReward);

        Widgets.StatCard(item.Name.ExtractText(), projection.Current.ToString("N0"), hint, item.Icon, color: projection.WillExceedCap
                                                                                                                    ? V2Palette.Warning
                                                                                                                    : V2Palette.Success,
                         width: width, progress: projection.Current / (float)projection.Max, max: projection.Max.ToString("N0"));
    }

    private unsafe List<HuntCurrencyProjection> GetHuntCurrencyProjections()
    {
        var pendingRewards    = HuntCurrencies.ToDictionary(currency => currency.ItemId, _ => 0u);
        var mobHuntOrderTypes = GetCorrectedMobHuntOrderTypes();

        for (var i = 0; i < HuntBoardOptions.Count && i < mobHuntOrderTypes.Count; i++)
        {
            var key = HuntBoardOptions[i];
            if (!Configuration.EnableHuntBills.TryGetValue(key, out var enabled) || !enabled) continue;

            var currentMobHuntType = mobHuntOrderTypes[i];
            var mobHuntOrderType   = (byte)currentMobHuntType.RowId;
            if (!MobHunt.Instance()->IsMarkBillUnlocked(mobHuntOrderType)) continue;

            var hasObtainedBill     = MobHunt.Instance()->IsMarkBillObtained(mobHuntOrderType);
            var availableMarkId     = MobHunt.Instance()->GetAvailableHuntOrderRowId(mobHuntOrderType);
            var obtainedMarkId      = MobHunt.Instance()->GetObtainedHuntOrderRowId(mobHuntOrderType);
            var availableMarkOffset = MobHunt.Instance()->AvailableMarkId[mobHuntOrderType];
            var obtainedMarkOffset  = MobHunt.Instance()->ObtainedMarkId[mobHuntOrderType];
            var currencyItemId      = GetHuntCurrencyItemId(key);

            if (hasObtainedBill && obtainedMarkOffset > 0)
                AddBillRewards(pendingRewards, mobHuntOrderType, obtainedMarkOffset, currencyItemId, true);

            if (availableMarkOffset > 0)
            {
                if (!hasObtainedBill && obtainedMarkId != availableMarkId)
                    AddBillRewards(pendingRewards, mobHuntOrderType, availableMarkOffset, currencyItemId, false);
                else if (!hasObtainedBill && !OnYourMark.IsBillCompleted(mobHuntOrderType, availableMarkOffset)) AddBillRewards(pendingRewards, mobHuntOrderType, availableMarkOffset, currencyItemId, true);
            }
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
            if (mark.Target.Value.FATE.IsValid && mark.Target.Value.FATE.Value.RowId > 0 && C.SkipFateMarks)
                continue;

            if (onlyOpenMarks && MobHunt.Instance()->GetKillCount(mobHuntOrderType, (byte)mark.SubrowId) >= mark.NeededKills)
                continue;

            var reward = mark.MobHuntReward.Value;
            if (currencyItemId == 0 || !pendingRewards.ContainsKey(currencyItemId)) continue;

            pendingRewards[currencyItemId] += reward.CurrencyReward;
        }
    }

    private static uint GetHuntCurrencyItemId(string huntBoardOption) => ExpansionOf(huntBoardOption) switch
                                                                         {
                                                                                 "A Realm Reborn"                               => AlliedSealsItemId,
                                                                                 "Heavensward" or "Stormblood"                  => CenturioSealsItemId,
                                                                                 "Shadowbringers" or "Endwalker" or "Dawntrail" => SacksOfNutsItemId,
                                                                                 _                                              => 0
                                                                         };

    private enum BillStatus
    {
        Locked,
        NotObtained,
        InProgress,
        OldBill,
        Complete
    }

    private sealed record BillMark(string Name, int Kills, int NeededKills)
    {
        public bool Finished => Kills >= NeededKills;
    }

    private sealed record BillInfo(BillStatus Status, List<BillMark> Marks)
    {
        public int Finished => Marks.Count(mark => mark.Finished);
    }

    private sealed record HuntCurrencyInfo(string Name, uint ItemId, uint Max);

    private sealed record HuntCurrencyProjection(HuntCurrencyInfo Currency, uint Current, uint PendingReward)
    {
        public uint Max           => Currency.Max;
        public uint Projected     => Current + PendingReward;
        public bool WillExceedCap => Projected > Max;

        public uint Overflow => WillExceedCap
                                        ? Projected - Max
                                        : 0;
    }
}
