using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Models;
using Lumina.Excel.Sheets;
using Underlings.Configuration;
using Underlings.GameHelpers;
using Underlings.Keybinds;
using Underlings.Modules;
using Underlings.TaskManager;
using Action = System.Action;
using GrandCompany = Lumina.Excel.Sheets.GrandCompany;

namespace Henchman.Features.BumpOnALog;

[Module]
public class BumpOnALogUI : ModuleUI<BumpOnALog, Configuration>
{
    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;

    internal readonly BumpOnALog          Feature = new();
    private           string              characterFilter = string.Empty;
    private           int                 classMonsterNoteId;
    private           MonsterNoteRankInfo classMonsterNoteRankInfo;
    private           int                 currentClassLogRank;
    private           int                 currentGcLogRank;
    private           int                 gcMonsterNoteId;
    private           MonsterNoteRankInfo gcMonsterNoteRankInfo;
    private readonly Cached<List<OfflineCharacterData>> characters = new(
            () => SubscriptionManager.IsLoaded(IPCNames.AutoRetainer)
                          ? AutoRetainer.GetRegisteredCIDs.Invoke([])
                                        .Select(cid => AutoRetainer.GetOfflineCharacterData.Invoke(cid)).ToList()
                          : [], TimeSpan.FromMilliseconds(500));
    private readonly Cached<Dictionary<ulong, (List<CharacterGearsets.Gearset> Gearsets, string? Error)>> offlineGearsets;
    private static readonly Dictionary<string, string> dataCenters = Svc.Data.GetExcelSheet<World>()
            .DistinctBy(x => x.Name.ExtractText())
            .ToDictionary(x => x.Name.ExtractText(), x => x.DataCenterName());

    public BumpOnALogUI()
    {
        Configuration = LoadConfig<Configuration>() ?? new Configuration();
        offlineGearsets = new(() => characters.Value.ToDictionary(x => x.CID, x => ReadOfflineGearsets(x.CID)),
                             TimeSpan.FromSeconds(5));
        Svc.Framework.Update += CaptureGCRank;
    }

    public override string          Name     => "Bump On A Log";
    public override Enum            Category => Henchman.Category.Combat;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.List;


    public override Action Help => () =>
                                   {
                                       ImGui.Text(T("HelpText"));
                                       DrawRequirements(Requirements);
                                   };

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.AutoRetainer, false),
            (IPCNames.AutoDuty, false),
            (IPCNames.Questionable, false),
            (IPCNames.BossMod, false),
            (IPCNames.Wrath, false),
            (IPCNames.RotationSolverReborn, false)
    ];

    public override bool LoginNeeded => false;

    public sealed override required Configuration Configuration { get; init; }

    public override IReadOnlyList<string> Tabs => [T("TabClass"), T("TabGrandCompany"), T("TabCharacters")];

    public override ModuleAction PrimaryAction => LogAction(SelectedTab == 1);

    public override IReadOnlyList<ModuleAction> SecondaryActions => [LogAction(SelectedTab != 1), CycleAction(SelectedTab == 1)];

    [Keybind("Bump On A Log - Start Rank Log")]
    private void StartRankLog()
    {
        if (!Svc.ClientState.IsLoggedIn || IsTaskRunning(Name)) return;
        TryStartTask(new TaskRecord(Feature.StartClassRank, "Bump On A Log - Rank Log", onDone: CleanupCombatAutomation, onAbort: CleanupCombatAutomation));
    }

    [Keybind("Bump On A Log - Start GC Log")]
    private void StartGcLog()
    {
        if (!Svc.ClientState.IsLoggedIn || Feature.server != null || IsTaskRunning(Name)) return;
        TryStartTask(new TaskRecord(token => Feature.StartGCRank(token), "Bump On A Log - GC Log", onDone: CleanupCombatAutomation, onAbort: CleanupCombatAutomation));
    }

    public override void Dispose()
    {
        Svc.Framework.Update -= CaptureGCRank;
        Feature.CleanupCycle();
    }

    private unsafe void CaptureGCRank(IFramework _)
    {
        if (!Svc.ClientState.IsLoggedIn || !Player.Available || !IsScreenAndPlayerReady()) return;
        var playerState = PlayerState.Instance();
        if (playerState == null || !playerState->IsLoaded || playerState->ContentId == 0 || playerState->ContentId != Player.CID) return;
        if (Configuration.RecordGCRank(playerState->ContentId, GetGrandCompanyRank())) SaveConfig(Configuration);
    }

    private static unsafe uint CurrentGrandCompany() => PlayerState.Instance()->GrandCompany;

    private static unsafe uint CurrentMonsterNote() => Svc.Data.GetExcelSheet<ClassJob>()
                                                          .GetRow(PlayerState.Instance()->CurrentClassJobId)
                                                          .MonsterNote.RowId;

    private ModuleAction LogAction(bool gcLog)
    {
        var blocker = !Svc.ClientState.IsLoggedIn
                              ? T("LoginForLog")
                              : gcLog
                                      ? (GcHuntRanks.ContainsKey(CurrentGrandCompany()) ? null : T("NotInGrandCompany"))
                                      : (ClassHuntRanks.ContainsKey(CurrentMonsterNote()) ? null : T("NoHuntLogForClass"));
        var enabled = blocker == null && Feature.server == null;
        return gcLog
                       ? new ModuleAction(T("StartGCLog"), StartGcLog, "Bump On A Log - Start GC Log", null, enabled, blocker)
                       : new ModuleAction(T("StartRankLog"), StartRankLog, "Bump On A Log - Start Rank Log", null, enabled, blocker);
    }

    private ModuleAction CycleAction(bool gcLog)
    {
        var hasAutoRetainer = SubscriptionManager.IsLoaded(IPCNames.AutoRetainer);
        var count = hasAutoRetainer
                            ? characters.Value.Count(x => Configuration.EnableCharacter.GetValueOrDefault(x.CID) &&
                                                          (!gcLog || !Configuration.ShouldSkipGCCharacter(x.CID)))
                            : 0;
        return new ModuleAction(string.Format(T("CycleFmt"), count), () => StartCharacterCycle(gcLog), null, FontAwesomeIcon.Users,
                                Feature.server == null && hasAutoRetainer && count > 0,
                                hasAutoRetainer ? string.Format(T("CycleTooltipFmt"), count) : T("AutoRetainerUnavailable"));
    }

    private void DrawCharacterTab()
    {
        if (!SubscriptionManager.IsLoaded(IPCNames.AutoRetainer))
        {
            Widgets.Banner(BannerKind.Info, T("AutoRetainerUnavailable"));
            return;
        }
        var configChanged = false;
        foreach (var character in characters.Value)
            if (character.GCRank > 0)
                configChanged |= Configuration.CharacterGCRanks.TryAdd(character.CID, (int)character.GCRank);
        if (configChanged) SaveConfig(Configuration);

        var shown = characters.Value
                              .Where(x => $"{x.Name} {x.World} {dataCenters.GetValueOrDefault(x.World, "")}".Contains(characterFilter, StringComparison.OrdinalIgnoreCase))
                              .ToList();
        var headerButton = new Vector2(0, ImGui.GetTextLineHeight() + 4 * GlobalFontScale);

        Widgets.Card(T("TabCharacters"), () =>
                                         {
                                             Widgets.SearchField("##LogCharacterFilter", ref characterFilter, T("FilterHint"), 240 * GlobalFontScale);
                                             ImGui.SameLine(0, 8 * GlobalFontScale);
                                             if (Widgets.Button(T("SelectAllShown"))) SetCharactersEnabled(shown, true);
                                             ImGui.SameLine(0, 6 * GlobalFontScale);
                                             if (Widgets.Button(T("DeselectAllShown"))) SetCharactersEnabled(shown, false);
                                             DrawCharacterTable(shown);
                                         }, 0f, () =>
                                                {
                                                    if (Widgets.Button(T("SelectAll"), ButtonKind.Ghost, null, headerButton)) SetCharactersEnabled(characters.Value, true);
                                                    ImGui.SameLine(0, 4 * GlobalFontScale);
                                                    if (Widgets.Button(T("DeselectAll"), ButtonKind.Ghost, null, headerButton)) SetCharactersEnabled(characters.Value, false);
                                                });
    }

    private void DrawCharacterTable(List<OfflineCharacterData> shown)
    {
        using var table = ImRaii.Table("##LogCharacters", 4, TableFlags, new Vector2(Widgets.Avail, 0));
        if (!table.Success) return;

        ImGui.TableSetupColumn("##Enabled", ImGuiTableColumnFlags.WidthFixed, 28 * GlobalFontScale);
        ImGui.TableSetupColumn(T("ColName").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1.3f);
        ImGui.TableSetupColumn(T("ColGearsets").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(T("ColGCRank").ToUpperInvariant(), ImGuiTableColumnFlags.WidthFixed, 110 * GlobalFontScale);
        using (ImRaii.PushColor(ImGuiCol.Text, V2Palette.TextMuted))
        {
            ImGui.TableHeadersRow();
        }

        var framePadding = ImGui.GetStyle().FramePadding.Y;
        var stopRank     = Configuration.StopAfterGCRank + 1;
        foreach (var character in shown)
        {
            ImGui.TableNextRow();
            if (Svc.ClientState.IsLoggedIn && character.CID == Player.CID) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, Widgets.U32(Theme.AccentPinkDim));

            ImGui.TableNextColumn();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + framePadding);
            var enabled = Configuration.EnableCharacter.GetValueOrDefault(character.CID);
            if (Widgets.Checkbox($"##LogCharacter{character.CID}", ref enabled))
            {
                Configuration.EnableCharacter[character.CID] = enabled;
                SaveConfig(Configuration);
            }

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(character.Name);
            ImGui.SameLine();
            Widgets.TextMuted($"{character.World} · {dataCenters.GetValueOrDefault(character.World, "")}");

            ImGui.TableNextColumn();
            DrawGearsetSelector(character.CID);

            ImGui.TableNextColumn();
            var rank = Configuration.CharacterGCRanks.TryGetValue(character.CID, out var savedRank) ? savedRank : -1;
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(rank >= 0 ? rank.ToString() : "-");
            ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + framePadding + (ImGui.GetTextLineHeight() - 4 * GlobalFontScale) / 2f);
            Widgets.ProgressBar(rank / (float)stopRank, rank >= stopRank ? V2Palette.Success : V2Palette.Accent, 4f, ImGui.GetContentRegionAvail().X);
        }
    }

    private void SetCharactersEnabled(IEnumerable<OfflineCharacterData> selectedCharacters, bool enabled)
    {
        Configuration.SetCharactersEnabled(selectedCharacters.Select(x => x.CID), enabled);
        SaveConfig(Configuration);
    }

    private unsafe void StartCharacterCycle(bool gcLog)
    {
        if (Running || Feature.server != null) return;
        if (!Player.Available && !(TryGetAddonByName<AtkUnitBase>("_TitleMenu", out var titleMenu) && titleMenu->IsVisible))
        {
            FullWarning(T("CycleNeedsTitleScreen"));
            return;
        }
        try
        {
            var plan = Configuration.CreateCyclePlan(characters.Value.Select(x => (x.CID, x.Name, x.World)), gcLog);
            foreach (var character in plan)
            {
                if (character.GearsetId == null) continue;
                var (gearsets, error) = Svc.ClientState.IsLoggedIn && character.CID == Player.CID
                                               ? ReadCurrentGearsets() : ReadOfflineGearsets(character.CID);
                var index = gearsets.FindIndex(x => x.Id == character.GearsetId);
                if (error != null || index < 0)
                {
                    FullWarning($"{character.Name}@{character.World}: {error ?? string.Format(T("SelectedGearsetMissingFmt"), character.GearsetId + 1)}");
                    return;
                }
                var gearset = gearsets[index];
                var classJob = Svc.Data.GetExcelSheet<ClassJob>().GetRowOrDefault(gearset.ClassJob);
                if (classJob == null || !IsCombat(gearset.ClassJob) || (!gcLog && !ClassHuntRanks.ContainsKey(classJob.Value.MonsterNote.RowId)))
                {
                    FullWarning(string.Format(T("InvalidCycleGearsetFmt"), character.Name, character.World, character.GearsetId + 1));
                    return;
                }
            }
            TryStartTask(new TaskRecord(token => Feature.RunCharacterCycle(plan, gcLog, token),
                                       $"{Name} - {(gcLog ? "GC" : "Class")} Cycle", onDone: Feature.CleanupCycle, onAbort: Feature.CleanupCycle));
        }
        catch (InvalidOperationException ex)
        {
            FullWarning(ex.Message);
        }
    }

    public override void Draw()
    {
        Widgets.Columns(16f, (0f, () =>
                                  {
                                      if (SelectedTab == 1)
                                          DrawGcHuntLog();
                                      else if (SelectedTab == 2)
                                          DrawCharacterTab();
                                      else
                                          DrawJobHuntLog();
                                  }),
                        (262f, () =>
                               {
                                   DrawSettings();
                                   Widgets.RequirementsCard(this);
                               }));
    }

    private string FormatGearset(CharacterGearsets.Gearset gearset) =>
            $"{gearset.Id + 1}. {gearset.Name} ({Svc.Data.GetExcelSheet<ClassJob>().GetRowOrDefault(gearset.ClassJob)?.Abbreviation.ExtractText() ?? "?"})";

    private void DrawGearsetSelector(ulong cid)
    {
        var (gearsets, error) = Svc.ClientState.IsLoggedIn && cid == Player.CID
                                       ? ReadCurrentGearsets()
                                       : offlineGearsets.Value.GetValueOrDefault(cid, ([], T("GearsetsUnavailable")));
        var hasSelection = Configuration.SelectedGearset.TryGetValue(cid, out var selectedId);
        var selectedIndex = hasSelection ? gearsets.FindIndex(x => x.Id == selectedId) : -1;
        var preview = selectedIndex >= 0 ? FormatGearset(gearsets[selectedIndex])
                      : hasSelection ? error ?? string.Format(T("SelectedGearsetMissingFmt"), selectedId + 1)
                      : T("None");
        ImGui.SetNextItemWidth(-1);
        if (!ImGui.BeginCombo($"##LogGearset{cid}", preview)) return;
        if (ImGui.Selectable(T("None"), !hasSelection) && Configuration.SelectedGearset.Remove(cid))
            SaveConfig(Configuration);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("NoneGearsetTooltip"));
        if (gearsets.Count == 0) ImGui.TextUnformatted(error ?? T("NoGearsets"));
        foreach (var gearset in gearsets)
        {
            var selected = hasSelection && gearset.Id == selectedId;
            if (ImGui.Selectable($"{FormatGearset(gearset)}##Gearset{gearset.Id}", selected))
            {
                Configuration.SelectedGearset[cid] = gearset.Id;
                SaveConfig(Configuration);
            }
            if (selected) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }

    private unsafe (List<CharacterGearsets.Gearset> Gearsets, string? Error) ReadCurrentGearsets()
    {
        var module = RaptureGearsetModule.Instance();
        if (module == null || module->CharacterContentId != Player.CID) return ([], T("GearsetsUnavailable"));
        return (module->Entries.ToArray()
                                    .Where(x => x.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                                    .Select(x => new CharacterGearsets.Gearset(x.Id, x.NameString, x.ClassJob)).ToList(), null);
    }

    private unsafe (List<CharacterGearsets.Gearset> Gearsets, string? Error) ReadOfflineGearsets(ulong cid)
    {
        var framework = Framework.Instance();
        if (framework == null) return ([], T("GearsetsUnavailable"));
        var configPath = Path.GetDirectoryName(framework->ConfigPath.ToString());
        if (string.IsNullOrWhiteSpace(configPath)) return ([], T("GearsetsUnavailable"));
        try
        {
            var path = Path.Combine(configPath, $"FFXIV_CHR{cid:X16}", "GEARSET.DAT");
            return (CharacterGearsets.Parse(File.ReadAllBytes(path)), null);
        }
        catch (FileNotFoundException) { return ([], T("GearsetsMissing")); }
        catch (DirectoryNotFoundException) { return ([], T("GearsetsMissing")); }
        catch (InvalidDataException) { return ([], T("GearsetsUnsupported")); }
        catch (IOException) { return ([], T("GearsetsUnavailable")); }
        catch (UnauthorizedAccessException) { return ([], T("GearsetsUnavailable")); }
    }

    private unsafe void DrawJobHuntLog()
    {
        if (!Svc.ClientState.IsLoggedIn)
        {
            Widgets.Banner(BannerKind.Info, T("LoginForLog"));
            return;
        }

        var classJobRow = Svc.Data.Excel.GetSheet<ClassJob>()
                             .GetRow(PlayerState.Instance()->CurrentClassJobId);

        classMonsterNoteId = classJobRow.MonsterNote.RowId.ToInt();

        if (!ClassHuntRanks.ContainsKey((uint)classMonsterNoteId))
        {
            Widgets.Banner(BannerKind.Error, T("NoHuntLogForClass"));
            return;
        }

        classMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[classMonsterNoteId];
        currentClassLogRank      = classMonsterNoteRankInfo.Rank;

        var huntMarks = ClassHuntRanks[(uint)classMonsterNoteId].HuntMarks;
        Widgets.Card(null, () =>
                           {
                               ImGui.TextUnformatted(classJobRow.NameEnglish.ExtractText());
                               ImGui.SameLine();
                               Widgets.TextDim(string.Format(T("CurrentDifficultyFmt"), currentClassLogRank + 1));
                               DrawHuntLog(classMonsterNoteRankInfo, huntMarks, false);
                           });
    }

    private unsafe void DrawGcHuntLog()
    {
        if (!Svc.ClientState.IsLoggedIn)
        {
            Widgets.Banner(BannerKind.Info, T("LoginForLog"));
            return;
        }

        gcMonsterNoteId = (int)Svc.Data.GetExcelSheet<GrandCompany>()
                                  .GetRow(PlayerState.Instance()->GrandCompany)
                                  .MonsterNote.RowId;

        if (!GcHuntRanks.ContainsKey(PlayerState.Instance()->GrandCompany))
        {
            Widgets.Banner(BannerKind.Error, T("NotInGrandCompany"));
            return;
        }

        var gcRow = Svc.Data.Excel.GetSheet<GrandCompany>()
                       .GetRow(PlayerState.Instance()->GrandCompany);

        gcMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[gcMonsterNoteId];
        currentGcLogRank      = gcMonsterNoteRankInfo.Rank;

        var gcRank    = GetGrandCompanyRank();
        var details   = string.Format(T("CurrentRankFmt"), gcRank, GetGCRankTitle(), currentGcLogRank);
        var huntMarks = GcHuntRanks[PlayerState.Instance()->GrandCompany].HuntMarks;
        Widgets.Card(null, () =>
                           {
                               ImGui.TextUnformatted(gcRow.Name.ExtractText());
                               ImGui.SameLine();
                               Widgets.TextDim(details);

                               if (currentGcLogRank > 2)
                                   Widgets.TextColored(V2Palette.Success, T("FinishedAllGCRanks"));
                               else if ((currentGcLogRank == 1 && gcRank < 5) || (currentGcLogRank == 2 && gcRank < 9))
                                   Widgets.TextColored(V2Palette.Danger, T("GCRankNotUnlocked"));
                               else
                                   DrawHuntLog(gcMonsterNoteRankInfo, huntMarks, true);
                           });
    }

    private void DrawHuntLog(MonsterNoteRankInfo rankInfo, HuntMark?[,] huntMarks, bool gcLog)
    {
        if (rankInfo.Rank > huntMarks.GetLength(0) - 1)
        {
            Widgets.TextColored(V2Palette.Success, T("FinishedAllRanks"));
            return;
        }

        var huntMarksArray = Enumerable.Range(0, huntMarks.GetLength(1))
                                       .Select(col => huntMarks[rankInfo.Rank, col])
                                       .Where(mark => mark != null)
                                       .Select(mark => ResolveBestLevelVariant(mark!, Svc.PlayerState.Level, preferOverworldNonFate: !gcLog))
                                       .ToArray();

        DrawHuntTable(huntMarksArray, gcLog);
    }

    private void DrawHuntTable(HuntMark[] marks, bool gcLog)
    {
        using var table = ImRaii.Table("##HuntTable", 4, TableFlags, new Vector2(Widgets.Avail, 0));
        if (!table.Success) return;

        ImGui.TableSetupColumn(T("ColName").ToUpperInvariant());
        ImGui.TableSetupColumn(T("ColLevel").ToUpperInvariant(), ImGuiTableColumnFlags.WidthFixed, 70 * GlobalFontScale);
        ImGui.TableSetupColumn(T("ColKills").ToUpperInvariant(), ImGuiTableColumnFlags.WidthFixed, 100 * GlobalFontScale);
        ImGui.TableSetupColumn(T("ColFinished").ToUpperInvariant(), ImGuiTableColumnFlags.WidthFixed, 100 * GlobalFontScale);
        using (ImRaii.PushColor(ImGuiCol.Text, V2Palette.TextMuted))
        {
            ImGui.TableHeadersRow();
        }

        foreach (var mark in marks)
        {
            ImGui.TableNextRow();
            if (mark.IsCurrentTarget) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, Widgets.U32(Theme.AccentPinkDim));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(ToTitleCaseExtended(mark.Name, Svc.ClientState.ClientLanguage));
            ImGui.TableNextColumn();
            Widgets.TextMuted(mark.Level?.ToString() ?? "-");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{mark.GetCurrentMonsterNoteKills}/{mark.NeededKills}");
            ImGui.TableNextColumn();
            DrawCompletionAction(mark, gcLog);
        }
    }

    private void DrawCompletionAction(HuntMark mark, bool gcLog)
    {
        var finished = mark.GetOpenMonsterNoteKills == 0;
        if (finished)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.SuccessGreen))
            {
                using var font = ImRaii.PushFont(UiBuilder.IconFont);
                ImGui.Text(FontAwesomeIcon.Check.ToIconString());
            }

            return;
        }

        using (ImRaii.PushColor(ImGuiCol.Text, Theme.ErrorRed))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.Text(FontAwesomeIcon.Times.ToIconString());

            if (ImGui.IsItemClicked() && !IsTaskRunning(Name)) TryStartTask(new TaskRecord(token => Feature.ProcessSingleMark(mark, gcLog, token), Name, onDone: CleanupCombatAutomation, onAbort: CleanupCombatAutomation));
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Click to hunt");
    }

    private void DrawSettings()
    {
        Widgets.Card(T("RunOptions"), () =>
                                      {
                                          var configChanged = false;

                                          Widgets.TextDim(T("StopAfterJobRank"));
                                          ImGui.SetNextItemWidth(Widgets.Avail);
                                          configChanged |= ImGui.Combo("##jobRank", ref Configuration.StopAfterJobRank, Enumerable.Range(1, 5)
                                                                                                                                  .Select(x => x.ToString())
                                                                                                                                  .ToArray(), 5);

                                          Widgets.TextDim(T("StopAfterGCRank"));
                                          ImGui.SetNextItemWidth(Widgets.Avail);
                                          configChanged |= ImGui.Combo("##gcRank", ref Configuration.StopAfterGCRank, Enumerable.Range(1, 9)
                                                                                                                                .Select(x => x.ToString())
                                                                                                                                .ToArray(), 9);

                                          configChanged |= Widgets.SwitchRow(T("OrderByTerritory"), ref Configuration.OrderByTerritory);
                                          configChanged |= Widgets.SwitchRow(T("SkipDutyMarks"), ref Configuration.SkipDutyMarks);
                                          configChanged |= Widgets.SwitchRow(T("SoloUnsyncDuty"), ref C.SoloUnsyncLogDuty);
                                          configChanged |= Widgets.SwitchRow(T("RankUpGC"), ref Configuration.AutoGCRankUp);

                                          if (configChanged)
                                          {
                                              PluginConfig.Save();
                                              SaveConfig(Configuration);
                                          }
                                      });
    }

    public unsafe string GetGCRankTitle()
    {
        var playerState = PlayerState.Instance();
        var playerSex   = playerState->Sex;
        var playerGC    = playerState->GrandCompany;
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
