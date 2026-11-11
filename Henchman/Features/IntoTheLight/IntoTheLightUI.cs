using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.Sheets;
using Underlings.Modules;
using Action = System.Action;

namespace Henchman.Features.IntoTheLight;

[Module]
public class IntoTheLightUI : ModuleUI<IntoTheLight, Configuration>
{
    public enum ClassJob
    {
        Gladiator,
        Pugilist,
        Marauder,
        Lancer,
        Archer,
        Conjurer,
        Thaumaturge,
        Arcanist
    }

    private readonly IntoTheLight feature = new();

    private readonly IEnumerable<int> values = Enumerable.Range(1, 40);
    private          LightCharacter?  charToDelete;
    private          bool             configChanged;

    private LightCharacter newCharacter = new();

    public IntoTheLightUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public sealed override required Configuration   Configuration { get; init; }
    public override                 string          Name          => "Into The Light";
    public override                 Enum            Category      => Henchman.Category.Exploration;
    public override                 FontAwesomeIcon Icon          => FontAwesomeIcon.PersonRays;

    public override Action? Help => () => { ImGui.Text(T("HelpText")); };

    public override bool LoginNeeded => false;

    public override ModuleAction PrimaryAction => new(Loc.G("UI.V2.Start", "Start"), Start);

    private void Start()
    {
        if (!IsTaskRunning(Name)) Feature.RunTask();
    }

    public override void Draw()
    {
        configChanged = false;

        Widgets.Columns(16f, (0f, () =>
                                  {
                                      Widgets.Banner(BannerKind.Info, T("TitleScreenNote"));
                                      DrawCharactersCard();
                                      Widgets.Card(null, () => configChanged |= Widgets.SwitchRow(T("NoLoginSkip"), ref Configuration.LightNoLoginSkip, T("NoLoginSkipHelp")));
                                  }),
                        (262f, () => Widgets.RequirementsCard(this)));

        if (charToDelete != null)
        {
            Configuration.LightCharacters.Remove(charToDelete);
            charToDelete  = null;
            configChanged = true;
        }

        if (configChanged) SaveConfig(Configuration);
    }

    private void DrawCharactersCard()
    {
        Widgets.Card(T("CharactersToCreate"), () =>
                                              {
                                                  using var table = ImRaii.Table("##LightCharacterTable", 8, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp,
                                                                                 new Vector2(Widgets.Avail, 0));
                                                  if (!table.Success) return;

                                                  ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24 * GlobalFontScale);
                                                  ImGui.TableSetupColumn(T("ColFirstName").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1.2f);
                                                  ImGui.TableSetupColumn(T("ColLastName").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1.2f);
                                                  ImGui.TableSetupColumn(T("ColDataCenter").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1f);
                                                  ImGui.TableSetupColumn(T("ColWorld").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1f);
                                                  ImGui.TableSetupColumn(T("ColClassJob").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 1f);
                                                  ImGui.TableSetupColumn(T("ColPreset").ToUpperInvariant(), ImGuiTableColumnFlags.WidthStretch, 0.8f);
                                                  ImGui.TableSetupColumn("##actions", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
                                                  using (ImRaii.PushColor(ImGuiCol.Text, V2Palette.TextMuted))
                                                  {
                                                      ImGui.TableHeadersRow();
                                                  }

                                                  for (var index = 0; index < Configuration.LightCharacters.Count; index++)
                                                  {
                                                      var character = Configuration.LightCharacters[index];
                                                      ImGui.TableNextRow();
                                                      Cell((index + 1).ToString(), V2Palette.TextMuted);
                                                      NameCell(character.FirstName);
                                                      NameCell(character.LastName);
                                                      Cell(Svc.Data.GetExcelSheet<WorldDCGroupType>()
                                                              .GetRow(character.DataCenterId)
                                                              .Name.ExtractText(), V2Palette.Text);
                                                      Cell(Svc.Data.GetExcelSheet<World>()
                                                              .GetRow(character.WorldId)
                                                              .Name.ExtractText(), V2Palette.Text);
                                                      Cell(character.ClassJob.ToString(), V2Palette.Text);
                                                      Cell(PresetName(character.PresetId), V2Palette.TextDim);
                                                      ImGui.TableNextColumn();
                                                      if (Widgets.IconButton($"LightRemove{index}", FontAwesomeIcon.Trash, size: ImGui.GetFrameHeight() / Widgets.Scale)) charToDelete = character;
                                                  }

                                                  DrawNewCharacterRow();
                                              });
    }

    private void DrawNewCharacterRow()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##newFirstName", T("Random"), ref newCharacter.FirstName, newCharacter is { LastName.Length: > 0 }
                                                                                               ? Math.Min(15, 20 - newCharacter.LastName.Length)
                                                                                               : 15);
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##newLastName", T("Random"), ref newCharacter.LastName, newCharacter is { FirstName.Length: > 0 }
                                                                                             ? Math.Min(15, 20 - newCharacter.FirstName.Length)
                                                                                             : 15);
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        // World.DataCenter retains the old Japanese grouping; Unknown0 has the current data center ID.
        if (ExcelSheetCombo<WorldDCGroupType>("##newdc", out var selectedDC, s => s.GetRowOrDefault(newCharacter.DataCenterId) is { } row
                                                                                          ? row.Name.ExtractText()
                                                                                          : string.Empty, x => x.Name.ExtractText(), x => x.RowId is > 0 and < 12 && Svc.Data.GetExcelSheet<World>().Any(y => y.Unknown0 == x.RowId && y.IsPublic)))
        {
            newCharacter.DataCenterId = selectedDC.RowId;
            newCharacter.WorldId = Svc.Data
                                      .GetExcelSheet<World>()
                                      .First(x => x.Unknown0 == selectedDC.RowId && x.IsPublic)
                                      .RowId;
        }

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        if (ExcelSheetCombo<World>("##newworld", out var selectedWorld, s => s.GetRowOrDefault(newCharacter.WorldId) is { } row
                                                                                     ? row.Name.ExtractText()
                                                                                     : string.Empty, y => y.Name.ExtractText(), y => y.IsPublic && y.Unknown0 == newCharacter.DataCenterId))
            newCharacter.WorldId = selectedWorld.RowId;
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        EnumCombo("##newclassJob", ref newCharacter.ClassJob);
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        configChanged |= PresetCombo("##newpreset", ref newCharacter.PresetId);
        ImGui.TableNextColumn();

        var randomFirstName = string.IsNullOrEmpty(newCharacter.FirstName);
        var randomLastName  = string.IsNullOrEmpty(newCharacter.LastName);
        if (Widgets.IconButton("LightAdd", FontAwesomeIcon.Plus, null, ButtonKind.Secondary, ImGui.GetFrameHeight() / Widgets.Scale) &&
            !Configuration.LightCharacters.Any(x => !randomFirstName && x.FirstName == newCharacter.FirstName && !randomLastName && x.LastName == newCharacter.LastName && x.WorldId == newCharacter.WorldId))
        {
            Configuration.LightCharacters.Add(newCharacter);
            var tempCharacter = new LightCharacter(newCharacter);
            tempCharacter.FirstName = "";
            tempCharacter.LastName  = "";
            newCharacter            = tempCharacter;
            configChanged           = true;
        }
    }

    private static void Cell(string text, Vector4 color)
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        Widgets.TextColored(color, text);
    }

    private void NameCell(string name) => Cell(string.IsNullOrEmpty(name)
                                                       ? T("Random")
                                                       : name, string.IsNullOrEmpty(name)
                                                                       ? V2Palette.TextMuted
                                                                       : V2Palette.Text);

    private unsafe string PresetName((byte DenseIndex, byte RealIndex) presetId) => presetId.RealIndex == 255
                                                                                           ? T("None")
                                                                                           : $"{Framework.Instance()->CharamakeAvatarSaveData->Release.Slots[presetId.RealIndex].SlotIndex} - {Framework.Instance()->CharamakeAvatarSaveData->Release.Slots[presetId.RealIndex].LabelString}";

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
}
