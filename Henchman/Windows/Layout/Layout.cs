using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;

namespace Henchman.Windows.Layout;

public class Layout(ImTextureID logoTextureHandle = default)
{
    public Sidebar Sidebar { get; } = new(logoTextureHandle);

    public static float GlobalFontScale => ImGui.GetIO()
                                                .FontGlobalScale;

    public void Draw(Action renderContent)
    {
        var   posX = ImGui.GetCursorPosX();
        float posY;
        using (var contentArea = ImRaii.Child("##HenchmanContent", new Vector2(0, ImGui.GetContentRegionAvail()
                                                                                       .Y -
                                                                                  (40 * GlobalFontScale)), true))
        {
            if (!contentArea.Success) return;

            using (var contentBody = ImRaii.Child("##ContentBody", Vector2.Zero, false))
            {
                if (!contentBody.Success) return;

                renderContent();
            }

            posY = ImGui.GetCursorPosY() + 38;
        }

        ImGui.SetCursorPos(new Vector2(posX, posY));
        DrawFooter();
    }

    public void DrawWithHeader(FeatureUI selectedFeature)
    {
        var   posX = ImGui.GetCursorPosX();
        float posY;
        using (var contentArea = ImRaii.Child("##HenchmanContent", new Vector2(0, ImGui.GetContentRegionAvail()
                                                                                       .Y -
                                                                                  (40 * GlobalFontScale)), true))
        {
            if (!contentArea.Success) return;

            if (selectedFeature.Requirements.Count > 0 &&
                selectedFeature.Requirements.Where(x => x.mandatory)
                               .Any(x => !SubscriptionManager.IsInitialized(x.pluginName)))
            {
                var missingRequirements = selectedFeature.Requirements.Where(x => x.mandatory && !SubscriptionManager.IsInitialized(x.pluginName));
                ImGuiEx.TextCentered("Missing Plugins");
                foreach (var requirement in missingRequirements)
                {
                    ImGui.Text($"{requirement.pluginName}");
                    ImGui.SameLine(100);
                    ImGui.TextColored(Theme.ErrorRed, "disabled");
                }

                return;
            }

            DrawContentHeader(selectedFeature);

            using (var contentBody = ImRaii.Child("##ContentBody", Vector2.Zero, false))
            {
                if (!contentBody.Success) return;

                selectedFeature.Draw();
            }

            posY = ImGui.GetCursorPosY() + 38;
        }

        ImGui.SetCursorPos(new Vector2(posX, posY));
        DrawFooter();
    }

    private void DrawContentHeader(FeatureUI selectedFeature)
    {
        using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.16f, 0.16f, 1f)))
        {
            using (ImRaii.PushColor(ImGuiCol.Border, new Vector4(0.20f, 0.20f, 0.20f, 1f)))
            {
                using (var headerChild = ImRaii.Child("##ContentHeader", new Vector2(0, 45 * GlobalFontScale), true, ImGuiWindowFlags.NoScrollbar))
                {
                    if (!headerChild.Success) return;

                    ImGui.SetCursorPosY((ImGui.GetCursorPosY() + 7) * GlobalFontScale);
                    ImGui.SetCursorPosX((ImGui.GetCursorPosX() + 5) * GlobalFontScale);

                    using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextPrimary))
                    {
                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.Text(selectedFeature.Icon.ToIconString());
                        ImGui.PopFont();

                        ImGui.SameLine();

                        var style        = ImGui.GetStyle();
                        var originalSize = style.ItemSpacing;
                        style.ItemSpacing = originalSize with { X = 0 };

                        ImGui.Text(selectedFeature.Name);

                        if (selectedFeature.Help != null) HelpMarker(selectedFeature.Help, sameLine: true, xOffset: 5f * GlobalFontScale);


                        style.ItemSpacing = originalSize;

                        ImGui.SameLine(ImGui.GetContentRegionAvail()
                                            .X -
                                       (30 * GlobalFontScale));
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (2 * GlobalFontScale));

                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.PushStyleColor(ImGuiCol.Text, Running
                                                                    ? Theme.SuccessGreen
                                                                    : Theme.ErrorRed);
                        if (ImGui.SmallButton(FontAwesomeIcon.ChartLine.ToIconString())) P.StatusWindow.IsOpen = !P.StatusWindow.IsOpen;
                        ImGui.PopStyleColor();
                        ImGui.PopFont();

                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle Plugin Status");
                    }
                }
            }
        }

        ImGui.Spacing();
    }

    internal static void DrawInfoBox(Action startButton, Action? additionalText = null)
    {
        using (ImRaii.PushColor(ImGuiCol.ChildBg, Theme.BackgroundCard))
        {
            using (var infoBox = ImRaii.Child("##InfoBox", new Vector2(0, 50 * GlobalFontScale), true, ImGuiWindowFlags.NoScrollbar))
            {
                if (!infoBox.Success) return;

                var boxHeight    = 50f * GlobalFontScale;
                var textHeight   = ImGui.GetTextLineHeight();
                var buttonHeight = 30f * GlobalFontScale;

                var textY   = (boxHeight - textHeight)   / 2f;
                var buttonY = (boxHeight - buttonHeight) / 2f;

                ImGui.SetCursorPosY(textY);

                additionalText?.Invoke();

                var buttonX = ImGui.GetCursorPosX() +
                              ImGui.GetContentRegionAvail()
                                   .X -
                              (70 * GlobalFontScale);
                ImGui.SetCursorPos(new Vector2(buttonX, buttonY));

                DrawButton(startButton);
            }
        }
    }

    internal static void DrawButton(Action button)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.AccentPink))
        {
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(1f, 0.42f, 0.62f, 0.15f)))
            {
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.42f, 0.62f, 0.25f)))
                {
                    using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(1f, 0.42f, 0.62f, 0.3f)))
                    {
                        using (ImRaii.PushColor(ImGuiCol.Border, Theme.AccentPink))
                        {
                            var style     = ImGui.GetStyle();
                            var oldBorder = style.FrameBorderSize;
                            style.FrameBorderSize = 1f;

                            button();

                            style.FrameBorderSize = oldBorder;
                        }
                    }
                }
            }
        }
    }

    private void DrawFooter()
    {
        using (var copyrightArea = ImRaii.Child("##Copyright", new Vector2(0, 35 * GlobalFontScale), true))
        {
            if (!copyrightArea.Success) return;
            ImGui.Text("Plugin by");
            ImGui.SameLine();
            DrawLink("Knightmore", "GitHub", "https://github.com/Knightmore/Henchman");
            ImGui.SameLine();
            ImGui.Text("•");
            ImGui.SameLine();
            ImGui.Text("Theme/Design by");
            ImGui.SameLine();
            DrawLink("Wah", "GitHub", "https://github.com/Brappp");
        }
    }
}
