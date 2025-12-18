using Dalamud.Bindings.ImGui;

namespace Henchman.Windows.Layout;

public static class Theme
{
    public static Vector4 BackgroundDark  { get; } = new(0.04f, 0.04f, 0.04f, 1f);
    public static Vector4 BackgroundMid   { get; } = new(0.10f, 0.10f, 0.10f, 1f);
    public static Vector4 BackgroundLight { get; } = new(0.16f, 0.16f, 0.16f, 1f);
    public static Vector4 BackgroundCard  { get; } = new(0.15f, 0.15f, 0.15f, 1f);

    public static Vector4 Border     { get; } = new(0.25f, 0.25f, 0.25f, 1f);
    public static Vector4 BorderDark { get; } = new(0.20f, 0.20f, 0.20f, 1f);

    public static Vector4 TextPrimary   { get; } = new(0.88f, 0.88f, 0.88f, 1f);
    public static Vector4 TextSecondary { get; } = new(0.69f, 0.69f, 0.69f, 1f);
    //Original with lower contrast
    //public static Vector4 TextSecondary { get; } = new(0.53f, 0.53f, 0.53f, 1f);
    public static Vector4 TextDisabled  { get; } = new(0.40f, 0.40f, 0.40f, 1f);

    public static Vector4 AccentPink    { get; } = new(1f, 0.42f, 0.62f, 1f);
    public static Vector4 AccentPinkDim { get; } = new(1f, 0.42f, 0.62f, 0.1f);

    public static Vector4 SuccessGreen { get; } = new(0.29f, 0.87f, 0.5f, 1f);
    public static Vector4 ErrorRed     { get; } = new(0.94f, 0.27f, 0.27f, 1f);

    public static Vector4 ButtonDefault { get; } = new(0.20f, 0.20f, 0.20f, 1f);
    public static Vector4 ButtonHovered { get; } = new(0.25f, 0.25f, 0.25f, 1f);
    public static Vector4 ButtonActive  { get; } = new(0.30f, 0.30f, 0.30f, 1f);

    public static Vector4 ButtonDanger        { get; } = new(0.35f, 0.16f, 0.16f, 1f);
    public static Vector4 ButtonDangerHovered { get; } = new(0.48f, 0.23f, 0.23f, 1f);

    public static IDisposable Push()
    {
        var colorCount = 0;
        var styleCount = 0;

        ImGui.PushStyleColor(ImGuiCol.WindowBg, BackgroundMid);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, BackgroundMid);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.PopupBg, BackgroundLight);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.Border, Border);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.Separator, BorderDark);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.Text, TextPrimary);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, TextDisabled);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.Button, ButtonDefault);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ButtonHovered);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, AccentPink);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.Header, BackgroundLight);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.18f, 0.18f, 0.18f, 1f));
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.20f, 0.20f, 0.20f, 1f));
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.Tab, ButtonDefault);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(1f, 0.42f, 0.62f, 0.15f));
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(1f, 0.42f, 0.62f, 0.2f));
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TabUnfocused, ButtonDefault);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, new Vector4(1f, 0.42f, 0.62f, 0.15f));
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, BackgroundMid);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, Border);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TableBorderLight, BorderDark);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, BackgroundCard);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, BackgroundLight);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.FrameBg, BackgroundCard);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.16f, 0.16f, 0.16f, 1f));
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.18f, 0.18f, 0.18f, 1f));
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.CheckMark, AccentPink);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, AccentPink);
        colorCount++;
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, BackgroundMid);
        colorCount++;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        styleCount++;
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        styleCount++;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        styleCount++;
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 4f);
        styleCount++;
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4f);
        styleCount++;
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6f);
        styleCount++;

        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0.2f);
        styleCount++;

        return new ThemeScope(colorCount, styleCount);
    }

    private class ThemeScope(int colorCount, int styleCount) : IDisposable
    {
        public void Dispose()
        {
            ImGui.PopStyleColor(colorCount);
            ImGui.PopStyleVar(styleCount);
        }
    }
}
