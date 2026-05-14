using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Henchman.Abstractions;
using Henchman.Windows.Layout;

namespace Henchman.Features.General;

[Feature]
internal class MultiboxingUI : FeatureUI
{
    public MultiboxingUI() => Configuration = LoadConfig<Multiboxing.Configuration>() ?? new Multiboxing.Configuration();
    public override string Name => "Multiboxing";
    public override Category Category => Category.System;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.NetworkWired;
    public override Action? Help => () => { ImGui.Text("Settings for multiboxing connections."); };
    public override bool LoginNeeded => false;

    public Multiboxing.Configuration Configuration { get; }

    public override void Draw()
    {
        var configChanged = false;
        DrawCentered(() =>
                     {
                         ImGui.TextColored(Theme.ErrorRed, "You are responsible for your own network settings. No general IT support will be provided.");
                         ImGui.NewLine();

                         ImGui.Text("Use only locally:");
                         ImGui.SameLine(240);
                         configChanged |= ImGui.Checkbox("##localOnly", ref Configuration.LocalOnly);
                         ImGui.SameLine();
                         HelpMarker(() => ImGui.Text("""
                                                     If you don't plan to use multiboxing across multiple machines but only locally, you can keep this option enabled.
                                                     Networked multiboxing can trigger a firewall request on your boss (server) machine.
                                                     """));

                         ImGui.Text("IP Address (Boss) to connect to");
                         ImGui.SameLine(240);
                         ImGui.SetNextItemWidth(240);
                         ImGui.BeginDisabled(Configuration.LocalOnly);
                         ImGui.InputTextWithHint("##ipAddress", "127.0.0.1 / ::1", Configuration.IpBytes);
                         ImGui.EndDisabled();
                         ImGui.SameLine();
                         HelpMarker(() => ImGui.Text("You only need to set this on your henchmen (clients) if you don't use it only locally."));

                         ImGui.Text("Port:");
                         ImGui.SameLine(240);
                         ImGui.SetNextItemWidth(60);
                         configChanged |= ImGui.InputUInt("###port", ref Configuration.Port);
                         ImGui.SameLine();
                         HelpMarker(() => ImGui.Text("""
                                                     If you are not running multiple boss instances (servers) in your network, you can keep the default port.
                                                     Else you have to change the port for each distinct boss/henchmen (server/client) group to a different one.
                                                     """));

                         if (configChanged) SaveConfig(Configuration);
                     });
    }
}
