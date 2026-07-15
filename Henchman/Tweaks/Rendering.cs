using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace Henchman.Tweaks;

internal static unsafe class Rendering
{
    internal static uint[]? RenderDisableProcessed;
    internal static bool    ForcedRenderFlag;

    internal static bool ForceRenderEnabled;

    internal static bool DisableRenderWhenUnfocused;
    private static  bool LastWindowInactive;

    /*
     *  Performance
     */
    internal static ref bool RenderDisabled => ref Manager.Instance()->Is3DRenderingDisabled;

    internal static void SetRender(bool enabled)
    {
        ForcedRenderFlag = !enabled;
        RenderDisabled   = ForcedRenderFlag;
    }

    internal static void SetForceRenderEnabled(bool enabled)
    {
        if (ForceRenderEnabled == enabled)
        {
            if (!enabled) Svc.Framework.Update -= ForceRender;
            return;
        }

        ForceRenderEnabled = enabled;
        if (ForceRenderEnabled)
        {
            RenderDisableProcessed ??= Svc.PluginInterface.GetOrCreateData<uint[]>(
                                                                                   "ECommons.RenderDisableProcessingFramecount",
                                                                                   () => [0]
                                                                                  );
            ForcedRenderFlag     =  RenderDisabled;
            Svc.Framework.Update -= ForceRender;
            Svc.Framework.Update += ForceRender;
        }
        else
        {
            Svc.Framework.Update -= ForceRender;
            SetRender(true);
        }
    }

    internal static void ForceRender(IFramework framework)
    {
        RenderDisabled             = ForcedRenderFlag;
        RenderDisableProcessed![0] = Framework.Instance()->FrameCounter;
    }

    internal static void SetDisableRenderWhenUnfocused(bool enabled)
    {
        if (DisableRenderWhenUnfocused == enabled) return;

        DisableRenderWhenUnfocused = enabled;
        if (enabled)
        {
            LastWindowInactive   =  Framework.Instance()->WindowInactive;
            Svc.Framework.Update -= CheckWindowFocus;
            Svc.Framework.Update += CheckWindowFocus;
            if (LastWindowInactive) SetRender(false);
        }
        else
        {
            Svc.Framework.Update -= CheckWindowFocus;
            SetRender(true);
        }
    }

    private static void CheckWindowFocus(IFramework framework)
    {
        var windowInactive = Framework.Instance()->WindowInactive;
        if (windowInactive == LastWindowInactive) return;
        LastWindowInactive = windowInactive;
        SetRender(!windowInactive);
    }
}
