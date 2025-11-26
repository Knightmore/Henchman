using System.ComponentModel;
using ECommons.EzIpcManager;

namespace Henchman.IPC;

[IPC(IPCNames.RotationSolverReborn)]
public static class RotationSolverReborn
{
    public enum StateCommandType : byte
    {
        /// <summary>
        ///     Stop the addon. Always remember to turn it off when it is not in use!
        /// </summary>
        [Description("Stop the addon. Always remember to turn it off when it is not in use!")]
        Off,

        /// <summary>
        ///     Start the addon in Auto mode. When out of combat or when combat starts, switches the target according to the set
        ///     condition.
        /// </summary>
        [Description("Start the addon in Auto mode. When out of combat or when combat starts, switches the target according to the set condition. " +
                     "\r\n Optionally: You can add the target type to the end of the command you want RSR to do. For example: /rotation Auto Big")]
        Auto,

        /// <summary>
        ///     Start the addon in Target-Only mode. RSR will auto-select targets per normal logic but will not perform any
        ///     actions.
        /// </summary>
        [Description("Start in Target-Only mode. RSR will auto-select targets per normal logic but will not perform any actions.")]
        TargetOnly,

        /// <summary>
        ///     Start the addon in Manual mode. You need to choose the target manually. This will bypass any engage settings that
        ///     you have set up and will start attacking immediately once something is targeted.
        /// </summary>
        [Description("Start the addon in Manual mode. You need to choose the target manually. This will bypass any engage settings that you have set up and will start attacking immediately once something is targeted.")]
        Manual,

        /// <summary>
        /// </summary>
        [Description("This mode is managed by the Autoduty plugin")]
        AutoDuty
    }

    [EzIPC]
    public static Action<byte> ChangeOperatingMode;

    [EzIPC]
    public static Action<string> Test;

    public static void Enable()
    {
        if (SubscriptionManager.IsInitialized(IPCNames.RotationSolverReborn))
        {
            ChangeOperatingMode((byte)StateCommandType.Manual);
            Svc.Commands.ProcessCommand("/rotation Settings HostileType 0");
        }
    }

    public static void Disable()
    {
        if (SubscriptionManager.IsInitialized(IPCNames.RotationSolverReborn))
            ChangeOperatingMode((byte)StateCommandType.Off);
    }
}
