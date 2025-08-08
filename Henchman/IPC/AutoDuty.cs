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
}
