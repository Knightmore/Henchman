using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
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
    internal readonly BumpOnALog          Feature = new();
    private           int                 classMonsterNoteId;
    private           MonsterNoteRankInfo classMonsterNoteRankInfo;
    private           int                 currentClassLogRank;
    private           int                 currentGcLogRank;
    private           int                 gcMonsterNoteId;
    private           MonsterNoteRankInfo gcMonsterNoteRankInfo;
    private readonly Table<OfflineCharacterData> characterTable;
    private readonly Cached<List<OfflineCharacterData>> characters = new(
            () => SubscriptionManager.IsLoaded(IPCNames.AutoRetainer)
                          ? AutoRetainer.GetRegisteredCIDs.Invoke([])
                                        .Select(cid => AutoRetainer.GetOfflineCharacterData.Invoke(cid)).ToList()
                          : [], TimeSpan.FromMilliseconds(500));
    private readonly Cached<Dictionary<ulong, (List<CharacterGearsets.Gearset> Gearsets, string? Error)>> offlineGearsets;
    private static readonly Dictionary<string, string> dataCenters = Svc.Data.GetExcelSheet<World>()
            .DistinctBy(x => x.Name.ExtractText())
            .ToDictionary(x => x.Name.ExtractText(), x => x.DataCenter.Value.Name.ExtractText());

    public BumpOnALogUI()
    {
        Configuration = LoadConfig<Configuration>() ?? new Configuration();
        offlineGearsets = new(() => characters.Value.ToDictionary(x => x.CID, x => ReadOfflineGearsets(x.CID)),
                             TimeSpan.FromSeconds(5));
        characterTable = new Table<OfflineCharacterData>("##LogCharacters",
                [
                    new("##Enabled", Width: 35, Alignment: ColumnAlignment.Center, DrawCustom: (x, _) =>
                    {
                        var enabled = Configuration.EnableCharacter.GetValueOrDefault(x.CID);
                        using var color = ImRaii.PushColor(ImGuiCol.Button, 0xFF097000, enabled);
                        if (IconButton($"\uf021##LogCharacter{x.CID}"))
                        {
                            Configuration.EnableCharacter[x.CID] = !enabled;
                            SaveConfig(Configuration);
                        }
                    }),
                    new(T("ColName"), x => x.Name, 135, FilterType.String, ColumnAlignment.Center),
                    new(T("ColWorld"), x => x.World, 90, FilterType.MultiSelect, ColumnAlignment.Center),
                    new(T("ColDataCenter"), x => dataCenters.GetValueOrDefault(x.World, ""),
                        90, FilterType.MultiSelect, ColumnAlignment.Center),
                    new(T("ColGCRank"), x => Configuration.CharacterGCRanks.TryGetValue(x.CID, out var rank) && rank >= 0 ? rank.ToString() : "-",
                        70, Alignment: ColumnAlignment.Center),
                    new(T("ColGearsets"), Width: 280, DrawCustom: (x, _) => DrawGearsetSelector(x.CID))
                ], () => characters.Value, x => Svc.ClientState.IsLoggedIn && x.CID == Player.CID);
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

    private void DrawCharacterTab()
    {
        if (!SubscriptionManager.IsLoaded(IPCNames.AutoRetainer))
        {
            ImGui.TextUnformatted(T("AutoRetainerUnavailable"));
            return;
        }
        var configChanged = false;
        foreach (var character in characters.Value)
            if (character.GCRank > 0)
                configChanged |= Configuration.CharacterGCRanks.TryAdd(character.CID, (int)character.GCRank);
        if (configChanged) SaveConfig(Configuration);
        DrawCentered("##LogAllCharacterSelector", () =>
        {
            if (ImGui.Button(T("SelectAll"))) SetCharactersEnabled(characters.Value, true);
            ImGui.SameLine();
            if (ImGui.Button(T("DeselectAll"))) SetCharactersEnabled(characters.Value, false);
        });
        DrawCentered("##LogShownCharacterSelector", () =>
        {
            if (ImGui.Button(T("SelectAllShown"))) SetCharactersEnabled(characterTable.FilteredItems, true);
            ImGui.SameLine();
            if (ImGui.Button(T("DeselectAllShown"))) SetCharactersEnabled(characterTable.FilteredItems, false);
        });
        characterTable.Draw();
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

    private void DrawLogActions(bool gcLog, bool canStart, Action? description = null)
    {
        var hasAutoRetainer = SubscriptionManager.IsLoaded(IPCNames.AutoRetainer);
        var count = hasAutoRetainer ? characters.Value.Count(x => Configuration.EnableCharacter.GetValueOrDefault(x.CID) &&
                                                                 (!gcLog || !Configuration.ShouldSkipGCCharacter(x.CID))) : 0;
        var cycleLabel = string.Format(T("CycleFmt"), count);
        var cycleWidth = Math.Max(70 * GlobalFontScale, ImGui.CalcTextSize(cycleLabel).X + 2 * ImGui.GetStyle().FramePadding.X);
        var shift = cycleWidth - 70 * GlobalFontScale;
        Layout.DrawInfoBox(() =>
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - shift);
            using (ImRaii.Disabled(Running || Feature.server != null || !hasAutoRetainer || count == 0))
                if (ImGui.Button(cycleLabel, new Vector2(cycleWidth, 30 * GlobalFontScale))) StartCharacterCycle(gcLog);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(string.Format(T("CycleTooltipFmt"), count));
        }, description, () =>
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - shift);
            using (ImRaii.Disabled(Running || Feature.server != null || !canStart))
                if (StartButton())
                {
                    if (gcLog) StartGcLog();
                    else StartRankLog();
                }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(T("StartTooltip"));
        });
        ImGui.Spacing();
    }

    public override void Draw()
    {
        using var tabs = ImRaii.TabBar("##BumpOnALogTabs", ImGuiTabBarFlags.None);
        if (tabs)
        {
            using (var tab = ImRaii.TabItem(T("TabClass")))
            {
                if (tab)
                    DrawJobHuntLog();
            }

            using (var tab = ImRaii.TabItem(T("TabGrandCompany")))
            {
                if (tab)
                    DrawGcHuntLog();
            }


            using (var tab = ImRaii.TabItem(T("TabCharacters")))
            {
                if (tab) DrawCharacterTab();
            }

            using (var tab = ImRaii.TabItem(T("TabSettings")))
            {
                if (tab)
                    DrawSettings();
            }
        }
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
            DrawLogActions(false, false);
            ImGui.TextUnformatted(T("LoginForLog"));
            return;
        }

        var classJobRow = Svc.Data.Excel.GetSheet<ClassJob>()
                             .GetRow(PlayerState.Instance()->CurrentClassJobId);

        classMonsterNoteId = classJobRow.MonsterNote.RowId.ToInt();

        if (!ClassHuntRanks.ContainsKey((uint)classMonsterNoteId))
        {
            DrawLogActions(false, false);
            TextCentered(ImGuiColors.DalamudRed, T("NoHuntLogForClass"));
            return;
        }

        classMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[classMonsterNoteId];
        currentClassLogRank      = classMonsterNoteRankInfo.Rank;

        DrawLogActions(false, true, () =>
                           {
                               ImGui.Text(classJobRow.NameEnglish.ExtractText());
                               ImGui.SameLine();

                               using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary)) ImGui.Text(string.Format(T("CurrentDifficultyFmt"), currentClassLogRank + 1));
                           });

        DrawHuntLog(classMonsterNoteRankInfo, ClassHuntRanks[(uint)classMonsterNoteId].HuntMarks, false);
    }

    private unsafe void DrawGcHuntLog()
    {
        if (!Svc.ClientState.IsLoggedIn)
        {
            DrawLogActions(true, false);
            ImGui.TextUnformatted(T("LoginForLog"));
            return;
        }

        gcMonsterNoteId = (int)Svc.Data.GetExcelSheet<GrandCompany>()
                                  .GetRow(PlayerState.Instance()->GrandCompany)
                                  .MonsterNote.RowId;

        if (!GcHuntRanks.ContainsKey(PlayerState.Instance()->GrandCompany))
        {
            DrawLogActions(true, false);
            TextCentered(ImGuiColors.DalamudRed, T("NotInGrandCompany"));
            return;
        }

        var gcRow = Svc.Data.Excel.GetSheet<GrandCompany>()
                       .GetRow(PlayerState.Instance()->GrandCompany);

        gcMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[gcMonsterNoteId];
        currentGcLogRank      = gcMonsterNoteRankInfo.Rank;

        DrawLogActions(true, true, () =>
                              {
                                  ImGui.Text(gcRow.Name.ExtractText());
                                  ImGui.SameLine();

                                  using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary)) ImGui.Text(string.Format(T("CurrentRankFmt"), GetGrandCompanyRank(), GetGCRankTitle(), currentGcLogRank));
                              });

        if (currentGcLogRank > 2)
        {
            TextCentered(ImGuiColors.HealerGreen, T("FinishedAllGCRanks"));
            return;
        }

        if ((currentGcLogRank == 1 && GetGrandCompanyRank() < 5) || (currentGcLogRank == 2 && GetGrandCompanyRank() < 9))
        {
            TextCentered(ImGuiColors.DalamudRed, T("GCRankNotUnlocked"));
            return;
        }

        DrawHuntLog(gcMonsterNoteRankInfo, GcHuntRanks[PlayerState.Instance()->GrandCompany].HuntMarks, true);
    }

    private void DrawHuntLog(MonsterNoteRankInfo rankInfo, HuntMark?[,] huntMarks, bool gcLog)
    {
        if (rankInfo.Rank > huntMarks.GetLength(0) - 1)
        {
            TextCentered(ImGuiColors.HealerGreen, T("FinishedAllRanks"));
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
        var table = new Table<HuntMark>(
                                        "##HuntTable",
                                        new List<TableColumn<HuntMark>>
                                        {
                                                new(T("ColName"), h => ToTitleCaseExtended(h.Name, Svc.ClientState.ClientLanguage)),
                                                new("Level", h => h.Level?.ToString() ?? "-", 70, Alignment: ColumnAlignment.Center),
                                                new(T("ColKills"), h => $"{h.GetCurrentMonsterNoteKills}/{h.NeededKills}", 100, Alignment: ColumnAlignment.Center),
                                                new(T("ColFinished"), Width: 100, Alignment: ColumnAlignment.Center, DrawCustom: (h, _) => DrawCompletionAction(h, gcLog))
                                        },
                                        () => marks,
                                        h => h.IsCurrentTarget
                                       );

        table.Draw();
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
        var configChanged = false;

        DrawCentered("##BumpOnALogSpacer", () => { });
        ImGui.Text(T("StopAfterJobRank"));
        ImGui.SameLine(250          * GlobalFontScale);
        ImGui.SetNextItemWidth(120f * GlobalFontScale);
        configChanged |= ImGui.Combo("##jobRank", ref Configuration.StopAfterJobRank, Enumerable.Range(1, 5)
                                                                                                .Select(x => x.ToString())
                                                                                                .ToArray(), 5);

        ImGui.Text(T("StopAfterGCRank"));
        ImGui.SameLine(250          * GlobalFontScale);
        ImGui.SetNextItemWidth(120f * GlobalFontScale);
        configChanged |= ImGui.Combo("##gcRank", ref Configuration.StopAfterGCRank, Enumerable.Range(1, 9)
                                                                                              .Select(x => x.ToString())
                                                                                              .ToArray(), 9);

        ImGui.Text(T("OrderByTerritory"));
        ImGui.SameLine(250 * GlobalFontScale);
        configChanged |= ImGui.Checkbox("##orderByTerritory", ref Configuration.OrderByTerritory);

        ImGui.Text(T("SkipDutyMarks"));
        ImGui.SameLine(250 * GlobalFontScale);
        configChanged |= ImGui.Checkbox("##skipDutyMarks", ref Configuration.SkipDutyMarks);

        ImGui.Text(T("SoloUnsyncDuty"));
        ImGui.SameLine(250 * GlobalFontScale);
        configChanged |= ImGui.Checkbox("##soloUnsyncDuty", ref C.SoloUnsyncLogDuty);

        ImGui.Text(T("RankUpGC"));
        ImGui.SameLine(250 * GlobalFontScale);
        configChanged |= ImGui.Checkbox("##autoGCRankUp", ref Configuration.AutoGCRankUp);


        if (configChanged)
        {
            PluginConfig.Save();
            SaveConfig(Configuration);
        }
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
