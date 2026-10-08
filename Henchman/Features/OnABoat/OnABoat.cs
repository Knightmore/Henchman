using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.DutyState;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Data;
using Underlings.GameHelpers;
using Underlings.TaskManager;
using Module = Underlings.Modules.Module;

namespace Henchman.Features.OnABoat;

internal class OnABoat : Module
{
    private readonly Cached<List<OfflineCharacterData>> charactersCache = new(
                                                                              () => AutoRetainer.GetRegisteredCIDs.Invoke([])
                                                                                                .Select(cid => AutoRetainer.GetOfflineCharacterData.Invoke(cid))
                                                                                                .OrderBy(x => x.ClassJobLevelArray[17])
                                                                                                .Where(x => x.ClassJobLevelArray[17] >= 1)
                                                                                                .ToList(),
                                                                              TimeSpan.FromMilliseconds(500));

    internal         bool    AskARforAccess;

    internal uint BaseIdMerchantMender = 1005422;
    internal bool CachedMultiMode;

    internal bool dutyStarted;
    internal bool EventsSubscribed;

    internal bool   InPostProcess;
    private Guid? lastManualCharacter;

    internal Vector3 PositionMerchantMender = new(-399, 3, 80);


    private static        Configuration? Configuration          => GetFeatureConfig<OnABoatUI, Configuration>();

    public unsafe InstanceContentOceanFishing.OceanFishingStatus? GetStatus
    {
        get
        {
            var framework = EventFramework.Instance();
            if (framework == null) return null;

            var oceanFishing = framework->GetInstanceContentOceanFishing();
            return oceanFishing == null ? null : oceanFishing->Status;
        }
    }

    internal       bool IsRegistrationOpen => DateTime.UtcNow.Hour % 2 == 0                          && DateTime.UtcNow.Minute <= 13;
    private unsafe bool IsInTitleScreen    => TryGetAddonByName<AtkUnitBase>("Title", out var addon) && addon->IsVisible;

    public override void RunTask() => TryStartTask(new TaskRecord(Start, "On A Boat", onDone: () => UnsubscribeEvents(), onAbort: UnsubscribeEvents, onError: OnError));

    internal async Task Start(CancellationToken token = default)
    {
        ErrorThrowIf(!Feesh.Route.IsAvailable || !Feesh.CatchTheBoat.IsAvailable || !Feesh.AutoOcean.IsAvailable ||
                     !Feesh.AutoClean.IsAvailable || !Feesh.Stop.IsAvailable,
                "Enable Feesh in WahTools to use On A Boat.");
        if (Configuration!.DiscardAfterVoyage)
        {
            if (Configuration.UseFeeshDiscard)
            {
                ErrorThrowIf(Configuration.OCFishingHandleAR,
                             "Use AutoRetainer discard with AR support. Released Feesh cannot report when its cleanup finishes.");
                ErrorThrowIf(!Feesh.AutoClean.IsAvailable, "Enable Feesh in WahTools to use its discard support.");
            }
            else
                ErrorThrowIf(!SubscriptionManager.IsInitialized(IPCNames.AutoRetainer), "Enable AutoRetainer or select WahTools for discarding.");
        }
        lastManualCharacter = null;
        if (!Configuration.OCFishingHandleAR)
        {
            var characters = Configuration.GetManualCharacters();
            ErrorThrowIf(characters.Count == 0, "Add at least one character to the On A Boat list.");
            ErrorThrowIf(characters.Any(x => string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.World)),
                         "Every On A Boat character needs a name and home world.");
            if (Player.Available && Player.TerritoryId is 900 or 1163)
                lastManualCharacter = characters.FirstOrDefault(x => x.Name == Player.Name && x.World == Player.HomeWorld.Value.Name.ExtractText())?.Id;
        }
        SubscribeEvents();
        ErrorThrowIf(!Feesh.AutoOcean.Invoke(false), "Feesh could not disable automatic voyage cycling.");
        ErrorThrowIf(!Feesh.AutoClean.Invoke(Configuration.DiscardAfterVoyage && Configuration.UseFeeshDiscard, false),
                     "Feesh could not configure post-voyage discard.");

        var state = Player.TerritoryId is 900 or 1163
                            ? OceanFishingState.FishingVoyage
                            : OceanFishingState.WaitingForVoyage;
        if (state == OceanFishingState.FishingVoyage)
            dutyStarted = true;

        while (!token.IsCancellationRequested)
        {
            switch (state)
            {
                case OceanFishingState.WaitingForVoyage:
                    await WaitUntilAsync(() => IsRegistrationOpen, "Waiting for Ocean Fishing time window", token);
                    state = OceanFishingState.CharacterSelection;
                    break;

                case OceanFishingState.CharacterSelection:
                    await SelectCharacter(token);
                    state = OceanFishingState.Preparation;
                    break;

                case OceanFishingState.Preparation:
                    await PrepareForVoyage(token);
                    state = OceanFishingState.Boarding;
                    break;

                case OceanFishingState.Boarding:
                    await BoardVoyage(token);
                    state = OceanFishingState.FishingVoyage;
                    break;

                case OceanFishingState.FishingVoyage:
                    await Fish(token);
                    state = OceanFishingState.PostVoyage;
                    break;

                case OceanFishingState.PostVoyage:
                    await PostVoyageCleanup(token);
                    state = OceanFishingState.WaitingForVoyage;
                    break;
            }
        }
    }

    internal async Task SelectCharacter(CancellationToken token = default)
    {
        if (Configuration!.OCFishingHandleAR)
        {
            if (SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
            {
                AskARforAccess = true;

                await WaitUntilAsync(() => InPostProcess || (!AutoRetainer.IsBusy.Invoke() && !Lifestream.IsBusy.Invoke() && IsInTitleScreen), "Waiting for AR PostProccess", token);
                AskARforAccess  = false;
                CachedMultiMode = AutoRetainer.GetMultiModeEnabled.Invoke();
                StopAutoRetainer();

                var lowestFisherCharacter = GetCurrentARCharacterData()
                                           .Where(x => Configuration!.EnableCharacterForOCFishing.ContainsKey(x.CID) && Configuration!.EnableCharacterForOCFishing[x.CID])
                                           .OrderBy(x => x.ClassJobLevelArray[17])
                                           .First();

                if (lowestFisherCharacter.ClassJobLevelArray[17] == Configuration.MaxLevel && Configuration!.OCFishingStopLevel)
                {
                    AutoRetainer.FinishCharacterPostprocessRequest.Invoke();
                    AutoRetainer.SetMultiModeEnabled.Invoke(true);
                    UnsubscribeEvents();
                    InPostProcess = false;
                    return;
                }

                await Lifestream.SwitchToChar(lowestFisherCharacter.Name, lowestFisherCharacter.World, Lang.SelectYesNoLogout, token);
            }
            else
                FullError("Auto Retainer not enabled! Use On A Boat - Single Character mode or enabled Auto Retainer for this feature to work!");
        }
        else
        {
            var character = Configuration.NextManualCharacter(lastManualCharacter);
            ErrorThrowIf(character is null, "Add at least one character to the On A Boat list.");
            await Lifestream.SwitchToChar(character!.Name, character.World, Lang.SelectYesNoLogout, token);
            lastManualCharacter = character.Id;
        }
    }

    internal async Task PrepareForVoyage(CancellationToken token = default)
    {
#if PRIVATE
        unsafe
        {
            UIModule.Instance()->SendChatCommand("/nastatus off");
        }
#endif

        await Task.Delay(8 * GeneralDelayMs, token);

        if (Player.ClassJob.RowId != 18)
            ErrorThrowIf(!ChangeToHighestGearsetForClassJobId(18), "No gearset for jobId 18 found");

        await Task.Delay(4 * GeneralDelayMs, token);

        if (!QuestManager.IsQuestComplete(69379))
        {
            if (SubscriptionManager.IsLoaded(IPCNames.Questionable))
            {
                await TeleportTo(8, token);
                await Questionable.CompleteQuest(69379, token);
            }
            else
                ErrorThrow($"{Player.NameWithWorld} has not unlocked ocean fishing!");
        }
    }

    internal async Task BoardVoyage(CancellationToken token = default)
    {
        ErrorThrowIf(!Feesh.Route.Invoke(Configuration!.PreferRuby ? "Ruby" : "Indigo"), "Feesh could not select the route.");
        ErrorThrowIf(!Feesh.CatchTheBoat.Invoke(), "Feesh could not start boarding.");

        await WaitUntilAsync(() =>
                             {
                                 ErrorThrowIf(!Feesh.CatchTheBoat.IsAvailable, "Feesh was disabled while boarding.");
                                 return dutyStarted || Player.TerritoryId is 900 or 1163;
                             }, "Waiting for Feesh to board the selected route", token, TimeSpan.FromMinutes(15));
        dutyStarted = true;
        await WaitUntilAsync(() => GetStatus == InstanceContentOceanFishing.OceanFishingStatus.Fishing, "Waiting for voyage to begin", token);
        await Task.Delay(2 * GeneralDelayMs, token);
    }

    internal async Task Fish(CancellationToken token = default)
    {
        while (dutyStarted)
        {
            ErrorThrowIf(!Feesh.CatchTheBoat.IsAvailable, "Feesh was disabled during the voyage.");
            await Task.Delay(GeneralDelayMs, token);
        }
    }

    internal async Task PostVoyageCleanup(CancellationToken token = default)
    {
        await Task.Delay(GeneralDelayMs * 4, token);

        await WaitUntilAsync(() => CloseIKDResult(), "Waiting for Ocean Fishing results", token);

        await WaitPulseConditionAsync(() => !IsScreenAndPlayerReady(), "Waiting for Player", token);

        await Task.Delay(Random.Shared.Next(16) * GeneralDelayMs, token);

        if (Configuration!.SellAfterVoyage && Configuration!.SellAtLocalVendor &&
            !(Configuration.DiscardAfterVoyage && Configuration.UseFeeshDiscard) && SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            await MoveToStationaryObject(PositionMerchantMender, BaseIdMerchantMender, token: token);
            unsafe
            {
                UIModule.Instance()->SendChatCommand("/ays itemsell");
            }

            await WaitWhileAsync(() => AutoRetainer.IsBusy.Invoke(), "Wait until selling finished", token);
        }

        // Feesh owns its cleanup travel; there is no released IPC to wait for it.
        if (!Configuration!.DiscardAfterVoyage || !Configuration.UseFeeshDiscard)
            await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);

        if (Configuration!.DiscardAfterVoyage)
        {
            if (!Configuration.UseFeeshDiscard)
            {
                ErrorThrowIf(!SubscriptionManager.IsInitialized(IPCNames.AutoRetainer), "AutoRetainer was disabled before discarding.");
                unsafe
                {
                    UIModule.Instance()->SendChatCommand("/ays discard");
                }

                await WaitWhileAsync(() => AutoRetainer.IsBusy.Invoke(), "Wait until discard finished", token);
            }
        }

        if (SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
        {
            if (Configuration!.OCFishingHandleAR)
            {
                if (Configuration!.SellAfterVoyage)
                {
                    unsafe
                    {
                        UIModule.Instance()->SendChatCommand("/ays itemsell");
                    }

                    await WaitWhileAsync(() => AutoRetainer.IsBusy.Invoke(), "Wait until selling finished", token);
                }

                if (InPostProcess || CachedMultiMode)
                    AutoRetainer.SetMultiModeEnabled.Invoke(true);
                else
                {
                    while (true)
                    {
                        unsafe
                        {
                            if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out _))
                                break;

                            UIModule.Instance()->SendChatCommand("/logout");
                        }

                        await Task.Delay(8 * GeneralDelayMs, token);
                    }

                    await WaitUntilAsync(() => RegexYesNo(true, Lang.SelectYesNoLogout), "Confirm logout", token);
                }

                InPostProcess = false;
            }
        }
    }

    public void OnCharacterPostProcessStep()
    {
        if (AskARforAccess)
        {
            AutoRetainer.RequestCharacterPostprocess.Invoke(Svc.PluginInterface.InternalName);
            TaskLog.Info("Requesting AR post process");
        }
        else
            TaskLog.Verbose("Outside of Voyage window. Skipping post process request.");
    }

    public void OnCharacterReadyToPostProcess()
    {
        StopAutoRetainer();
        InPostProcess = true;
    }

    private void StopAutoRetainer()
    {
        AutoRetainer.SetMultiModeEnabled.Invoke(false);
        AutoRetainer.SetSuppressed.Invoke(true);
        AutoRetainer.AbortAllTasks.Invoke();
        AutoRetainer.SetSuppressed.Invoke(false);
        AutoRetainer.FinishCharacterPostprocessRequest.Invoke();
        Log.Verbose("AutoRetainer MultiMode disabled.");
    }

    internal void SubscribeEvents()
    {
        if (EventsSubscribed) return;
        Log.Information("Subscribe");
        AutoRetainer.OnCharacterPostprocessStep    += OnCharacterPostProcessStep;
        AutoRetainer.OnCharacterReadyToPostProcess += OnCharacterReadyToPostProcess;
        Svc.DutyState.DutyStarted                  += DutyStarted;
        Svc.DutyState.DutyCompleted                += DutyCompleted;
        EventsSubscribed                           =  true;
    }

    internal void UnsubscribeEvents()
    {
        if (!EventsSubscribed) return;
        Log.Information("Unsubscribe");
        AutoRetainer.OnCharacterPostprocessStep    -= OnCharacterPostProcessStep;
        AutoRetainer.OnCharacterReadyToPostProcess -= OnCharacterReadyToPostProcess;
        Svc.DutyState.DutyStarted                  -= DutyStarted;
        Svc.DutyState.DutyCompleted                -= DutyCompleted;
        Feesh.AutoOcean.Invoke(false);
        Feesh.Stop.Invoke();
        AskARforAccess   = false;
        dutyStarted      = false;
        InPostProcess    = false;
        EventsSubscribed = false;
    }

    private void DutyStarted(IDutyStateEventArgs   args) => dutyStarted = true;
    private void DutyCompleted(IDutyStateEventArgs args) => dutyStarted = false;

    internal async Task OnError()
    {
        UnsubscribeEvents();
        if (Player.Available)
            await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone);
    }

    internal List<OfflineCharacterData> GetCurrentARCharacterData() => charactersCache.Value;

    internal static async Task<bool> CloseIKDResult()
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("IKDResult", out var addon) && IsAddonReady(addon))
            {
                addon->FireCallback(true, 0);
                return true;
            }
        }

        await Task.Delay(100);
        return false;
    }

    private enum OceanFishingState
    {
        WaitingForVoyage,
        CharacterSelection,
        Preparation,
        Boarding,
        FishingVoyage,
        PostVoyage
    }
}
