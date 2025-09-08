using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.Events;
using ECommons.ImGuiMethods;
using Henchman.Helpers;

namespace Henchman.Windows;

internal class FeatureWindow : Window, IDisposable
{
    public FeatureWindow() : base($"{P!.Name} - {P.GetType().Assembly.GetName().Version}##featureWindow")
    {
        SizeConstraints = new WindowSizeConstraints { MaximumSize = new Vector2(1920, 1080) };
        SizeCondition   = ImGuiCond.Always;
    }

    private FeatureUI? SelectedFeature =>
            FeatureSet.FirstOrDefault(t => t.Name == P!.SelectedFeatureName) ??
            GeneralSet.FirstOrDefault(t => t.Name      == P!.SelectedFeatureName) ??
            ExperimentalSet.FirstOrDefault(t => t.Name == P!.SelectedFeatureName);


    public void Dispose() { }

    public override void Draw()
    {
        var feature = SelectedFeature;
        if (feature == null)
        {
            ImGuiEx.TextCentered(ImGuiColors.DalamudRed, "ATTENTION");
            ImGuiEx.TextCentered("""
                                 The included features work within the limitations of vnavmesh and your unlocked ingame progress!
                                 There is no 'intelligent' pathing included (yet!?). 
                                 If you haven't unlocked all needed Territories/Aetherytes, certain features may stop at that task.
                                 """);
            ImGui.NewLine();
            ImGui.Separator();
            ImGui.NewLine();

            ImGuiEx.TextCentered(ImGuiColors.DalamudRed, "Positional Mob Data");
            ImGuiEx.TextCentered("""
                                 Due to the amount of positional data, it can happen, that not all positions are correct.
                                 If you want to report a problematic position, please also send the corrected position if possible.
                                 """);
            return;
        }

        using var id = ImRaii.PushId(feature.Name);

        if (Running && CurrentTaskRecord != null)
        {
            ImGuiEx.TextCentered("Running Task:");
            ImGuiEx.TextCentered(TaskName);
            ImGui.SameLine();
            ImGuiEx.HelpMarker($"""
                                Task Subroutine:

                                {(TaskDescription.Count == 0 ? "No Description" : string.Join("\n", TaskDescription))}
                                """);


            ImGui.SameLine();
            if (ImGui.Button("Abort")) CancelAllTasks();
        }

        ImGui.Separator();

        ImGuiEx.TextCentered(feature.Name);
        ImGuiEx.TextCentered($"{ImGui.GetWindowSize()}");
        if (feature.Help != null)
        {
            ImGui.SameLine();
            ImGuiHelper.HelpMarker(feature.Help);
        }

        ImGui.Separator();
        if (!ProperOnLogin.PlayerPresent && SelectedFeature is { LoginNeeded: true })
        {
            ImGuiEx.Text(EzColor.Red, "Player not logged in!");
            return;
        }

        if (feature.Requirements.Count > 0 &&
            feature.Requirements.Where(x => x.mandatory)
                   .Any(x => !SubscriptionManager.IsInitialized(x.pluginName)))
        {
            var missingRequirements = feature.Requirements.Where(x => x.mandatory && !SubscriptionManager.IsInitialized(x.pluginName));
            ImGuiEx.TextCentered("Missing Plugins");
            foreach (var requirement in missingRequirements)
            {
                ImGui.Text($"{requirement.pluginName}");
                ImGui.SameLine(100);
                ImGui.TextColored(ImGuiColors.DalamudRed, "disabled");
            }

            return;
        }


        feature.Draw();

        ImGui.SetCursorPos(ImGui.GetCursorPos() +
                           new Vector2(0, ImGui.GetContentRegionAvail()
                                               .Y -
                                          ImGui.GetTextLineHeight()));
    }

    public override void OnClose()
    {
        P!.SelectedFeatureName = string.Empty;
    }
}
