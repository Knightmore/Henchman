using System.Threading;
using System.Threading.Tasks;
using ECommons.Automation;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Data;
using Henchman.TaskManager;

namespace Henchman.IPC;

[IPC(IPCNames.Lifestream)]
public static class Lifestream
{
    public enum LifestreamDestination
    {
        Home,
        FC,
        Apartment,
        Inn,
        Auto
    }

    [EzIPC]
    public static Func<bool> IsBusy;

    [EzIPC]
    public static Func<IDalamudPlugin> Instance;

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

    [EzIPC]
    public static Func<ulong, (HousePathData Private, HousePathData FC)> GetHousePathData;

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
        if (Player.Available && Player.Name == charName && Player.HomeWorld.Value.Name.ExtractText()== worldName) return;
        var inTitleMenu = false;
        unsafe
        {
            inTitleMenu = TryGetAddonByName<AtkUnitBase>("_TitleMenu", out var addon) && addon->IsVisible;
        }

        if (!Player.Available)
        {
            if (inTitleMenu)
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
                if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out _))
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
                if (TeleportToHome())
                    await WaitPulseConditionAsync(() => !IsScreenAndPlayerReady(), "Waiting for Area transistion", token);
                break;
            }

            case LifestreamDestination.FC:
            {
                if (TeleportToFC())
                    await WaitPulseConditionAsync(() => !IsScreenAndPlayerReady(), "Waiting for Area transistion", token);
                break;
            }
            case LifestreamDestination.Apartment:
            {
                if (TeleportToApartment())
                    await WaitPulseConditionAsync(() => !IsScreenAndPlayerReady(), "Waiting for Area transistion", token);
                break;
            }
            case LifestreamDestination.Inn:
            {
                ExecuteCommand("inn");
                await WaitPulseConditionAsync(() => !IsScreenAndPlayerReady(), "Waiting for Area transistion", token);
                break;
            }
            case LifestreamDestination.Auto:
            {
                ExecuteCommand("auto");
                await WaitPulseConditionAsync(() => !IsScreenAndPlayerReady(), "Waiting for Area transistion", token);
                break;
            }
            default:
                return false;
        }

        await Task.Delay(GeneralDelayMs, token);
        await WaitWhileAsync(() => IsBusy(), "Wait for Lifestream", token);
        if ((C is { ReturnOnceDone: true, ReturnTo: LifestreamDestination.FC } &&
             GetHousePathData(Player.CID)
                    .FC is { PathToEntrance.Count: > 0 }) ||
            (C is { ReturnOnceDone: true, ReturnTo: LifestreamDestination.Home } &&
             GetHousePathData(Player.CID)
                    .Private is { PathToEntrance.Count: > 0 })) await WaitPulseConditionAsync(() => !IsScreenAndPlayerReady(), "Waiting for House transistion", token);
        return true;
    }

    [Serializable]
    public class HousePathData
    {
        public ulong         CID;
        public bool          EnableHouseEnterModeOverride = false;
        public int           EnterModeOverride            = 0;
        public bool          IsPrivate;
        public List<Vector3> PathToEntrance = [];
        public List<Vector3> PathToWorkshop = [];
        public int           Plot;
        public int           ResidentialDistrict;
        public int           Ward;
    }
}
