using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Henchman.Abstractions;
using Henchman.Windows.Layout;

namespace Henchman.Features.General;

[Feature]
internal class MultiboxingUI : FeatureUI
{
    public MultiboxingUI() => Configuration = LoadConfig<Multiboxing.Configuration>() ?? new Multiboxing.Configuration();
    public override string          Name        => "Multiboxing";
    public override Category        Category    => Category.System;
    public override FontAwesomeIcon Icon        => FontAwesomeIcon.NetworkWired;
    public override Action?         Help        => () => { ImGui.Text(T("HelpText")); };
    public override bool            LoginNeeded => false;

    public Multiboxing.Configuration Configuration { get; }

    public override void Draw()
    {
        var configChanged = false;
        DrawCentered(() =>
                     {
                         ImGui.TextColored(Theme.ErrorRed, T("NetworkWarning"));
                         ImGui.NewLine();

                         ImGui.Text(T("UseOnlyLocally"));
                         ImGui.SameLine(240);
                         configChanged |= ImGui.Checkbox("##localOnly", ref Configuration.LocalOnly);
                         ImGui.SameLine();
                         HelpMarker(() => ImGui.Text(T("LocalOnlyHelp")));

                         ImGui.Text(T("IPAddress"));
                         ImGui.SameLine(240);
                         ImGui.SetNextItemWidth(240);
                         ImGui.BeginDisabled(Configuration.LocalOnly);
                         ImGui.InputTextWithHint("##ipAddress", "127.0.0.1 / ::1", Configuration.IpBytes);
                         ImGui.EndDisabled();
                         ImGui.SameLine();
                         HelpMarker(() => ImGui.Text(T("IPAddressHelp")));


                         /*ImGui.Text("Only one Boss (server) instance:");
                         ImGui.SameLine(240);
                         configChanged |= ImGui.Checkbox("##singleServerInstance", ref Configuration.SingleServerInstance);
                         ImGui.SameLine();
                         HelpMarker(() => ImGui.Text("If you aren't using multiple boss instances (servers) of any feature, you can keep this option enabled."));*/

                         ImGui.Text(T("Port"));
                         ImGui.SameLine(240);
                         ImGui.SetNextItemWidth(60);
                         //ImGui.BeginDisabled(Configuration.SingleServerInstance);
                         configChanged |= ImGui.InputUInt("###port", ref Configuration.Port);
                         //ImGui.EndDisabled();
                         ImGui.SameLine();
                         HelpMarker(() => ImGui.Text(T("PortHelp")));

                         if (configChanged) SaveConfig(Configuration);
                     });
    }
}
