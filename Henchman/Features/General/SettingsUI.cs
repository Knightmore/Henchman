using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Underlings.Configuration;
using Underlings.Modules;
using Action = System.Action;

namespace Henchman.Features.General;

[Module]
internal class SettingsUI : ModuleUI
{
    private static readonly (string Label, UiLanguage Lang, string Folder)[] Languages =
    [
            ("English", UiLanguage.English, "en"),
            ("Deutsch", UiLanguage.German, "de"),
            ("Français", UiLanguage.French, "fr"),
            ("日本語", UiLanguage.Japanese, "jp"),
            ("한국어", UiLanguage.Korean, "ko"),
            ("简体中文", UiLanguage.Chinese, "zh"),
            ("繁體中文", UiLanguage.Taiwanese, "tw")
    ];

    public override string          Name     => "Settings";
    public override Enum            Category => Henchman.Category.System;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Cog;

    public override Action Help => () => { ImGui.Text(T("HelpText")); };

    public override bool LoginNeeded => false;

    public override unsafe void Draw()
    {
        var configChanged = false;

        Widgets.Grid(2, 12f,
        [
                width => Widgets.Card(T("SectionInterface"), () => Widgets.FieldRow(Label("UILanguage"), () => configChanged |= DrawLanguageCombo()), width),
                width => Widgets.Card(T("SectionTravel"), () =>
                                                          {
                                                              configChanged |= Widgets.SwitchRow($"{T("UseMount")}##useMount", ref C.UseMount);
                                                              configChanged |= Widgets.SwitchRow($"{T("UseMountRoulette")}##useMountRoulette", ref C.UseMountRoulette, child: true,
                                                                                                 enabled: C.UseMount);
                                                              Widgets.FieldRow(Label("Mount"), () =>
                                                                                               {
                                                                                                   if (ExcelSheetCombo<Mount>("##mount", out var selectedMount, s => s.GetRowOrDefault(C.MountId) is { } row
                                                                                                                                                                       ? ToTitleCaseExtended(row
                                                                                                                                                                                            .Singular.ExtractText(), Svc.ClientState.ClientLanguage)
                                                                                                                                                                       : string.Empty, x => ToTitleCaseExtended(x.Singular.ExtractText(), Svc.ClientState.ClientLanguage), x => PlayerState.Instance()->IsMountUnlocked(x.RowId)))
                                                                                                   {
                                                                                                       C.MountId     = selectedMount.RowId;
                                                                                                       configChanged = true;
                                                                                                   }
                                                                                               });
                                                              Widgets.FieldRow(Label("MountDistance"), () => configChanged |= ImGui.InputInt("##mountForDistance", ref C.MinMountDistance), 140f);
                                                              Widgets.FieldRow(Label("RunDistance"), () => configChanged |= ImGui.InputInt("##runForDistance", ref C.MinRunDistance), 140f);
                                                          }, width),
                width => Widgets.Card(T("SectionFinish"), () =>
                                                          {
                                                              configChanged |= Widgets.SwitchRow($"{Label("ReturnOnceDone")}##returnOnceDone", ref C.ReturnOnceDone);
                                                              using (Widgets.Disabled(!C.ReturnOnceDone))
                                                              {
                                                                  Widgets.FieldRow(T("ReturnDestination"), () => configChanged |= EnumCombo("##returnDestination", ref C.ReturnTo), 160f);
                                                              }
                                                          }, width),
                width => Widgets.Card(T("SectionCombat"), () =>
                                                          {
                                                              Widgets.FieldRow(Label("AutoRotationPlugin"),
                                                                               () => configChanged |= Combo("##AutoRotation", ref C.AutoRotationPlugin, AutoRotation.SupportedPlugins, null,
                                                                                                            AutoRotation.DisplayNames), 160f);
                                                              configChanged |= Widgets.SwitchRow($"{T("UseChocoboCompanion")}##useChocoboCompanion", ref C.UseChocoboInFights,
                                                                                                 T("ChocoboCompanionHelp"));
                                                          }, width)
        ]);

        if (configChanged) PluginConfig.Save();
    }

    private string Label(string key) => T(key).TrimEnd(':', ' ');

    private static bool DrawLanguageCombo()
    {
        var availableLanguages = GetAvailableLanguages();
        if (availableLanguages.Length == 0)
        {
            ImGui.TextDisabled("No languages found");
            return false;
        }

        var changed                    = false;
        var currentIdx                 = Array.FindIndex(availableLanguages, l => l.Lang == C.UILanguage);
        if (currentIdx < 0) currentIdx = 0;
        if (ImGui.BeginCombo("##uiLanguage", availableLanguages[currentIdx].Label))
        {
            for (var i = 0; i < availableLanguages.Length; i++)
            {
                var selected = i == currentIdx;
                if (ImGui.Selectable(availableLanguages[i].Label, selected))
                {
                    C.UILanguage = availableLanguages[i].Lang;
                    Loc.Load(MapLanguage(C.UILanguage));
                    changed = true;
                }

                if (selected) ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private static (string Label, UiLanguage Lang, string Folder)[] GetAvailableLanguages()
    {
        var result          = new List<(string Label, UiLanguage Lang, string Folder)>();
        var localizationDir = Path.Combine(Svc.PluginInterface.AssemblyLocation.Directory!.FullName, "Localization");

        foreach (var language in Languages)
        {
            if (Directory.Exists(Path.Combine(localizationDir, language.Folder)))
                result.Add(language);
        }

        return result.ToArray();
    }
}
