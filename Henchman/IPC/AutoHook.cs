using ECommons.EzIpcManager;

namespace Henchman.IPC;

[IPC(IPCNames.AutoHook)]
public static class AutoHook
{
    [EzIPC]
    public static Action<bool> SetPluginState;

    [EzIPC]
    public static Func<bool> GetPluginState;

    [EzIPC]
    public static Action<bool> SetAutoGigState;

    [EzIPC]
    public static Action<int> SetAutoGigSize;

    [EzIPC]
    public static Action<int> SetAutoGigSpeed;

    [EzIPC]
    public static Action<string> SetPreset;

    [EzIPC]
    public static Action<string> CreateAndSelectAnonymousPreset;

    [EzIPC]
    public static Action DeleteSelectedPreset;

    [EzIPC]
    public static Action DeleteAllAnonymousPresets;
}
