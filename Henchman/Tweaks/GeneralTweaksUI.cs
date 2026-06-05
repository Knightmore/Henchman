using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Henchman.Abstractions;
using static Henchman.Tweaks.GeneralTweaks;

namespace Henchman.Tweaks;

[Feature]
public partial class GeneralTweaksUI : FeatureUI
{
    public override string Name => "General";
    public override Category Category => Category.Tweaks;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Box;

    public override Action? Help => () =>
                                    {
                                        ImGui.Text(T("HelpText"));
                                    };

    public override bool LoginNeeded => false;

    public override void Draw()
    {
        DrawSection(T("SectionPerformance"), DrawPerformanceSection);
        //DrawSection("AutoFire", rsAuto.Draw);
    }

    public void DrawPerformanceSection()
    {
        ref var activeRender = ref ActiveRenderFlag;
        var current = activeRender != 0;

        if (ImGui.Checkbox(T("DisableRender"), ref current))
        {
            activeRender = current
                                   ? (byte)1
                                   : (byte)0;
            ForcedRenderFlag = activeRender;
        }
        ImGui.SameLine();
        HelpMarker(() => ImGui.Text(T("DisableRenderHelp")));

        if (ImGui.Checkbox(T("ForceRender"), ref ForceRenderEnabled))
        {
            if (ForceRenderEnabled)
            {
                RenderDisableProcessed ??= Svc.PluginInterface.GetOrCreateData<uint[]>(
                                                                                       "ECommons.RenderDisableProcessingFramecount",
                                                                                       () => [0]
                                                                                      );

                Svc.Framework.Update += ForceRender;
            }
            else
            {
                Svc.Framework.Update -= ForceRender;
                ActiveRenderFlag = 0;
            }
        }
        ImGui.SameLine();
        HelpMarker(() => ImGui.Text(T("ForceRenderHelp")));
    }

    public override void Dispose()
    {
        ActiveRenderFlag = 0;
        if (ForceRenderEnabled)
        {
            Svc.Framework.Update -= ForceRender;
            ForceRenderEnabled = false;
        }
    }
}
