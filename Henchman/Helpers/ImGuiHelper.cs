using System.Linq;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;

namespace Henchman.Helpers;

internal static class ImGuiHelper
{
    private static string HeaderText => """
                                        For some optional plugins such as Auto Rotations, you can select your preferred option in the plugin settings.
                                        If no settings are available, you'll need to manually configure alternatives for any optional plugins.

                                        If BossMod is optional, you can also use BossModReborn for AI (not the autorotation).
                                        Don't have both enabled at the same time. Henchman is not actively preventing you from being stupid! 
                                        """;

    public static void HelpMarker(Action textAction, Vector4? color = null, string symbolOverride = null, bool sameLine = true) => InfoMarker(textAction, color, symbolOverride, sameLine);

    public static void InfoMarker(Action textAction, Vector4? color = null, string symbolOverride = null, bool sameLine = true)
    {
        if (sameLine) ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        Text(color ?? ImGuiColors.DalamudGrey3, symbolOverride ?? FontAwesomeIcon.InfoCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            textAction();
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    public static void Text(Vector4 col, string s)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, col);
        ImGui.TextUnformatted(s);
        ImGui.PopStyleColor();
    }

    private static void DrawPluginRequirement(string pluginName, bool mandatory, float longestStringWidth, float requirementWidth, float spacing)
    {
        ImGui.Text(pluginName);
        ImGui.SameLine(ImGui.GetCursorPosX() + longestStringWidth + spacing);
        ImGui.Text(mandatory
                           ? "(mandatory)"
                           : "(optional)");
        ImGui.SameLine(ImGui.GetCursorPosX() + longestStringWidth + requirementWidth + (2 * spacing));

        var pluginActive = SubscriptionManager.IsInitialized(pluginName);
        ImGui.TextColored(pluginActive
                                  ? ImGuiColors.HealerGreen
                                  : ImGuiColors.DalamudRed,
                          pluginActive
                                  ? "enabled"
                                  : "disabled");
    }

    private static float GetLongestIPCNameWidth()
    {
        return ImGui.CalcTextSize(typeof(IPCNames)
                                 .GetFields(BindingFlags.Public | BindingFlags.Static)
                                 .Where(f => f.FieldType == typeof(string))
                                 .Select(f => (string)f.GetValue(null)!)
                                 .OrderByDescending(s => s.Length)
                                 .First())
                    .X;
    }

    public static void DrawRequirements(List<(string pluginName, bool mandatory)> requirements)
    {
        if (requirements.Count == 0) return;

        ImGui.Separator();
        ImGuiEx.TextCentered("REQUIREMENTS");
        ImGui.Text(HeaderText);
        ImGui.NewLine();

        var spacing            = 10f;
        var longestStringWidth = GetLongestIPCNameWidth();
        var requirementWidth = ImGui.CalcTextSize("(mandatory)")
                                    .X;

        foreach (var plugin in requirements) DrawPluginRequirement(plugin.pluginName, plugin.mandatory, longestStringWidth, requirementWidth, spacing);
    }

    public static void DrawRequirements(HashSet<FeatureUI> featureList)
    {
        if (!featureList.Any(f => f.Requirements.Count > 0)) return;

        ImGuiEx.TextCentered(HeaderText);
        ImGui.NewLine();

        var spacing            = 10f;
        var longestStringWidth = GetLongestIPCNameWidth();
        var requirementWidth = ImGui.CalcTextSize("(mandatory)")
                                    .X;

        foreach (var feature in featureList)
        {
            if (feature.Requirements.Count == 0) continue;

            ImGui.Separator();
            ImGuiEx.TextCentered(feature.Name);
            ImGui.NewLine();

            foreach (var req in feature.Requirements) DrawPluginRequirement(req.pluginName, req.mandatory, longestStringWidth, requirementWidth, spacing);
        }
    }

    /*public static void DrawRequirements(HashSet<FeatureUI> featureList)
    {
        ImGuiEx.TextCentered("""
                             For some optional plugins such as Auto Rotations, you can select your preferred option in the plugin settings.
                             If no settings are available, you'll need to manually configure alternatives for any optional plugins.
                             """);
        ImGui.NewLine();

        var spacing = 10;

        var longestStringWidth = ImGui.CalcTextSize(typeof(IPCNames)
                                                   .GetFields(BindingFlags.Public | BindingFlags.Static)
                                                   .Where(f => f.FieldType == typeof(string))
                                                   .Select(f => (string)f.GetValue(null)!)
                                                   .OrderByDescending(s => s.Length)
                                                   .First())
                                      .X;


        var requirementWidth = ImGui.CalcTextSize("(mandatory)")
                                    .X;

        foreach (var feature in featureList)
        {
            if (feature.Requirements.Count > 0)
            {
                ImGui.Separator();
                ImGuiEx.TextCentered(feature.Name);
                ImGui.NewLine();

                foreach (var requirement in feature.Requirements)
                {
                    ImGui.Text($"{requirement.pluginName}");
                    ImGui.SameLine(ImGui.GetCursorPosX() + longestStringWidth + spacing);
                    ImGui.Text(requirement.mandatory
                                       ? "(mandatory)"
                                       : "(optional)");
                    ImGui.SameLine(ImGui.GetCursorPosX() + longestStringWidth + requirementWidth + (2 * spacing));
                    var pluginActive = SubscriptionManager.IsInitialized(requirement.pluginName);
                    ImGui.TextColored(pluginActive
                                              ? ImGuiColors.HealerGreen
                                              : ImGuiColors.DalamudRed, pluginActive
                                                                                ? "enabled"
                                                                                : "disabled");
                }
            }
        }
    }

    public static void DrawRequirements(List<(string pluginName, bool mandatory)> requirements)
    {
        if (requirements.Count > 0)
        {
            ImGui.Separator();
            ImGuiEx.TextCentered("REQUIREMENTS");
            ImGuiEx.Text("""
                         For some optional plugins such as Auto Rotations, you can select your preferred option in the plugin settings.
                         If no settings are available, you'll need to manually configure alternatives for any optional plugins.
                         """);
            ImGui.NewLine();

            var spacing = 10;

            var longestStringWidth = ImGui.CalcTextSize(typeof(IPCNames)
                                                       .GetFields(BindingFlags.Public | BindingFlags.Static)
                                                       .Where(f => f.FieldType == typeof(string))
                                                       .Select(f => (string)f.GetValue(null)!)
                                                       .OrderByDescending(s => s.Length)
                                                       .First())
                                          .X;


            var requirementWidth = ImGui.CalcTextSize("(mandatory)")
                                        .X;

            foreach (var plugin in requirements)
            {
                ImGui.Text($"{plugin.pluginName}");
                ImGui.SameLine(ImGui.GetCursorPosX() + longestStringWidth + spacing);
                ImGui.Text(plugin.mandatory
                                   ? "(mandatory)"
                                   : "(optional)");
                ImGui.SameLine(ImGui.GetCursorPosX() + longestStringWidth + requirementWidth + (2 * spacing));
                var pluginActive = SubscriptionManager.IsInitialized(plugin.pluginName);
                ImGui.TextColored(pluginActive
                                          ? ImGuiColors.HealerGreen
                                          : ImGuiColors.DalamudRed, pluginActive
                                                                            ? "enabled"
                                                                            : "disabled");
            }
        }
    }*/

    public static void AnimatedRainbowTextCentered(string text)
    {
        var time = (float)ImGui.GetTime();

        var totalWidth = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            totalWidth += ImGui.CalcTextSize(text[i]
                                                    .ToString())
                               .X;
        }

        var windowWidth = ImGui.GetWindowSize()
                               .X;
        var startX = (windowWidth - totalWidth) * 0.5f;

        ImGui.SetCursorPosX(startX);

        for (var i = 0; i < text.Length; i++)
        {
            var hue   = ((time * 50f) + (i * 30f)) % 360f;
            var color = ColorFromHSV(hue, 1f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.Text(text[i]
                              .ToString());
            ImGui.PopStyleColor();

            ImGui.SameLine(0f, 0f);
        }

        ImGui.NewLine();
    }

    private static Vector4 ColorFromHSV(float hue, float saturation, float value)
    {
        var hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
        var f  = (hue / 60) - MathF.Floor(hue / 60);

        value *= 255;
        var v = value;
        var p = value * (1 - saturation);
        var q = value * (1 - (f       * saturation));
        var t = value * (1 - ((1 - f) * saturation));

        return hi switch
               {
                       0 => new Vector4(v, t, p, 255),
                       1 => new Vector4(q, v, p, 255),
                       2 => new Vector4(p, v, t, 255),
                       3 => new Vector4(p, q, v, 255),
                       4 => new Vector4(t, p, v, 255),
                       _ => new Vector4(v, p, q, 255)
               } /
               255f;
    }

    /*public static unsafe ImRaii.IEndObject TabItem(string label, ref bool open)
    {
        ArgumentNullException.ThrowIfNull(label);

        // One-off for now, we should make this into a generic solution if we need it more often
        const int labelMaxAlloc = 2048;

        var labelByteCount = Encoding.UTF8.GetByteCount(label);

        if (labelByteCount > labelMaxAlloc)
        {
            throw new ArgumentOutOfRangeException(nameof(label), $"Label is too long. (Longer than {labelMaxAlloc} bytes)");
        }

        var nativeLabelStackBytes = stackalloc byte[labelByteCount + 1];

        int nativeLabelOffset;
        fixed (char* utf16Ptr = label)
        {
            nativeLabelOffset = Encoding.UTF8.GetBytes(utf16Ptr, label.Length, nativeLabelStackBytes, labelByteCount);
        }

        nativeLabelStackBytes[nativeLabelOffset] = 0;
        var p_open = open
                             ? (byte*)1
                             : (byte*)0;
        ImGuiTabItemFlags flags = 0;
        var               ret   = ImGuiNative.igBeginTabItem(nativeLabelStackBytes, p_open, flags);

        return new EndConditionally(ImGuiNative.igEndTabItem, ret != 0);
    }*/

    private struct EndConditionally : ImRaii.IEndObject
    {
        public EndConditionally(Action endAction, bool success)
        {
            EndAction = endAction;
            Success   = success;
            Disposed  = false;
        }

        public bool Success { get; }

        public bool Disposed { get; private set; }

        private Action EndAction { get; }

        public void Dispose()
        {
            if (Disposed)
                return;

            if (Success)
                EndAction();
            Disposed = true;
        }
    }
}
