using System.IO;
using System.Linq;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ImGuiNET;
#if PRIVATE
using Henchman.Features.Private.Debugging;
using Henchman.Features.Private.LGBInspector;
#endif

namespace Henchman.Windows;

public class FeatureBar : Window, IDisposable
{
    private const uint SidebarWidth = 200;

    //private string    selectedFeature = string.Empty;

    public FeatureBar() : base($"{P.Name} - {P.GetType().Assembly.GetName().Version} ",
                                              ImGuiWindowFlags.NoScrollbar |
                                              ImGuiWindowFlags.NoScrollWithMouse |
                                              ImGuiWindowFlags.AlwaysAutoResize |
                                              ImGuiWindowFlags.NoSavedSettings)
    {

        var width = SidebarWidth +
                    ImGui.GetStyle()
                         .ItemSpacing.X +
                    (2 *
                     ImGui.GetStyle()
                          .FramePadding.X);
        //Size            = new Vector2(width, 600);
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(width, 400), MaximumSize = new Vector2(1920, 1080) };
        SizeCondition = ImGuiCond.Always;
    }

    /*private FeatureUI? SelectedFeature => FeatureSet.FirstOrDefault(t => t.Name == P!.selectedFeature) ??
                                          GeneralSet.FirstOrDefault(t => t.Name == P!.selectedFeature);*/

    public void Dispose() { }

    public override void Draw()
    {
        var region = ImGui.GetContentRegionAvail();
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
                    ImGuiEx.LineCentered("###Logo", () => { ImGui.Image(logo.ImGuiHandle, new Vector2(128f.Scale(), 128f.Scale())); });
            }


            ImGui.Spacing();
            ImGui.Separator();

            if (Running && CurrentTaskRecord != null)
            {
                ImGuiEx.TextCentered($"Running Task:");
                ImGuiEx.TextCentered(TaskName);
                ImGui.SameLine();
                ImGuiEx.HelpMarker($"""
                                    Task Subroutine:

                                    {(TaskDescription.Count == 0 ? "No Description" : string.Join("\n", TaskDescription))}
                                    """);


                ImGui.SameLine();
                if (ImGui.Button("Abort")) CancelAllTasks();
            }
            else
                ImGuiEx.TextCentered("No feature running");

            ImGui.Separator();

            foreach (var feature in FeatureSet.OrderBy(t => t.Name))
            {
                if (ImGui.Selectable($"{feature.Name}##Selectable_{feature.Name}", P!.SelectedFeature == feature.Name))
                {
                    P!.SelectedFeature = P!.SelectedFeature != feature.Name
                                              ? feature.Name
                                              : string.Empty;
                    P.FeatureWindow.IsOpen = !P.SelectedFeature.IsNullOrEmpty();
                }
            }

            ImGui.Separator();


            foreach (var feature in GeneralSet.OrderBy(t => t.Name))
            {
                if (ImGui.Selectable($"{feature.Name}##Selectable_{feature.Name}", P!.SelectedFeature == feature.Name))
                {
                    P!.SelectedFeature = P!.SelectedFeature != feature.Name
                                                 ? feature.Name
                                                 : string.Empty;
                    P.FeatureWindow.IsOpen = !P.SelectedFeature.IsNullOrEmpty();
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
