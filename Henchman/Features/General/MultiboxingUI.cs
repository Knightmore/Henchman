using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Underlings.Modules;

namespace Henchman.Features.General;

[Module]
internal class MultiboxingUI : ModuleUI
{
    public MultiboxingUI() => Configuration = LoadConfig<Multiboxing.Configuration>() ?? new Multiboxing.Configuration();
    public override string          Name        => "Multiboxing";
    public override Enum            Category    => Henchman.Category.System;
    public override FontAwesomeIcon Icon        => FontAwesomeIcon.NetworkWired;
    public override Action?         Help        => () => { ImGui.Text(T("HelpText")); };
    public override bool            LoginNeeded => false;

    public Multiboxing.Configuration Configuration { get; }

    public override void Draw()
    {
        var configChanged = false;
        DrawCentered("##MultiboxingNetworkWarning", () =>
                                                    {
                                                        ImGui.TextColored(Theme.ErrorRed, T("NetworkWarning"));
                                                        ImGui.NewLine();

                                                        ImGui.Text(T("UseOnlyLocally"));
                                                        ImGui.SameLine(240 * GlobalFontScale);
                                                        configChanged |= ImGui.Checkbox("##localOnly", ref Configuration.LocalOnly);
                                                        ImGui.SameLine();
                                                        HelpMarker(() => ImGui.Text(T("LocalOnlyHelp")));

                                                        ImGui.Text(T("IPAddress"));
                                                        ImGui.SameLine(240          * GlobalFontScale);
                                                        ImGui.SetNextItemWidth(240f * GlobalFontScale);
                                                        ImGui.BeginDisabled(Configuration.LocalOnly);
                                                        ImGui.InputTextWithHint("##ipAddress", "127.0.0.1 / ::1", Configuration.IpBytes);
                                                        ImGui.EndDisabled();
                                                        ImGui.SameLine();
                                                        HelpMarker(() => ImGui.Text(T("IPAddressHelp")));
                                                        ImGui.Text(T("Port"));
                                                        ImGui.SameLine(240         * GlobalFontScale);
                                                        ImGui.SetNextItemWidth(60f * GlobalFontScale);
                                                        configChanged |= ImGui.InputUInt("###port", ref Configuration.Port);
                                                        ImGui.SameLine();
                                                        HelpMarker(() => ImGui.Text(T("PortHelp")));

                                                        if (configChanged) SaveConfig(Configuration);
                                                    });
    }
}
