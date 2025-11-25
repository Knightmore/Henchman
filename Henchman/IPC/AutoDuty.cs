using ECommons.EzIpcManager;

namespace Henchman.IPC;

[IPC(IPCNames.AutoDuty)]
public static class AutoDuty
{
    [EzIPC]
    public static Func<uint, bool> ContentHasPath;

    [EzIPC]
    public static Func<bool> IsStopped;

    [EzIPC]
    public static Func<bool> IsLooping;

    [EzIPC]
    public static Action<uint, int, bool> Run;

    [EzIPC]
    public static Action<string, object> SetConfig;

    public static void RunDutySupport(uint territoryType, int loops = 0, bool bareMode = false)
    {
        SetConfig("dutyModeEnum", "Support");
        Run(territoryType, loops, bareMode);
    }
}
