using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Henchman.Data;
using Lumina.Excel.Sheets;
using Underlings.Keybinds;
using Underlings.Modules;
using Underlings.TaskManager;
using Action = System.Action;

namespace Henchman.Features.RetainerVocate;

[Module]
public class RetainerVocateUI : ModuleUI<RetainerVocate, Configuration>
{
    internal readonly RetainerVocate Feature = new();
    private           bool           configChanged;

    public RetainerVocateUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public sealed override required Configuration   Configuration { get; init; }
    public override                 string          Name          => "Retainer Vocate";
    public override                 Enum            Category      => Henchman.Category.Economy;
    public override                 FontAwesomeIcon Icon          => FontAwesomeIcon.ConciergeBell;

    public override Action Help => () =>
                                   {
                                       ImGui.Text(T("HelpText"));
                                       DrawRequirements(Requirements);
                                   };

    public override bool LoginNeeded => true;

    public override ModuleAction PrimaryAction => new(Loc.G("UI.V2.Start", "Start"), StartTask, "Retainer Vocate - Start", enabled: QuestManager.IsQuestComplete(66196),
                                                      disabledReason: T("RetainersLocked"));

    public override IReadOnlyList<ModuleAction> SecondaryActions
    {
        get
        {
            if (!QuestManager.IsQuestComplete(66196)) return [];

            var maxRetainers = MaxRetainerEntitlement();
            List<ModuleAction> actions =
            [
                    new(Configuration.UseMaxRetainerAmount || maxRetainers != 0
                                ? T("CreateRetainers")
                                : T("GoToVocate"), CreateRetainers, enabled: maxRetainers == 0 || HasFreeRetainerSlot(), disabledReason: T("CannotCreateMoreRetainers"))
            ];

            if (!QuestManager.IsQuestComplete(66968) && !QuestManager.IsQuestComplete(66969) && !QuestManager.IsQuestComplete(66970))
            {
                var classJob = Svc.Data.GetExcelSheet<ClassJob>()
                                  .GetRow(Configuration.QstClassJob);
                actions.Add(new ModuleAction(T("RunQuest"), RunVentureQuest, enabled: GetFirstGearsetForClassJob(classJob) != null, disabledReason: T("NoGearsetForClass")));
            }

            actions.Add(new ModuleAction(T("AssignClass"), AssignClass));
            return actions;
        }
    }

    [Keybind("Retainer Vocate - Start")]
    private void StartTask()
    {
        if (IsTaskRunning(Name)) return;
        TryStartTask(new TaskRecord(token => Feature.RunFullCreation(token, Configuration.UseMaxRetainerAmount
                                                                                    ? 10
                                                                                    : (uint)Configuration.RetainerAmount + 1, Configuration.RetainerClass, Configuration.QstClassJob), Name));
    }

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
            (IPCNames.vnavmesh, true),
            (IPCNames.Lifestream, true),
            (IPCNames.Questionable, true)
    ];

    public override void Draw()
    {
        configChanged = false;

        if (!QuestManager.IsQuestComplete(66196))
        {
            Widgets.Banner(BannerKind.Error, T("RetainersLocked"));
            return;
        }

        if (!Configuration.UseMaxRetainerAmount && MaxRetainerEntitlement() == 0)
            Widgets.Banner(BannerKind.Warning, T("InteractWithVocate"), title: T("CantReadMaxRetainers"));

        Widgets.Grid(2, 12f,
        [
            width => Widgets.Card(T("Appearance"), DrawAppearance, width),
            width => Widgets.Card(T("CityAndClass"), DrawCityAndClass, width),
            width => Widgets.Card(T("Amount"), DrawAmount, width),
            width => Widgets.Card(T("Ventures"), DrawVentures, width)
        ]);

        DrawNamesCard();
        if (!Configuration.UseMaxRetainerAmount) DrawDetailsCard();

        if (configChanged) SaveConfig(Configuration);
    }

    private void DrawAppearance()
    {
        var labelWidth = LabelWidth(T("Race"), T("Gender"), T("Personality"));
        using (Widgets.Disabled(!Configuration.UseMaxRetainerAmount))
        {
            Label(T("Race"), labelWidth);
            ImGui.SetNextItemWidth(Widgets.Avail);
            configChanged |= EnumCombo("##retainerRace", ref Configuration.RetainerRace);

            Label(T("Gender"), labelWidth);
            configChanged |= Widgets.Segmented("retainerGender", ref Configuration.RetainerGender,
                                               [
                                                   (RetainerDetails.RetainerGender.Male, T("Male")),
                                                   (RetainerDetails.RetainerGender.Female, T("Female"))
                                               ], Widgets.Avail);

            Label(T("Personality"), labelWidth);
            ImGui.SetNextItemWidth(Widgets.Avail);
            configChanged |= EnumCombo("##retainerPersonality", ref Configuration.RetainerPersonality);
        }

        if (!Configuration.UseMaxRetainerAmount) Widgets.TextMuted(T("AppearancePerRetainer"), true);
    }

    private void DrawCityAndClass()
    {
        var labelWidth = LabelWidth(T("City"), T("RetainerClass"), T("QuestClass"));

        Label(T("City"), labelWidth);
        configChanged |= Widgets.Segmented("retainerCity", ref Configuration.RetainerCity,
                                           [
                                               (NpcDatabase.StarterCity.LimsaLominsa, T("CityLimsaLominsa")),
                                               (NpcDatabase.StarterCity.Gridania, T("CityGridania")),
                                               (NpcDatabase.StarterCity.Uldah, T("CityUldah"))
                                           ], Widgets.Avail, V2Palette.Accent);

        Label(T("RetainerClass"), labelWidth);
        ImGui.SetNextItemWidth(Widgets.Avail);
        if (ExcelSheetCombo<ClassJob>("##retainerJob", out var selected, s => s.GetRowOrDefault(Configuration.RetainerClass) is { } row
                                                                                  ? row.Abbreviation.ExtractText()
                                                                                  : string.Empty, x => x.Abbreviation.ExtractText(),
                                      x => x.RowId is >= 1 and <= 7 or >= 16 and <= 18 or 26))
        {
            Configuration.RetainerClass = selected.RowId;
            configChanged               = true;
        }

        Label(T("QuestClass"), labelWidth);
        ImGui.SetNextItemWidth(Widgets.Avail);
        if (ExcelSheetCombo<ClassJob>("##qstCombatJob", out var classJobSheet, s => s.GetRowOrDefault(Configuration.QstClassJob) is { } row
                                                                                        ? row.Abbreviation.ExtractText()
                                                                                        : string.Empty,
                                      x => x.Abbreviation.ExtractText(), x => x.RowId is >= 1 and <= 7 or >= 19 and <= 42))
        {
            Configuration.QstClassJob = classJobSheet.RowId;
            configChanged             = true;
        }
    }

    private void DrawAmount()
    {
        configChanged |= Widgets.SwitchRow(T("FillAllSlots"), ref Configuration.UseMaxRetainerAmount);

        using (Widgets.Disabled(Configuration.UseMaxRetainerAmount))
        {
            Label(T("RetainerAmount"), LabelWidth(T("RetainerAmount")));
            var amount = Configuration.RetainerAmount + 1;
            if (Widgets.Stepper("retainerAmount", ref amount, 1, Configuration.RetainerCharacters.Length))
            {
                Configuration.RetainerAmount = amount - 1;
                configChanged                = true;
            }
        }
    }

    private void DrawVentures() => configChanged |= Widgets.SwitchRow(T("AssignExploration"), ref Configuration.SendOnFirstExploration, T("AssignExplorationDescription"));

    private void DrawNamesCard()
    {
        var slots = Configuration.RetainerCharacters.Length;
        var used = Configuration.UseMaxRetainerAmount
                           ? slots
                           : Configuration.RetainerAmount + 1;

        Widgets.Card(T("RetainerNames"), () =>
                                         {
                                             if (Configuration.UseMaxRetainerAmount) Widgets.TextMuted(T("NamesRandom"), true);
                                             Widgets.Grid(5, 8f, Enumerable.Range(0, slots)
                                                                           .Select<int, Action<float>>(index => width => DrawNameSlot(index, width))
                                                                           .ToList());
                                         }, 0f, () => Widgets.TextMuted(string.Format(T("SlotsUsed"), used, slots)));
    }

    private void DrawNameSlot(int index, float width)
    {
        var retainer = Configuration.RetainerCharacters[index];
        using (Widgets.Disabled(Configuration.UseMaxRetainerAmount || index > Configuration.RetainerAmount))
        {
            var oldName = retainer.Name;
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputTextWithHint($"##newFirstName{index}", T("Random"), ref retainer.Name, 20))
            {
                var duplicate = Configuration.RetainerCharacters
                                             .Where((name, idx) => idx != index)
                                             .Any(name => name.Name == Configuration.RetainerCharacters[index].Name);
                if (duplicate)
                    retainer.Name = oldName;
                else
                    configChanged = true;
            }
        }
    }

    private void DrawDetailsCard()
    {
        Widgets.Card(T("RetainerDetails"), () =>
                                           {
                                               using var table = ImRaii.Table("##RetainerVocateTable", 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp,
                                                                              new Vector2(Widgets.Avail, 0));
                                               if (!table.Success) return;

                                               ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24 * GlobalFontScale);
                                               ImGui.TableSetupColumn(T("ColName").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1.4f);
                                               ImGui.TableSetupColumn(T("ColRace").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1f);
                                               ImGui.TableSetupColumn(T("ColGender").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 0.8f);
                                               ImGui.TableSetupColumn(T("ColClan").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1f);
                                               ImGui.TableSetupColumn(T("ColPersonality").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1f);
                                               ImGui.TableSetupColumn(T("ColPreset").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 0.8f);
                                               using (ImRaii.PushColor(ImGuiCol.Text, V2Palette.TextMuted))
                                               {
                                                   ImGui.TableHeadersRow();
                                               }

                                               for (var index = 0; index <= Configuration.RetainerAmount; index++)
                                               {
                                                   var retainer = Configuration.RetainerCharacters[index];
                                                   ImGui.TableNextRow();
                                                   ImGui.TableNextColumn();
                                                   ImGui.AlignTextToFramePadding();
                                                   Widgets.TextMuted((index + 1).ToString());
                                                   ImGui.TableNextColumn();
                                                   ImGui.AlignTextToFramePadding();
                                                   if (string.IsNullOrEmpty(retainer.Name))
                                                       Widgets.TextMuted(T("Random"));
                                                   else
                                                       ImGui.TextUnformatted(retainer.Name);
                                                   ImGui.TableNextColumn();
                                                   ImGui.SetNextItemWidth(-1);
                                                   configChanged |= EnumCombo($"##retainerRace{index}", ref retainer.Race);
                                                   ImGui.TableNextColumn();
                                                   ImGui.SetNextItemWidth(-1);
                                                   configChanged |= EnumCombo($"##retainerGender{index}", ref retainer.Gender);
                                                   ImGui.TableNextColumn();
                                                   ImGui.SetNextItemWidth(-1);
                                                   configChanged |= ClanCombo($"##retainerTribe{index}", retainer.Race - 10, ref retainer.Clan);
                                                   ImGui.TableNextColumn();
                                                   ImGui.SetNextItemWidth(-1);
                                                   configChanged |= EnumCombo($"##retainerPersonality{index}", ref retainer.Personality);
                                                   ImGui.TableNextColumn();
                                                   ImGui.SetNextItemWidth(-1);
                                                   configChanged |= PresetCombo($"##preset{index}", ref retainer.PresetId);
                                               }
                                           });
    }

    private void CreateRetainers()
    {
        if (IsPluginBusy) return;

        TryStartTask(new TaskRecord(Feature.GoToRetainerVocate, "Go to Retainer Vocate"));
        if (Configuration.UseMaxRetainerAmount || MaxRetainerEntitlement() != 0)
        {
            TryStartTask(new TaskRecord(token => Feature.CreateRetainers(token, Configuration.UseMaxRetainerAmount
                                                                                        ? 10
                                                                                        : Configuration.RetainerAmount + 1), "Create Retainers"));
        }
    }

    private void RunVentureQuest()
    {
        if (Questionable.IsRunning.Invoke() || IsPluginBusy) return;

        ErrorIf(!ChangeToHighestGearsetForClassJobId(Configuration.QstClassJob), $"No gearset for {Configuration.QstClassJob} found!");
        if (!SubscriptionManager.IsLoaded(IPCNames.Questionable))
        {
            FullError("'Questionable' not available. Skipping Venture Quest and equipping Retainers.");
            return;
        }

        TryStartTask(new TaskRecord(token => Feature.StartVentureQuest(token, Configuration.QstClassJob), "Do Retainer Venture Quest"));
    }

    private void AssignClass()
    {
        if (MaxRetainerEntitlement() == 0)
            TryStartTask(new TaskRecord(Feature.GoToRetainerVocate, "Go to Retainer Vocate"));
        TryStartTask(new TaskRecord(token => Feature.BuyAndEquipRetainerGear(token, Configuration.UseMaxRetainerAmount
                                                                                            ? 10
                                                                                            : (uint)Configuration.RetainerAmount + 1, Configuration.UseMaxRetainerAmount
                                                                                                                                              ? Configuration.RetainerClass
                                                                                                                                              : 0), "Buy and Equip Retainer Gear"));
    }

    private static unsafe byte MaxRetainerEntitlement() => RetainerManager.Instance()->MaxRetainerEntitlement;

    private static unsafe bool HasFreeRetainerSlot() => RetainerManager.Instance()->MaxRetainerEntitlement - RetainerManager.Instance()->GetRetainerCount() > 0;

    private static float LabelWidth(params string[] labels) => Math.Max(90 * GlobalFontScale, labels.Max(label => ImGui.CalcTextSize(label).X) + 12 * GlobalFontScale);

    private static void Label(string text, float width)
    {
        var x = ImGui.GetCursorScreenPos().X;
        ImGui.AlignTextToFramePadding();
        Widgets.TextDim(text);
        ImGui.SameLine();
        ImGui.SetCursorScreenPos(new Vector2(x + width, ImGui.GetCursorScreenPos().Y));
    }

    private unsafe bool PresetCombo(string id, ref (byte DenseIndex, byte RealIndex) presetId)
    {
        var presets = Framework.Instance()->CharamakeAvatarSaveData->Release.Slots.ToArray()
                                                                            .Where(x => x.Timestamp > 0)
                                                                            .OrderBy(x => x.SlotIndex)
                                                                            .ToArray();

        var realIndices = presets.Select(p => p.SlotIndex)
                                 .ToArray();

        var denseIds = Enumerable.Range(0, realIndices.Length)
                                 .Select(i => (byte)i)
                                 .ToList();

        denseIds.Insert(0, 255);

        var denseToReal = new Dictionary<byte, byte>();
        denseToReal[255] = 255;
        for (byte i = 0; i < realIndices.Length; i++)
            denseToReal[i] = realIndices[i];

        var names = new Dictionary<byte, string>();
        names[255] = T("None");
        for (byte i = 0; i < realIndices.Length; i++)
        {
            var real = realIndices[i];
            var label = presets.First(p => p.SlotIndex == real)
                               .LabelString;
            names[i] = $"{real} - {label}";
        }

        var realIndex = presetId.RealIndex;
        var denseSelected =
                realIndex == 255
                        ? (byte)255
                        : denseToReal.First(x => x.Value == realIndex)
                                     .Key;

        if (!Combo(id, ref denseSelected, denseIds, names: names)) return false;

        presetId = denseSelected == 255
                           ? ((byte)255, (byte)255)
                           : (denseSelected, denseToReal[denseSelected]);
        return true;
    }

    private static bool ClanCombo(string id, RetainerDetails.RetainerRace race, ref int clan)
    {
        var raceIndex = (int)race; // 0-7

        if (raceIndex < 0 || raceIndex > 7)
            return false;

        clan = Math.Clamp(clan, 1, 2);

        var tribeSheet    = Svc.Data.GetExcelSheet<Tribe>();
        var firstTribeRow = (raceIndex * 2) + 1;

        var names = new Dictionary<int, string>
                    {
                            [1] = tribeSheet.GetRow((uint)firstTribeRow)
                                            .Masculine.ExtractText(),
                            [2] = tribeSheet.GetRow((uint)(firstTribeRow + 1))
                                            .Masculine.ExtractText()
                    };

        return Combo(id, ref clan, [1, 2], names: names);
    }
}
