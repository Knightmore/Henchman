using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
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
            ("日本語", UiLanguage.Japanese, "ja"),
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

        ImGui.Text(T("UILanguage"));
        ImGui.SameLine(240          * GlobalFontScale);
        ImGui.SetNextItemWidth(120f * GlobalFontScale);
        var availableLanguages = GetAvailableLanguages();
        if (availableLanguages.Length == 0)
        {
            ImGui.TextDisabled("No languages found");
            if (configChanged) PluginConfig.Save();
            return;
        }

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
                    P.MainWindow.RebuildSidebar();
                    configChanged = true;
                }

                if (selected) ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.NewLine();
        ImGui.Separator();
        ImGui.NewLine();

        configChanged |= ImGui.Checkbox($"{T("UseMount")}##useMount", ref C.UseMount);
        configChanged |= ImGui.Checkbox($"{T("UseMountRoulette")}##useMountRoulette", ref C.UseMountRoulette);
        ImGui.Text(T("Mount"));
        ImGui.SameLine(240          * GlobalFontScale);
        ImGui.SetNextItemWidth(150f * GlobalFontScale);
        if (ExcelSheetCombo<Mount>("##mount", out var selectedMount, s => s.GetRowOrDefault(C.MountId) is { } row
                                                                                  ? ToTitleCaseExtended(row
                                                                                                       .Singular.ExtractText(), Svc.ClientState.ClientLanguage)
                                                                                  : string.Empty, x => ToTitleCaseExtended(x.Singular.ExtractText(), Svc.ClientState.ClientLanguage), x => PlayerState.Instance()->IsMountUnlocked(x.RowId)))
        {
            C.MountId     = selectedMount.RowId;
            configChanged = true;
        }

        ImGui.Text(T("MountDistance"));
        ImGui.SameLine(240          * GlobalFontScale);
        ImGui.SetNextItemWidth(120f * GlobalFontScale);
        configChanged |= ImGui.InputInt("##mountForDistance", ref C.MinMountDistance);

        ImGui.Text(T("RunDistance"));
        ImGui.SameLine(240          * GlobalFontScale);
        ImGui.SetNextItemWidth(120f * GlobalFontScale);
        configChanged |= ImGui.InputInt("##runForDistance", ref C.MinRunDistance);

        configChanged |= ImGui.Checkbox($"{T("ReturnOnceDone")}##returnOnceDone", ref C.ReturnOnceDone);
        ImGui.SameLine(240          * GlobalFontScale);
        ImGui.SetNextItemWidth(120f * GlobalFontScale);
        configChanged |= EnumCombo("##returnDestination", ref C.ReturnTo);

        ImGui.Text(T("AutoRotationPlugin"));
        ImGui.SameLine(240          * GlobalFontScale);
        ImGui.SetNextItemWidth(120f * GlobalFontScale);
        configChanged |= Combo("##AutoRotation", ref C.AutoRotationPlugin, AutoRotation.SupportedPlugins, null, AutoRotation.DisplayNames);
        configChanged |= ImGui.Checkbox($"{T("UseChocoboCompanion")}##useChocoboCompanion", ref C.UseChocoboInFights);
        ImGui.SameLine();
        HelpMarker(T("ChocoboCompanionHelp"));

        if (configChanged) PluginConfig.Save();
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
