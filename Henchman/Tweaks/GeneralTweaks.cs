using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System.Runtime.CompilerServices;

namespace Henchman.Tweaks;

internal static unsafe class GeneralTweaks
{
    /*
     *  Performance
     */
    internal static ref byte ActiveRenderFlag => ref Unsafe.AddByteOffset(
                                                                            ref Unsafe.AsRef<byte>(FFXIVClientStructs.FFXIV.Client.Graphics.Render.Manager.Instance()),
                                                                            0x38358
                                                                           );

    internal static uint[]? RenderDisableProcessed;
    internal static byte ForcedRenderFlag;
    internal static bool ForceRenderEnabled;

    internal static void ForceRender(IFramework framework)
    {
        ActiveRenderFlag = ForcedRenderFlag;
        RenderDisableProcessed![0] = Framework.Instance()->FrameCounter;
    }
}
