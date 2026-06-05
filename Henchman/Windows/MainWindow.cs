using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using Henchman.Abstractions;
using Henchman.Windows.Layout;

namespace Henchman.Windows;

public class MainWindow : Window, IDisposable
{
    private bool                 isInitialized;
    private Layout.Layout?       layout;
    private IDalamudTextureWrap? logoTextureWrap;
    private string?              selectedFeatureName = string.Empty;

    public MainWindow() : base($"{P.Name} - {P.GetType().Assembly.GetName().Version}", ImGuiWindowFlags.None)
    {
        SizeConstraints = new WindowSizeConstraints
                          {
                                  MinimumSize = new Vector2(900, 600),
                                  MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
                          };

        Size          = new Vector2(1000, 700);
        SizeCondition = ImGuiCond.FirstUseEver;

        RespectCloseHotkey = true;
    }

    private float GlobalFontScale => ImGui.GetIO()
                                          .FontGlobalScale;

    private FeatureUI? selectedFeature => FeatureSet.FirstOrDefault(t => t.Name == selectedFeatureName);

    public void Dispose()
    {
        logoTextureWrap?.Dispose();
    }

    private void InitializeLayout()
    {
        if (isInitialized) return;

        var iconPath = Path.Combine(Svc.PluginInterface.AssemblyLocation.Directory?.FullName!, "Images", "Henchman.png");

        var logoHandle = default(ImTextureID);
        if (File.Exists(iconPath))
        {
            try
            {
                var textureTask = Svc.Texture.GetFromFile(iconPath);
                logoTextureWrap = textureTask.RentAsync()
                                             .Result;
                if (logoTextureWrap != null)
                {
                    logoHandle = logoTextureWrap.Handle;
                    Log($"Loaded icon: {logoHandle}, size: {logoTextureWrap.Width}x{logoTextureWrap.Height}");
                }
                else
                    InternalWarning("RentAsync returned null");
            }
            catch (Exception ex)
            {
                InternalError($"Failed to load texture: {ex.Message}");
            }
        }
        else
            InternalWarning($"Icon file not found: {iconPath}");

        layout = new Layout.Layout(logoHandle);

        SetupSidebar();

        isInitialized = true;
    }

    private void SetupSidebar()
    {
        if (layout == null) return;

        foreach (var feature in FeatureSet.OrderBy(x => x.Category)
                                          .ThenBy(x => x.Name))
        {
            if (!P!.categories.TryGetValue(feature.Category, out var foundCategory))
            {
                InternalError($"Category {feature.Category} was not defined!");
                continue;
            }

            var category = layout.Sidebar.AddCategory(Loc.G($"Category.{feature.Category}"), foundCategory);
            category.Value.Items.Add(new NavItem(feature.DisplayName, feature.Icon, () => selectedFeatureName = selectedFeatureName != feature.Name
                                                                                                                      ? feature.Name
                                                                                                                      : string.Empty));
        }
    }

    internal void RebuildSidebar()
    {
        if (layout == null) return;
        layout.Sidebar.Clear();
        layout.Sidebar.ActiveItemName = string.Empty;
        SetupSidebar();
    }

    public override void Draw()
    {
        InitializeLayout();

        if (layout == null) return;

        using (Theme.Push())
        {
            var sidebarButtonPos = layout.Sidebar.Draw(() =>
                                                       {
                                                           layout.Sidebar.ActiveItemName = string.Empty;
                                                           selectedFeatureName           = string.Empty;
                                                       });

            ImGui.SameLine();
            if (selectedFeature != null)
                layout.DrawWithHeader(selectedFeature);
            else
            {
                layout.Draw(() =>
                            {
                                ImGuiEx.TextCentered(Theme.ErrorRed, Loc.G("Splash.Attention"));
                                ImGuiEx.TextCentered(Loc.G("Splash.AttentionBody"));
                                ImGui.NewLine();
                                ImGui.Separator();
                                ImGui.NewLine();

                                ImGuiEx.TextCentered(Theme.ErrorRed, Loc.G("Splash.PositionalData"));
                                ImGuiEx.TextCentered(Loc.G("Splash.PositionalDataBody"));
                            });
            }

            ImGui.SetCursorPos(sidebarButtonPos);
            using (ImRaii.Child("OverlayChild", new Vector2(24 * GlobalFontScale, 40 * GlobalFontScale), false,
                                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground))
                layout.Sidebar.DrawCollapseButton();
        }
    }
}
