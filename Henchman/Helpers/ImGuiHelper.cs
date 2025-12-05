using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ECommons.ImGuiMethods;
using Henchman.Windows.Layout;

namespace Henchman.Helpers;

internal static class ImGuiHelper
{
    internal static float GlobalFontScale => ImGui.GetIO()
                                                 .FontGlobalScale;

    private static readonly Dictionary<string, float> CenteredWidths = new();

    private static string HeaderText => """
                                        For some optional plugins such as Auto Rotations, you can select your preferred option in the plugin settings.
                                        If no settings are available, you'll need to manually configure alternatives for any optional plugins.

                                        If BossMod is optional, you can also use BossModReborn for AI (not the autorotation).
                                        Don't have both enabled at the same time. Henchman is not actively preventing you from being stupid! 
                                        """;

    public static void HelpMarker(Action textAction, Vector4? color = null, string symbolOverride = null, bool sameLine = true, float xOffset = 0f)
    {
        if (sameLine) ImGui.SameLine();

        if (xOffset > 0)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + xOffset);

        ImGui.PushFont(UiBuilder.IconFont);
        ColoredText(color ?? ImGuiColors.DalamudGrey3, symbolOverride ?? FontAwesomeIcon.InfoCircle.ToIconString());
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


    public static void ColoredText(Vector4 col, string s)
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

    public static Vector2 GetLongestStringSize(IEnumerable<string> strings) => ImGui.CalcTextSize(strings.OrderByDescending(s => s.Length)
                                                                                                         .First());

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

    public static void DrawLink(string label, string title, string url)
    {
        ImGui.TextUnformatted(label);

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            using var tooltip = ImRaii.Tooltip();
            if (tooltip.Success)
            {
                ImGuiEx.Text(EzColor.White, title);

                var pos = ImGui.GetCursorPos();
                ImGui.GetWindowDrawList()
                     .AddText(
                              UiBuilder.IconFont, 12,
                              ImGui.GetWindowPos() + pos + new Vector2(2),
                              Theme.TextSecondary.ToUint(),
                              FontAwesomeIcon.ExternalLinkAlt.ToIconString()
                             );
                ImGui.SetCursorPos(pos + new Vector2(20, 0));
                ImGuiEx.Text(Theme.TextSecondary.ToUint(), url);
            }
        }

        if (ImGui.IsItemClicked()) Task.Run(() => Util.OpenLink(url));
    }

    public static void DrawCentered(Action func) => DrawCentered(GetCallStackID(), func);

    public static void DrawCentered(string id, Action draw)
    {
        if (CenteredWidths.TryGetValue(id, out var cachedWidth))
        {
            var regionWidth = ImGui.GetContentRegionAvail().X;
            var offset      = (regionWidth - cachedWidth) * 0.5f;
            if (offset > 0)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (float)Math.Floor(offset));
        }

        ImGui.BeginGroup();
        draw();
        ImGui.EndGroup();

        var measuredWidth = ImGui.GetItemRectSize().X;
        CenteredWidths[id] = (float)Math.Round(measuredWidth);
    }


    /*public static void DrawCentered(string id, Action draw)
    {
        if (CenteredWidths.TryGetValue(id, out var cachedWidth))
        {
            var regionWidth = ImGui.GetContentRegionAvail().X;
            var offset      = (regionWidth - cachedWidth) * 0.5f;
            if (offset > 0)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        }

        ImGui.BeginGroup();
        draw();
        ImGui.EndGroup();

        var rectMin       = ImGui.GetItemRectMin().X;
        var rectMax       = ImGui.GetItemRectMax().X;
        var measuredWidth = rectMax - rectMin;

        CenteredWidths[id] = measuredWidth;
    }*/


    /*public static void DrawCentered(string id, Action draw)
    {
        if (CenteredWidths.TryGetValue(id, out var cachedWidth))
        {
            var regionWidth = ImGui.GetContentRegionAvail()
                                   .X;
            var offset = (regionWidth - cachedWidth) * 0.5f;
            if (offset > 0)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        }

        ImGui.BeginGroup();
        draw();
        ImGui.EndGroup();

        if (!CenteredWidths.ContainsKey(id))
        {
            var measuredWidth = ImGui.GetItemRectSize()
                                     .X;
            CenteredWidths[id] = measuredWidth;
        }
    }*/

    public static bool StartButton() => ImGui.Button("Start", new Vector2(70 * GlobalFontScale, 30 * GlobalFontScale));

    public class ColumnScope : IDisposable
    {
        private readonly int expected;
        private          int actual;

        public ColumnScope(int expected)
        {
            this.expected = expected;
            actual        = 0;
            ImGui.TableNextRow();
        }

        public void Dispose()
        {
#if DEBUG
            ErrorIf(actual != expected, $"Column mismatch: used {actual}, expected {expected}");
#endif
        }

        public void TableNextColumn()
        {
            ImGui.TableNextColumn();
            actual++;
        }
    }
}
