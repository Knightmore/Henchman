using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Helpers;
using Lumina.Excel.Sheets;
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

    public override string          Name     => "Settings";
    public override string          Category => Henchman.Category.System;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Cog;

    public override Action Help => () => { ImGui.Text("General Setting used through all implemented features."); };

    public override bool LoginNeeded => false;

    public override unsafe void Draw()
    {
        var configChanged = false;
        configChanged |= ImGui.Checkbox("Use Mount##useMount", ref C.UseMount);
        configChanged |= ImGui.Checkbox("Use Mount Roulette##useMountRoulette", ref C.UseMountRoulette);
        ImGui.Text("Mount");
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(150f);
        if (ImGuiEx.ExcelSheetCombo<Mount>("##mount", out var selectedMount, s => s.GetRowOrDefault(C.MountId) is { } row
                                                                                          ? Utils.ToTitleCaseExtended(row
                                                                                                                     .Singular.ExtractText(), Svc.ClientState.ClientLanguage)
                                                                                          : string.Empty, x => Utils.ToTitleCaseExtended(x.Singular.ExtractText(), Svc.ClientState.ClientLanguage), x => PlayerState.Instance()->IsMountUnlocked(x.RowId)))
        {
            C.MountId     = selectedMount.RowId;
            configChanged = true;
        }

        ImGui.Text("Mount when distance greater than:");
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGui.InputInt("##mountForDistance", ref C.MinMountDistance);

        ImGui.Text("Run when distance greater than:");
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGui.InputInt("##runForDistance", ref C.MinRunDistance);

        configChanged |= ImGui.Checkbox("Return to once feature is finished:", ref C.ReturnOnceDone);
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGuiEx.EnumCombo("##returnDestination", ref C.ReturnTo);

        ImGui.Text("Auto Rotation Plugin:");
        ImGui.SameLine(240);
        ImGui.SetNextItemWidth(120f);
        configChanged |= ImGuiEx.Combo("##AutoRotation", ref C.AutoRotationPlugin, AutoRotationPlugins, null, AutoRotationCorrectedNames);
        configChanged |= ImGui.Checkbox("Use Chocobo Companion##useChocoboCompanion", ref C.UseChocoboInFights);
        ImGui.SameLine();
        ImGuiEx.HelpMarker("Summon your Chocobo Companion or feed it if the remaining time is less than 5 minutes.");

        ImGui.NewLine();
        ImGui.Separator();
        ImGui.NewLine();

        if (configChanged) EzConfig.Save();
    }
}
