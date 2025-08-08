using ECommons.EzIpcManager;

namespace Henchman.IPC;

[IPC(IPCNames.BossMod)]
public static class Bossmod
{
    [EzIPC("Presets.%m")]
    public static Func<string, string?> Get;

    [EzIPC("Presets.%m")]
    public static Func<string, bool, bool> Create;

    [EzIPC("Presets.%m")]
    public static Func<string, bool> Delete;

    [EzIPC("Presets.%m")]
    public static Func<string> GetActive;

    [EzIPC("Presets.%m")]
    public static Func<string, bool> SetActive;

    [EzIPC("Presets.%m")]
    public static Func<bool> ClearActive;

    [EzIPC("Presets.%m")]
    public static Func<bool> GetForceDisabled;

    [EzIPC("Presets.%m")]
    public static Func<bool> SetForceDisabled;

    [EzIPC("Presets.%m")]
    public static Func<bool> Activate;

    [EzIPC("Presets.%m")]
    public static Func<bool> Deactivate;

    [EzIPC("Presets.%m")]
    public static Func<List<string>> GetActiveList;

    [EzIPC("Presets.%m")]
    public static Func<List<string>, bool> SetActiveList;

    [EzIPC("Presets.%m")]
    public static Func<string, string, string, string, bool> AddTransientStrategy;

    [EzIPC("Presets.%m")]
    public static Func<string, string, string, string, int, bool> AddTransientStrategyTargetEnemyOID;

    [EzIPC("Presets.%m")]
    public static Func<string, string, string, bool> ClearTransientStrategy;

    [EzIPC("Presets.%m")]
    public static Func<string, string, bool> ClearTransientModuleStrategies;

    [EzIPC("Presets.%m")]
    public static Func<string, bool> ClearTransientPresetStrategies;

    public static void EnableRotation()
    {
        if (SubscriptionManager.IsInitialized(IPCNames.BossMod))
            SetActive("VBM Default");
    }

    public static void DisableRotation()
    {
        if (SubscriptionManager.IsInitialized(IPCNames.BossMod))
            ClearActive();
    }

    public static void EnableAI()
    {
        if (SubscriptionManager.IsInitialized(IPCNames.BossMod))
            Svc.Commands.ProcessCommand("/vbmai on");

        // Putting this here as the internal name and the IPC prefix of BMR are messed up
        if (SubscriptionManager.IsLoaded("BossModReborn"))
            Svc.Commands.ProcessCommand("/bmrai on");
    }

    public static void DisableAI()
    {
        if (SubscriptionManager.IsInitialized(IPCNames.BossMod))
            Svc.Commands.ProcessCommand("/vbmai off");

        // Putting this here as the internal name and the IPC prefix of BMR are messed up
        if (SubscriptionManager.IsLoaded("BossModReborn"))
            Svc.Commands.ProcessCommand("/bmrai off");
    }
}
