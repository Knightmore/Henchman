using ECommons.EzIpcManager;
using Henchman.Multiboxing.Command;

namespace Henchman.IPC;

[CommandGroup]
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

    public static void RunDutySupport(uint territoryType, int loops = 1, bool bareMode = true)
    {
        SetConfig("dutyModeEnum", "Support");
        Run(territoryType, loops, bareMode);
    }

    [Command]
    public static void RunDutyUsync(uint territoryType, int loops = 1, bool bareMode = true)
    {
        SetConfig("dutyModeEnum", "Regular");
        SetConfig("Unsynced", "true");
        Run(territoryType, loops, bareMode);
    }
}
