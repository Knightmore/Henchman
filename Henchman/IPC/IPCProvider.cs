using System.ComponentModel;
using System.Linq;
using ECommons.EzIpcManager;
using Henchman.Features.RetainerVocate;

namespace Henchman.IPC;

internal class IPCProvider
{
    /*
     * General
     */

    [EzIPC]
    public bool IsBusy => Running;

    /*
     * Wrath
     */
    [EzIPC]
    public void WrathComboCallback(int reason, string additionalInfo)
    {
        FullWarning($"Lease was cancelled for reason {reason}. " +
                $"Additional info: {additionalInfo}");

        if (reason == 0)
        {
            FullError("The user cancelled our lease." +
                  "We are suspended from creating a new lease for now.");
        }
    }
}
