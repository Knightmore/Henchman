using ECommons.EzIpcManager;

namespace Henchman.IPC;

[IPC(IPCNames.Wrath)]
public static class Wrath
{
    public enum AutoRotationConfigOption
    {
        InCombatOnly         = 0,  // bool
        DPSRotationMode      = 1,  // enum
        HealerRotationMode   = 2,  // enum
        FATEPriority         = 3,  // bool
        QuestPriority        = 4,  // bool
        SingleTargetHPP      = 5,  // int
        AoETargetHPP         = 6,  // int
        SingleTargetRegenHPP = 7,  // int
        ManageKardia         = 8,  // bool
        AutoRez              = 9,  // bool
        AutoRezDPSJobs       = 10, // bool
        AutoCleanse          = 11, // bool
        IncludeNPCs          = 12, // bool
        OnlyAttackInCombat   = 13  //bool
    }

    public enum SetResult
    {
        Okay        = 0,
        OkayWorking = 1,

        IPCDisabled          = 10,
        InvalidLease         = 11,
        BlacklistedLease     = 12,
        Duplicate            = 13,
        PlayerNotAvailable   = 14,
        InvalidConfiguration = 15,
        InvalidValue         = 16
    }

    private static Guid? Lease;

    [EzIPC]
    internal static Func<string, string, Guid?> RegisterForLease;

    [EzIPC]
    internal static Func<string, string, string?, Guid?>
            RegisterForLeaseWithCallback;

    [EzIPC]
    internal static Func<Guid, bool, SetResult>
            SetAutoRotationState;

    [EzIPC]
    internal static Func<Guid, SetResult>
            SetCurrentJobAutoRotationReady;

    [EzIPC]
    internal static Func<Guid, AutoRotationConfigOption, object, SetResult>
            SetAutoRotationConfigState;

    [EzIPC]
    internal static Action<Guid> ReleaseControl;

    internal static Guid? CurrentLease
    {
        get
        {
            Lease ??= RegisterForLeaseWithCallback(
                                                   "Henchman",
                                                   "Henchman",
                                                   null
                                                  );
            if (Lease is null)
                InternalWarning("Failed to register for lease.");
            return Lease;
        }
    }

    public static void EnableWrathAuto()
    {
        if (!SubscriptionManager.IsInitialized(IPCNames.Wrath)) return;
        try
        {
            var lease = (Guid)CurrentLease!;
            SetAutoRotationState(lease, true);
            SetCurrentJobAutoRotationReady(lease);
        }
        catch (Exception e)
        {
            InternalError("Unknown Wrath IPC error,"                +
                  "probably inability to register a lease." +
                  "\n"                                      +
                  e.Message);
        }
    }

    public static void EnableWrathAutoAndConfigureIt()
    {
        if (!SubscriptionManager.IsInitialized(IPCNames.Wrath)) return;
        try
        {
            var lease = (Guid)CurrentLease!;
            SetAutoRotationState(lease, true);
            var setJobReady = SetCurrentJobAutoRotationReady(lease);
            SetAutoRotationConfigState(lease, AutoRotationConfigOption.OnlyAttackInCombat, false);
            SetAutoRotationConfigState(lease, AutoRotationConfigOption.InCombatOnly, false);
            SetAutoRotationConfigState(lease, AutoRotationConfigOption.DPSRotationMode, 0);

            if (setJobReady == SetResult.Okay || setJobReady == SetResult.OkayWorking)
                Log("Job has been made ready for Auto-Rotation.");
        }
        catch (Exception e)
        {
            InternalError("Unknown Wrath IPC error,"                +
                            "probably inability to register a lease." +
                            "\n"                                      +
                            e.Message);
        }
    }

    public static void DisableWrath()
    {
        if (!SubscriptionManager.IsInitialized(IPCNames.Wrath) || Lease == null) return;
        try
        {
            var lease = (Guid)CurrentLease!;
            SetAutoRotationState(lease, false);
            ReleaseControl(lease);
            Lease = null;
        }
        catch (Exception e)
        {
            InternalError("Unknown Wrath IPC error,"                +
                            "probably inability to register a lease." +
                            "\n"                                      +
                            e.Message);
        }
    }
}
