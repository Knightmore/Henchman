using AutoRetainerAPI;
using AutoRetainerAPI.Configuration;
using ECommons.EzIpcManager;

namespace Henchman.IPC;

[IPC(IPCNames.AutoRetainer)]
public static class AutoRetainer
{
    public delegate void OnCharacterPostprocessTaskDelegate();

    public delegate void OnCharacterReadyToPostprocessDelegate();

    public static AutoRetainerApi ARAPI = new();

    [EzIPC]
    public static Action<bool> SetMultiModeEnabled;

    [EzIPC]
    public static Func<List<ulong>> GetRegisteredCIDs;

    [EzIPC]
    public static Func<ulong, OfflineCharacterData> GetOfflineCharacterData;

    [EzIPC]
    public static Action<string> RequestCharacterPostprocess;

    [EzIPC]
    public static Action FinishCharacterPostprocessRequest;

    [EzIPC]
    public static Func<bool> GetSuppressed;

    [EzIPC]
    public static Action<bool> SetSuppressed;

    [EzIPC("PluginState.%m")]
    public static Func<bool> IsBusy;

    [EzIPC("PluginState.%m")]
    public static Action AbortAllTasks;

    public static event OnCharacterReadyToPostprocessDelegate OnCharacterReadyToPostProcess;

    [EzIPCEvent]
    public static void OnCharacterAdditionalTask()
    {
        OnCharacterPostprocessStep?.Invoke();
    }

    public static event OnCharacterPostprocessTaskDelegate OnCharacterPostprocessStep;

    [EzIPCEvent]
    public static void OnCharacterReadyForPostProcess()
    {
        OnCharacterReadyToPostProcess?.Invoke();
    }
}
