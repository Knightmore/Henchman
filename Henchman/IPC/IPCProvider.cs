using System.ComponentModel;
using ECommons.EzIpcManager;

namespace Henchman.IPC;

internal class IPCProvider
{
    public enum CancellationReason
    {
        [Description("The Wrath user manually elected to revoke your lease.")]
        WrathUserManuallyCancelled = 0,

        [Description("Your plugin was detected as having been disabled, " +
                     "not that you're likely to see this.")]
        LeaseePluginDisabled = 1,

        [Description("The Wrath plugin is being disabled.")]
        WrathPluginDisabled = 2,

        [Description("Your lease was released by IPC call, " +
                     "theoretically this was done by you.")]
        LeaseeReleased = 3,

        [Description("IPC Services have been disabled remotely. "                +
                     "Please see the commit history for /res/ipc_status.txt.\n " +
                     "https://github.com/PunishXIV/WrathCombo/commits/main/res/ipc_status.txt")]
        AllServicesSuspended = 4,

        [Description("Player job has been changed and leases will have to be reapplied.")]
        JobChanged = 5
    }
    /*
     * Retainer Vocate
     */

    //[EzIPC] public void CreateRetainers(uint amount, uint retainerJob, uint combatClass) => FeatureSet.OfType<RetainerVocateUi>().First().feature.

    /*
     * Wrath
     */
    [EzIPC]
    public void WrathComboCallback(int reason, string additionalInfo)
    {
        Warning($"Lease was cancelled for reason {reason}. " +
                $"Additional info: {additionalInfo}");

        if (reason == 0)
        {
            Error("The user cancelled our lease." +
                  "We are suspended from creating a new lease for now.");
        }
    }
}
