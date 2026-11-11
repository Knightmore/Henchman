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
        Widgets.Banner(BannerKind.Error, T("NetworkWarning"));
        ImGui.Dummy(new Vector2(0, 2 * GlobalFontScale));
        Widgets.Region(520f, () => Widgets.Card(T("Connection"), () =>
                                                                         {
                                                                             var configChanged = Widgets.SwitchRow($"{T("UseOnlyLocally").TrimEnd(':')}##localOnly", ref Configuration.LocalOnly,
                                                                                                                   help: T("LocalOnlyHelp"));
                                                                             using (Widgets.Disabled(Configuration.LocalOnly))
                                                                             {
                                                                                 Widgets.FieldRow(T("IPAddress"), () =>
                                                                                                                  {
                                                                                                                      ImGui.SetNextItemWidth(Widgets.Avail - 26 * GlobalFontScale);
                                                                                                                      ImGui.InputTextWithHint("##ipAddress", "127.0.0.1 / ::1", Configuration.IpBytes);
                                                                                                                      ImGui.SameLine();
                                                                                                                      HelpMarker(() => ImGui.Text(T("IPAddressHelp")));
                                                                                                                  }, 220f);
                                                                             }

                                                                             Widgets.FieldRow(T("Port").TrimEnd(':'), () =>
                                                                                                                       {
                                                                                                                           ImGui.SetNextItemWidth(Widgets.Avail - 26 * GlobalFontScale);
                                                                                                                           configChanged |= ImGui.InputUInt("###port", ref Configuration.Port);
                                                                                                                           ImGui.SameLine();
                                                                                                                           HelpMarker(() => ImGui.Text(T("PortHelp")));
                                                                                                                       }, 140f);

                                                                             if (configChanged) SaveConfig(Configuration);
                                                                         }));
    }
}
