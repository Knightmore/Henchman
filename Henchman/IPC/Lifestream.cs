using ECommons.Automation;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Data;
using Henchman.TaskManager;
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

    /// <summary>
    ///     Requests aethernet teleport to be executed by ID from <see cref="Aetheryte" /> sheet, if possible.
    /// </summary>
    /// <param name="aetheryteID">Aetheryte ID</param>
    /// <param name="subIndex">Used for housing</param>
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
    public static Func<string, string, bool> ConnectAndOpenCharaSelect;

    /// <summary>
    ///     Requests character screen by name and world, if possible. Must be at the title menu.
    /// </summary>
    /// <param name="characterName"></param>
    /// <param name="worldName"></param>
    /// <returns></returns>
    [EzIPC]
    public static Func<string, string, bool> ConnectAndLogin;

    /// <summary>
    ///     Requests aethernet teleport to be executed by ID from <see cref="Aetheryte" /> sheet, if possible. Must be within
    ///     an aetheryte or aetheryte shard range.
    /// </summary>
    /// <param name="aethernetSheetRowId"></param>
    /// <returns></returns>
    [EzIPC]
    public static Func<uint, bool> AethernetTeleportById;

    [EzIPC]
    public static Func<string, bool> ChangeWorld;

    public static async Task SwitchToChar(string charName, string worldName, CancellationToken token = default)
    {
        if (Player.Available && Player.Name == charName && Player.HomeWorld == worldName) return;
        bool inTitleMenu = false;
        unsafe
        {
            inTitleMenu = TryGetAddonByName<AtkUnitBase>("_TitleMenu", out var addon) && addon->IsVisible;
        }
        if (!Player.Available)
        {
            if(inTitleMenu)
            {
                ErrorThrowIf(!ConnectAndLogin(charName, worldName), $"Can not connect to character {charName} on {worldName}");
                await WaitUntilAsync(() => Player.Available && !Player.IsBusy, "Waiting for login", token);
                return;
            }

            await WaitUntilAsync(() => Player.Available, "Wait for player avaialable", token);
        }

        while (true)
        {
            unsafe
            {
                if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out var _))
                    break;
            }

            Chat.SendMessage("/logout");
            await Task.Delay(1000, token);
        }

        await WaitUntilAsync(() => ProcessYesNo(true, Lang.SelectYesNoLogout), "Confirm logout", token);
        await WaitUntilAsync(() =>
                             {
                                 unsafe
                                 {
                                     return TryGetAddonByName<AtkUnitBase>("_TitleMenu", out var addon) && addon->IsVisible;
                                 }
                             }, "Wait for title screen", token);

        ErrorThrowIf(!ConnectAndLogin(charName, worldName), $"Can not connect to character {charName} on {worldName}");
        await WaitUntilAsync(() => Player.Available && !Player.IsBusy, "Waiting for login", token);
    }

    public static async Task<bool> LifestreamReturn(LifestreamDestination destination, bool returnCriteria, CancellationToken token = default)
    {
        if (!SubscriptionManager.IsInitialized(IPCNames.Lifestream))
            return false;
        if (!returnCriteria)
            return false;
        using var scope = new TaskDescriptionScope($"Returning to {destination}");
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
        await WaitWhileAsync(() => IsBusy(), "Wait for Lifestream", token);
        return true;
    }
}
