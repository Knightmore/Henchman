using ECommons.Automation;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Henchman.IPC;

[IPC(IPCNames.Lifestream)]
public static class Lifestream
{
    public enum LifestreamDestination
    {
        Home,
        FC,
        Apartment,
        Inn
    }

    [EzIPC]
    public static Func<bool> IsBusy;

    [EzIPC]
    public static Func<uint, byte, bool> Teleport;

    [EzIPC]
    public static Func<bool> TeleportToHome;

    [EzIPC]
    public static Func<bool> TeleportToFC;

    [EzIPC]
    public static Func<bool> TeleportToApartment;

    [EzIPC]
    public static Action<string> ExecuteCommand;

    [EzIPC]
    public static Action<string, string> ConnectAndOpenCharaSelect;

    public static async Task<bool> SwitchToChar(string charName, string worldName, CancellationToken token = default)
    {
        if (Player.Available && (Player.Name != charName || Player.HomeWorld != worldName))
        {
            Chat.SendMessage("/logout");
            await WaitUntilAsync(() => ProcessYesNo(true, Lang.SelectYesNoLogout), "Confirm logout", token);
            await WaitUntilAsync(() =>
                                 {
                                     unsafe
                                     {
                                         return TryGetAddonByName<AtkUnitBase>("_Title", out var addon) && addon->IsVisible;
                                     }
                                 }, "Wait for title screen", token);
        }

        ConnectAndOpenCharaSelect(charName, worldName);
        await WaitUntilAsync(() => Player.Available, "Waiting for login", token);
        return true;
    }

    public static async Task<bool> LifestreamReturn(LifestreamDestination destination, CancellationToken token = default)
    {
        if (!SubscriptionManager.IsInitialized(IPCNames.Lifestream))
            return false;
        if (!C.ReturnOnceDone)
            return false;
        switch (destination)
        {
            case LifestreamDestination.Home:
                {
                    TeleportToHome();
                    break;
                }

            case LifestreamDestination.FC:
                {
                    TeleportToFC();
                    break;
                }
            case LifestreamDestination.Apartment:
                {
                    TeleportToApartment();
                    break;
                }
            case LifestreamDestination.Inn:
                {
                    ExecuteCommand("Inn");
                    break;
                }
            default:
                return false;
        }

        await Task.Delay(GeneralDelayMs, token);
        return true;
    }
}
