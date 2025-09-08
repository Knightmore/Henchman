using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.Configuration;
using ECommons.ImGuiMethods;


#if PRIVATE
using Henchman.Features.Private.Debugging;
using Henchman.Features.Private.LGBInspector;
#endif

namespace Henchman.Windows;

public class FeatureBar : Window, IDisposable
{
    private const uint SidebarWidth = 200;

    //private string    selectedFeature = string.Empty;

    public FeatureBar() : base($"{P.Name} - {P.GetType().Assembly.GetName().Version}##featureBar ",
                               ImGuiWindowFlags.NoScrollbar       |
                               ImGuiWindowFlags.NoScrollWithMouse |
                               ImGuiWindowFlags.AlwaysAutoResize  |
                               ImGuiWindowFlags.NoSavedSettings)
    {
        var width = SidebarWidth +
                    ImGui.GetStyle()
                         .ItemSpacing.X +
                    2 *
                     ImGui.GetStyle()
                          .FramePadding.X;
        //Size            = new Vector2(width, 600);
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(width, 400), MaximumSize = new Vector2(1920, 1080) };
        SizeCondition   = ImGuiCond.Always;
    }

    /*private FeatureUI? SelectedFeature => FeatureSet.FirstOrDefault(t => t.Name == P!.selectedFeature) ??
                                          GeneralSet.FirstOrDefault(t => t.Name == P!.selectedFeature);*/

    public void Dispose() { }

    public override void Draw()
    {
        var region            = ImGui.GetContentRegionAvail();
        var topLeftSideHeight = region.Y;

        using (var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(5, 0)))
        {
            var regionSize = ImGui.GetContentRegionAvail();
            ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

            using (var c = ImRaii.Child("logo", new Vector2(0, 128f.Scale())))
            {
                var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.Directory?.FullName!, "Images\\Henchman.png");
                if (!File.Exists(imagePath))
                    throw new FileNotFoundException();

                if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
                    ImGuiEx.LineCentered("###Logo", () =>
                                                    {
                                                        ImGui.Image(logo.Handle, new Vector2(128f.Scale(), 128f.Scale()));
                                                        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                                                        {
                                                            P.ToggleMainUi();
                                                            P.FeatureWindow.IsOpen = false;
                                                            C.SeparateWindows      = !C.SeparateWindows;
                                                            P.ToggleMainUi();
                                                            EzConfig.Save();
                                                        }
                                                    });
            }


            ImGui.Spacing();
            ImGui.Separator();

            if (Running && CurrentTaskRecord != null)
            {
                ImGuiEx.TextCentered("Running Task:");
                ImGuiEx.TextCentered(TaskName);
                ImGui.SameLine();
                ImGuiEx.HelpMarker($"""
                                    Task Subroutine:

                                    {(TaskDescription.Count == 0 ? "No Description" : string.Join("\n", TaskDescription))}
                                    """);
                if (ImGui.Button("Abort")) CancelAllTasks();
            }
            else
                ImGuiEx.TextCentered("No feature running");

            ImGui.Separator();

            foreach (var feature in FeatureSet.OrderBy(t => t.Name))
            {
                if (ImGui.Selectable($"{feature.Name}##Selectable_{feature.Name}", P!.SelectedFeatureName == feature.Name))
                {
                    P!.SelectedFeatureName = P!.SelectedFeatureName != feature.Name
                                              ? feature.Name
                                              : string.Empty;
                    P.FeatureWindow.SizeConstraints = feature.SizeConstraints;
                    P.FeatureWindow.IsOpen          = !P.SelectedFeatureName.IsNullOrEmpty();
                }
            }
            ImGui.Separator();
            
            foreach (var feature in ExperimentalSet.OrderBy(t => t.Name))
            {
                if (ImGui.Selectable($"{feature.Name}##Selectable_{feature.Name}", P!.SelectedFeatureName == feature.Name))
                {
                    P!.SelectedFeatureName = P!.SelectedFeatureName != feature.Name
                                                     ? feature.Name
                                                     : string.Empty;
                    P.FeatureWindow.SizeConstraints = feature.SizeConstraints;
                    P.FeatureWindow.IsOpen          = !P.SelectedFeatureName.IsNullOrEmpty();
                }
            }

            ImGui.Separator();

            foreach (var feature in GeneralSet.OrderBy(t => t.Name))
            {
                if (ImGui.Selectable($"{feature.Name}##Selectable_{feature.Name}", P!.SelectedFeatureName == feature.Name))
                {
                    P!.SelectedFeatureName = P!.SelectedFeatureName != feature.Name
                                                 ? feature.Name
                                                 : string.Empty;
                    P.FeatureWindow.SizeConstraints = feature.SizeConstraints;
                    P.FeatureWindow.IsOpen          = !P.SelectedFeatureName.IsNullOrEmpty();
                }
            }

            ImGui.PopStyleVar();
        }
    }
#if PRIVATE
    public FeatureUI DebuggingUi = new DebuggingUI();
    public FeatureUI LGBInspectorUi = new LGBInspectorUI();
#endif
}
