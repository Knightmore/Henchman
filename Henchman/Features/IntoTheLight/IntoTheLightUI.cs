using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Henchman.Abstractions;
using Henchman.Windows.Layout;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Henchman.Features.IntoTheLight;

[Feature]
public class IntoTheLightUI : FeatureUI<IntoTheLight, Configuration>
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

    private LightCharacter newCharacter = new();

    public IntoTheLightUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public sealed override required Configuration   Configuration { get; init; }
    public override                 string          Name          => "Into The Light";
    public override                 Category        Category      => Category.Exploration;
    public override                 FontAwesomeIcon Icon          => FontAwesomeIcon.PersonRays;

    public override Action? Help => () => { ImGui.Text(T("HelpText")); };

    public override bool LoginNeeded => false;

    public override unsafe void Draw()
    {
        var configChanged = false;
        DrawCentered("##LightStart", () =>
                                     {
                                         Layout.DrawButton(() =>
                                                           {
                                                               if (ImGui.Button(T("Start"), new Vector2(70 * GlobalFontScale, 30 * GlobalFontScale)) && !IsTaskEnqueued(Name)) Feature.RunTask();
                                                           });
                                     });
        DrawCentered("##LightNoLoginSkip", () =>
                                           {
                                               ImGui.Checkbox(T("NoLoginSkip"), ref Configuration.LightNoLoginSkip);
                                               HelpMarker(() => ImGui.Text(T("NoLoginSkipHelp")), sameLine: true);
                                           });


        var characterColumns = new List<TableColumn<LightCharacter>>
                               {
                                       new("", Width: 30, DrawCustom: (x, index) =>
                                                                      {
                                                                          if (ImGuiComponents.IconButton($"##Light{x.GetHashCode()}Remove", FontAwesomeIcon.Trash)) charToDelete = x;
                                                                      }),
                                       new(T("ColFirstName"), x => string.IsNullOrEmpty(x.FirstName)
                                                                      ? T("Random")
                                                                      : x.FirstName, 110, Alignment: ColumnAlignment.Center),
                                       new(T("ColLastName"), x => string.IsNullOrEmpty(x.LastName)
                                                                     ? T("Random")
                                                                     : x.LastName, 110, Alignment: ColumnAlignment.Center),
                                       new(T("ColDataCenter"), x => Svc.Data.GetExcelSheet<WorldDCGroupType>()
                                                                  .GetRow(x.DataCenterId)
                                                                  .Name.ExtractText(), 100, Alignment: ColumnAlignment.Center),
                                       new(T("ColWorld"), x => Svc.Data.GetExcelSheet<World>()
                                                            .GetRow(x.WorldId)
                                                            .Name.ExtractText(), 100, Alignment: ColumnAlignment.Center),
                                       new(T("ColClassJob"), x => x.ClassJob.ToString(), 100, Alignment: ColumnAlignment.Center),
                                       new(T("ColPreset"), x => x.PresetId.RealIndex == 255
                                                                  ? T("None")
                                                                  : $"{Framework.Instance()->CharamakeAvatarSaveData->Release.Slots[x.PresetId.RealIndex].SlotIndex} - {Framework.Instance()->CharamakeAvatarSaveData->Release.Slots[x.PresetId.RealIndex].LabelString}", 130, Alignment: ColumnAlignment.Center)
                               };

        var table = new Table<LightCharacter>(
                                              "##LightCharacterTable",
                                              characterColumns,
                                              () => Configuration.LightCharacters,
                                              size: new Vector2(750, 0),
                                              drawExtraRow: () =>
                                                            {
                                                                ImGui.TableNextRow();
                                                                using var row = new ColumnScope(characterColumns.Count);
                                                                row.TableNextColumn();
                                                                var randomFirstName = string.IsNullOrEmpty(newCharacter.FirstName);
                                                                var randomLastName  = string.IsNullOrEmpty(newCharacter.LastName);

                                                                if (ImGuiComponents.IconButton("##LightAdd", FontAwesomeIcon.Plus) && !Configuration.LightCharacters.Any(x => !randomFirstName && x.FirstName == newCharacter.FirstName && !randomLastName && x.LastName == newCharacter.LastName && x.WorldId == newCharacter.WorldId))
                                                                {
                                                                    Configuration.LightCharacters.Add(newCharacter);
                                                                    var tempCharacter = new LightCharacter(newCharacter);
                                                                    tempCharacter.FirstName = "";
                                                                    tempCharacter.LastName  = "";
                                                                    newCharacter            = tempCharacter;
                                                                    configChanged           = true;
                                                                }

                                                                row.TableNextColumn();
                                                                ImGui.SetNextItemWidth(110f);
                                                                ImGui.InputText($"##newFirstName", ref newCharacter.FirstName, newCharacter is { LastName.Length: > 0 }
                                                                                                                                       ? Math.Min(15, 20 - newCharacter.LastName.Length)
                                                                                                                                       : 15);
                                                                row.TableNextColumn();
                                                                ImGui.SetNextItemWidth(110f);
                                                                ImGui.InputText($"##newLastName", ref newCharacter.LastName, newCharacter is { FirstName.Length: > 0 }
                                                                                                                                     ? Math.Min(15, 20 - newCharacter.FirstName.Length)
                                                                                                                                     : 15);
                                                                row.TableNextColumn();
                                                                ImGui.SetNextItemWidth(100f);
                                                                if (ImGuiEx.ExcelSheetCombo<WorldDCGroupType>("##newdc", out var selectedDC, s => s.GetRowOrDefault(newCharacter.DataCenterId) is { } row
                                                                                                                                                          ? row.Name.ExtractText()
                                                                                                                                                          : string.Empty, x => x.Name.ExtractText(), x => x.RowId is > 0 and < 12))
                                                                {
                                                                    newCharacter.DataCenterId = selectedDC.RowId;
                                                                    newCharacter.WorldId = Svc.Data
                                                                                              .GetExcelSheet<World>()
                                                                                              .First(x => x.DataCenter.Value.RowId == selectedDC.RowId && x.IsPublic)
                                                                                              .RowId;
                                                                }

                                                                row.TableNextColumn();
                                                                ImGui.SetNextItemWidth(110f);
                                                                if (ImGuiEx.ExcelSheetCombo<World>("##newworld", out var selectedWorld, s => s.GetRowOrDefault(newCharacter.WorldId) is { } row
                                                                                                                                                     ? row.Name.ExtractText()
                                                                                                                                                     : string.Empty, y => y.Name.ExtractText(), y => y.IsPublic && y.DataCenter.RowId == newCharacter.DataCenterId))
                                                                    newCharacter.WorldId = selectedWorld.RowId;
                                                                row.TableNextColumn();
                                                                ImGui.SetNextItemWidth(100f);
                                                                ImGuiEx.EnumCombo("##newclassJob", ref newCharacter.ClassJob);

                                                                row.TableNextColumn();

                                                                var presetId = newCharacter.PresetId;

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

                                                                var denseSelected =
                                                                        presetId.RealIndex == 255
                                                                                ? (byte)255
                                                                                : denseToReal.First(x => x.Value == presetId.RealIndex)
                                                                                             .Key;

                                                                ImGui.SetNextItemWidth(130f);
                                                                if (ImGuiEx.Combo("##newpreset", ref denseSelected, denseIds, names: names))
                                                                {
                                                                    newCharacter.PresetId = denseSelected == 255
                                                                                                    ? ((byte)255, (byte)255)
                                                                                                    : (denseSelected, denseToReal[denseSelected]);


                                                                    configChanged = true;
                                                                }

                                                                /*var presetId = newCharacter.PresetId;

                                                                var presets = Framework.Instance()->CharamakeAvatarSaveData->Release.Slots
                                                                                                                                    .ToArray()
                                                                                                                                    .Where(x => x.Timestamp > 0)
                                                                                                                                    .ToArray();

                                                                var presetIds = new[] { (byte)255 }
                                                                       .Concat(presets.Select(x => x.SlotIndex));

                                                                var names = presets.ToDictionary(
                                                                                                 y => y.SlotIndex,
                                                                                                 y => $"{y.SlotIndex} - {y.LabelString}"
                                                                                                );
                                                                names[255] = "None";

                                                                ImGui.SetNextItemWidth(130f);
                                                                if (ImGuiEx.Combo("##newpreset", ref presetId, presetIds, names: names))
                                                                {
                                                                    newCharacter.PresetId = presetId;
                                                                    configChanged = true;
                                                                }*/
                                                            });

        DrawCentered("##LightTable", () => table.Draw());

        if (charToDelete != null)
        {
            Configuration.LightCharacters.Remove(charToDelete);
            charToDelete  = null;
            configChanged = true;
        }

        if (configChanged) SaveConfig(Configuration);
    }
}
