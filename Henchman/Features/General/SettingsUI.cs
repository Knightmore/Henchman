using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Abstractions;
using Lumina.Excel.Sheets;
using System.IO;
using Action = System.Action;

namespace Henchman.Features.General;

[Feature]
internal class SettingsUI : FeatureUI
{
    public Dictionary<string, string> AutoRotationCorrectedNames = new()
                                                                   {
                                                                           { IPCNames.BossMod, IPCNames.BossMod },
                                                                           { IPCNames.RotationSolverReborn, "RotationSolverReborn" },
                                                                           { IPCNames.Wrath, IPCNames.Wrath }
                                                                   };

    public List<string> AutoRotationPlugins =
    [
            IPCNames.BossMod,
            IPCNames.RotationSolverReborn,
            IPCNames.Wrath
    ];

    private static readonly (string Label, ClientLanguage Lang, string Folder)[] Languages =
    [
        ("English", ClientLanguage.English, "en"),
        ("Deutsch", ClientLanguage.German, "de"),
        ("Français", ClientLanguage.French, "fr"),
        ("日本語", ClientLanguage.Japanese, "jp"),
    ];

    public override string Name => "Settings";
    public override Category Category => Category.System;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Cog;

    public override Action Help => () => { ImGui.Text(T("HelpText")); };

    public override bool LoginNeeded => false;

    public override unsafe void Draw()
    {
        var configChanged = false;

        ImGui.Text(T("UILanguage"));
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(120f);
        var availableLanguages = GetAvailableLanguages();
        if (availableLanguages.Length == 0)
        {
            ImGui.TextDisabled("No languages found");
            if (configChanged) EzConfig.Save();
            return;
        }

        var currentIdx = Array.FindIndex(availableLanguages, l => l.Lang == C.UILanguage);
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
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(150f);
        if (ImGuiEx.ExcelSheetCombo<Mount>("##mount", out var selectedMount, s => s.GetRowOrDefault(C.MountId) is { } row
                                                                                          ? ToTitleCaseExtended(row
                                                                                                               .Singular.ExtractText(), Svc.ClientState.ClientLanguage)
                                                                                          : string.Empty, x => ToTitleCaseExtended(x.Singular.ExtractText(), Svc.ClientState.ClientLanguage), x => PlayerState.Instance()->IsMountUnlocked(x.RowId)))
        {
            C.MountId = selectedMount.RowId;
            configChanged = true;
        }

        ImGui.Text(T("MountDistance"));
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGui.InputInt("##mountForDistance", ref C.MinMountDistance);

        ImGui.Text(T("RunDistance"));
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGui.InputInt("##runForDistance", ref C.MinRunDistance);

        configChanged |= ImGui.Checkbox($"{T("ReturnOnceDone")}##returnOnceDone", ref C.ReturnOnceDone);
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGuiEx.EnumCombo("##returnDestination", ref C.ReturnTo);

        ImGui.Text(T("AutoRotationPlugin"));
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGuiEx.Combo("##AutoRotation", ref C.AutoRotationPlugin, AutoRotationPlugins, null, AutoRotationCorrectedNames);
        configChanged |= ImGui.Checkbox($"{T("UseChocoboCompanion")}##useChocoboCompanion", ref C.UseChocoboInFights);
        ImGui.SameLine();
        ImGuiEx.HelpMarker(T("ChocoboCompanionHelp"));

        if (configChanged) EzConfig.Save();
    }

    private static (string Label, ClientLanguage Lang, string Folder)[] GetAvailableLanguages()
    {
        var result = new List<(string Label, ClientLanguage Lang, string Folder)>();
        var localizationDir = Path.Combine(Svc.PluginInterface.AssemblyLocation.Directory!.FullName, "Localization");

        foreach (var language in Languages)
        {
            if (Directory.Exists(Path.Combine(localizationDir, language.Folder)))
                result.Add(language);
        }

        return result.ToArray();
    }
}
