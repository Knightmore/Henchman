using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.Events;
using ECommons.ImGuiMethods;
using Henchman.Helpers;
#if PRIVATE
using Henchman.Features.Private.Debugging;
using Henchman.Features.Private.LGBInspector;
#endif

namespace Henchman.Windows;

public class MainWindow : Window, IDisposable
{
    private const uint SidebarWidth = 200;

    public MainWindow(Henchman plugin) : base($"{P.Name} - {P.GetType().Assembly.GetName().Version} ",
                                              ImGuiWindowFlags.NoScrollbar       |
                                              ImGuiWindowFlags.NoScrollWithMouse |
                                              ImGuiWindowFlags.AlwaysAutoResize  |
                                              ImGuiWindowFlags.NoSavedSettings)
    {
#if PRIVATE
        var widthRation = 7f;
#else
        var widthRation = 5f;
#endif

        var width = (SidebarWidth * widthRation) +
                    ImGui.GetStyle()
                         .ItemSpacing.X +
                    (2 *
                     ImGui.GetStyle()
                          .FramePadding.X);
        Size            = new Vector2(width, 600);
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(width, 600), MaximumSize = new Vector2(1920, 1080) };
        SizeCondition   = ImGuiCond.Always;
    }

    private FeatureUI? SelectedFeature => FeatureSet.FirstOrDefault(t => t.Name == P!.SelectedFeature) ??
                                          GeneralSet.FirstOrDefault(t => t.Name == P!.SelectedFeature);

    public void Dispose() { }

    public override void Draw()
    {
        var region            = ImGui.GetContentRegionAvail();
        var topLeftSideHeight = region.Y;
        if (Running && CurrentTaskRecord != null)
        {
            ImGuiEx.TextCentered($"Running Task: {TaskName}");
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

        using (var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(5, 0)))
        {
            using (var table = ImRaii.Table("###HenchmanTable", 2))
            {
                ImGui.TableSetupColumn("##SidebarColumn", ImGuiTableColumnFlags.WidthFixed, SidebarWidth);
                ImGui.TableSetupColumn("##MainColumn", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextColumn();

                var regionSize = ImGui.GetContentRegionAvail();
                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

                using (var leftChild = ImRaii.Child("###SidebarChild", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                {
                    using (var c = ImRaii.Child("logo", new Vector2(0, 128f.Scale())))
                    {
                        var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.Directory?.FullName!, "Images\\Henchman.png");
                        if (!File.Exists(imagePath))
                            throw new FileNotFoundException();

                        if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
                            ImGuiEx.LineCentered("###Logo", () => { ImGui.Image(logo.Handle, new Vector2(128f.Scale(), 128f.Scale())); });
                    }


                    ImGui.Spacing();
                    ImGui.Separator();

                    foreach (var feature in FeatureSet.OrderBy(t => t.Name))
                    {
                        if (ImGui.Selectable($"{feature.Name}###Selectable_{feature.Name}", P!.SelectedFeature == feature.Name))
                        {
                            P!.SelectedFeature = P!.SelectedFeature != feature.Name
                                                         ? feature.Name
                                                         : string.Empty;
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
                        }
                    }
                }


                ImGui.PopStyleVar();

                ImGui.TableNextColumn();

                using (var rightChild = ImRaii.Child("###MainChild", new Vector2(0, ImGui.GetContentRegionAvail()
                                                                                         .Y -
                                                                                    ImGui.GetTextLineHeight()), true))
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

                    ImGuiEx.TextCentered(feature.Name);
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
            }
        }
    }
}
