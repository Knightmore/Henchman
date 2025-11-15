using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoRetainerAPI.Configuration;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Data;
using Henchman.Helpers;
using Lumina.Excel.Sheets;
#if PRIVATE
using Henchman.Features.Private;
#endif

namespace Henchman.Features.OnABoat;

internal class OnABoat
{
    public enum Bait
    {
        Ragworm = 29714,
        Krill,
        PlumpWorm,
        VersatileLure
    }

    public enum TimeOfDay
    {
        Daytime = 1,
        Sunset,
        Nighttime
    }

    private static readonly Random  Rng         = new();
    private readonly        Vector2 dryskthotaA = new(-409, 75.3f);
    private readonly        Vector2 dryskthotaB = new(-407.5f, 72f);
    internal                bool    AskARforAccess;

    public Dictionary<uint, BaitData> BaitMap = new()
                                                {
                                                        { 237, new BaitData(Bait.Krill, Bait.Ragworm, Bait.PlumpWorm, Bait.Krill) },
                                                        { 239, new BaitData(Bait.Krill, Bait.Krill, Bait.Ragworm, Bait.PlumpWorm) },
                                                        { 241, new BaitData(Bait.PlumpWorm, Bait.PlumpWorm, Bait.Ragworm, Bait.Krill) },
                                                        { 243, new BaitData(Bait.Ragworm, Bait.PlumpWorm, Bait.Ragworm, Bait.Krill) },
                                                        { 246, new BaitData(Bait.Ragworm, Bait.Krill, Bait.PlumpWorm, Bait.Krill) },
                                                        { 248, new BaitData(Bait.Krill, Bait.Ragworm, Bait.PlumpWorm, Bait.Krill) },
                                                        { 250, new BaitData(Bait.PlumpWorm, Bait.Krill, Bait.Krill, Bait.Krill) },
                                                        { 286, new BaitData(Bait.PlumpWorm, Bait.Krill, Bait.Krill, Bait.Krill) },
                                                        { 288, new BaitData(Bait.Ragworm, Bait.Krill, Bait.Ragworm, Bait.PlumpWorm) },
                                                        { 290, new BaitData(Bait.Krill, Bait.Ragworm, Bait.PlumpWorm, Bait.Krill) },
                                                        { 292, new BaitData(Bait.Krill, Bait.Ragworm, Bait.Krill, Bait.Krill) }
                                                };

    internal uint                       BaseIdDryskthota     = 1005421;
    internal uint                       BaseIdMerchantMender = 1005422;
    internal bool                       CachedMultiMode;
    private  List<OfflineCharacterData> characters = [];
    internal bool                       dutyStarted;
    internal bool                       EventsSubscribed;
    internal bool                       InPostProcess;

    internal Vector3 PositionDryskthota = new(-408, 4, 75);

    internal Vector3 PositionMerchantMender = new(-399, 3, 80);

    internal string PresetNormal       = "AH4_H4sIAAAAAAAACu1Z227jNhD9FYPPJkDdL29eN8kG9SZBnDQPi6KgyJFNRCa9FJVsGvjfC0qWbTl2LkVQwF2/KRzO4ZnhzCHNPKNBZdSQlqYc5hOUPqMTSbMCBkWBUqMr6CNrHAkJayNvTeccpW6c9NGVFkoL84RSp4/Oy5OfrKg48PWwnb9osL4pxaYWrP5w7VeNE8Z9dDa/mWoop6rgKHUI6SC/Dr2LWhJ1AMib3IbTarYnTt8h/hsEWxBVFMBMG5jvEGdzmvs2C6W5oEULEDp+B8BfTjsV5fTkCcqNhYIthkHQYRi2W0DvYTwVuflCRc3TDpTtwNhQdl+iNFhmMYxf4m6iJkvUK2oESAYbfMJtv7CbMbd11eJvGFLTFEa76ra3u5Vvb+l9M6WFoPflKX1Q2gJ0Btpw7PZ3DNfA1ANolDo2S7vqJ4xf7Pk2hy7HQaYeAKU5Lcp2M7+IyRmd1UkZyEkBumwJ2ULgKPUi4r+ItLNGvLD1/dNo2unRFVe7aTdq/Ejn59JUwgglz6iQbSqx00ejSsM3KEs6AZQi1EcXNSd0oSSgJcLTHFBqc7oDb6RK86/xrjSUsJshwmiPvVmxtq/5jOfAjKbFsNIapPmkKLdQPy3WnWwRlUr+9RUkm86o7OHeJQMqeyN4gELISa912srLTo51dpqqGhs1t4Ig5GRsYF4L8TrCZeUN9OcEtgn3YgfHj2KWUWFORVGUr9ivK1leVi3CrRQ/KrDMUORlThTTHGfcAeznAcFxlHGc+CzPYsI4yUO06KORKM1lblmWKP3+XPO1KVhJSBI50f4o/wBdUiMK6NkZFvBC6Rktvip1byFaObsDer9uOmstwdhA2vZbDjWp8p3I6mHrPDZayclH3Im34T6CCUhO9dOHEX5TVVasuC9nrHSjFqgtRzdMVn5r2h/17DDeMetGi/kHeUWB6608P8is4/sKt+U820WD3IAe0moyNSMxs+ej0xi226u+GFW6OYDtRyvsXivsQfLyyvDK6b/oo5X8tVV4DT8qoYGPDTWVPZTtNemwS/O2hKZWmhibicd6Pch6bcV8qCppNv366FIWT7cl3E1BXqj6rj94oKKw0bebviH6DvF4FHqAacZ87JMkxknAMxznNPEpdRNGHLTo71Z5f7/KX9PJo9Kzt+X9fa3y7o44avIvrsn/QT0dhfT/U2SfJqSEOhBFXoz9BHzsczfEceLHmCRJlpOQcxJHe4U02C+kv2tRFEcZPVb4UUaPRfYL3EeDmAVZnOE4JhT7PMtwwpwIOwAOcyjLc3e/jIb7ZfSqqGbz3t27rqSH9bPu2DEH2TFHWT4W2eHIchYkJCAhxYEXhNgPOMOUM4b9xHGDkPKYkBwt/mwfh5f/a/y+GmiU2v7dPEkvVXn/c3yj0N0Has/LQ4fFEQ4hYdgnnofjkHEcMRo5bhwmlBO0+Af0uqCDWx0AAA==";
    internal string PresetNormalName   = "anon_Henchman - Ocean Leveling Normal";
    internal string PresetSpectral     = "AH4_H4sIAAAAAAAACu1ZXU/jOBT9K8jPsZTvr7dOFxi0HUAUlofRauU4N61FandsB4ZB/e8rN0nbtA0MK6Sd1eatte89Pvfm5Nh1X9Co0mJMlFbjYobSF3TKSVbCqCxRqmUFFjKTE8bBTF7MuJDwRQg6R2lBSgVWk5C34Rc5St04sdC1ZEIy/YxSx0IX6vQ7Lasc8u2wiV/V+A3iC1p/cLc8urBhbKHz5e1cgpqLMkepY9udhV5f6RhkEnUA7DepjufVYt2KHKW+Y/tvMGqzRFkC1TuJzm6Y+/ayQuaMlD2dCR2/g+c3WWdMzU+fQe2sG+wRDoIO4bB9IuQBpnNW6E+ErWmbAdUOTDWhDwqlwebBHOLuoiYN6jXRDDiFnjJ8x3b3YNy9frotkmQ/YEx0LZuWRPhGttdk385JyciDOiOPQhqAzkBbnWd1x2+AikeQKHVMz44L9EAQXodA295PbHZOFus+jPisBKnaRY0UTFpk+wfVdKDilZH0dy1J583dEDLP6VZMn8jyguuKaSb4OWG8bRd2LDSpJHwBpcgMUIqQhS7XnNCl4IAahOcloNT07QjeRCj9j/GuJSg4zhBh1DNfr7ie3/KZLoFqScpxJSVw/UFV7qF+WK1H2R5UfHR1RLjgf30GTucLwk/wyRUFwk8m8Agl47OTSyEXpERWo6mpFkvjAIzPphqWayPe1tfobiQ/pqxduMNqntgiI0yfsbJUr8zfVFxdVS3CHWffKjDMUJ5nDrghxeAHMfZDSnCcU4IpEOpmnuNkuYdWFpowpa8Kw1Kh9OvLmq9pwcYkksiJ+qv8A6QimpVwYiIMYN3Sz0I8GIjWv+6BPGxfOTOrQJtC2pevGapb5TuRMcA2eaql4LP3pNveTvoEZsBzIp/fjXCn4DdRNfFtYD3SFtSkbayk2eM7aG5oqqnztrX0hnT4Hom6lWz5TgJR4HqbzD4KnaBXSDRx5mUZFRrkmFSzuZ6whdn3nHpi/y1an4kqWW+s5sPOllG7d5Acngxe2eRXFtp4XCu2G/hWMQn5VBNdmc3WHH/2FfhzQvtpPQ2y+a/JprXOsai43s2z0BUvn+8U3M+BX4r1yXr0SFhpWtQ+xh2Lje3YcyAmOAfXwb4TBziJgwJTN/LCJI5JSBK0so57qt/vqTdk9iTk4m0zHaT8P5fy4ICDbP5NBwygICQPzKkycbGfhSHOIA9xEpIw9jybJnnc64BBvwP+LllZDv43CHnwv0E2v7D/+WGQ0SBLcJCbH9m0oDihQYahoEVSeHZOAtLrf2G//12X1WJ5cj8cAgc1DyY4yOYXN8E4C7PEznAeRC72vdzFJHRsTNw4pHaROXaUoNWf7VVj88/V181A7Yvme33B2Xhg/6Vte9PbvfAMczdyfXCxV1Af+7Zt4ySKE+y4QRzQCJyCemj1Nw8lKWu/GwAA";
    internal string PresetSpectralName = "anon_Henchman - Ocean Leveling Spectral";
    internal uint   RepairId           = 720915;
    internal uint   ShopId             = 263015;

    private bool SpectralActiveCache;

    public unsafe IKDRoute CurrentRoute => Svc.Data.GetExcelSheet<IKDRoute>()
                                              .GetRow(EventFramework.Instance()->GetInstanceContentOceanFishing()->CurrentRoute);

    public unsafe int CurrentZone => (int)EventFramework.Instance()->GetInstanceContentOceanFishing()->CurrentZone;

    public uint CurrentSpot => CurrentRoute.Spot[CurrentZone].Value.SpotMain.RowId;

    public byte CurrentTimeOfDay => Svc.Data.GetExcelSheet<IKDRoute>()
                                      ?.GetRow(CurrentRoute.RowId)
                                       .Time[CurrentZone].Value.Unknown0 ??
                                    0;

    public BaitData GetBaits => BaitMap[CurrentSpot];

    public Bait GetCurrentBait => IsSpectralActive
                                          ? GetBaits.SpectralBait[(TimeOfDay)CurrentTimeOfDay]
                                          : GetBaits.NormalBait;

    public unsafe bool IsSpectralActive => EventFramework.Instance()->GetInstanceContentOceanFishing()->SpectralCurrentActive;

    public unsafe InstanceContentOceanFishing.OceanFishingStatus GetStatus => EventFramework.Instance()->GetInstanceContentOceanFishing()->Status;

    internal Vector3 GetFishingPosition => new(Rng.Next(2) == 0
                                                       ? 7
                                                       : -7, 6.711f, Rng.NextSingle() * -10);

    internal       bool IsRegistrationOpen => DateTime.UtcNow.Hour % 2 == 0                           && DateTime.UtcNow.Minute < 13;
    private unsafe bool IsInTitleScreen    => TryGetAddonByName<AtkUnitBase>("_Title", out var addon) && addon->IsVisible;

    internal async Task Start(CancellationToken token = default)
    {
        SubscribeEvents();
        AutoHook.CreateAndSelectAnonymousPreset(PresetNormal);
        AutoHook.CreateAndSelectAnonymousPreset(PresetSpectral);
        while (!token.IsCancellationRequested)
        {
            await WaitUntilAsync(() => IsRegistrationOpen, "Waiting for Ocean Fishing time window", token);
            if (C.OCFishingHandleAR)
            {
                if (SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
                {
                    AskARforAccess = true;

                    await WaitUntilAsync(() => InPostProcess || (!IPC.AutoRetainer.IsBusy() && !Lifestream.IsBusy()), "Waiting for AR PostProccess", token);
                    AskARforAccess  = false;
                    CachedMultiMode = IPC.AutoRetainer.GetMultiModeEnabled();
                    IPC.AutoRetainer.SetMultiModeEnabled(false);

                    GetCurrentARCharacterData();
                    var lowestFisherCharacter = characters.Where(x => C.EnableCharacterForOCFishing.ContainsKey(x.CID) && C.EnableCharacterForOCFishing[x.CID])
                                                          .OrderBy(x => x.ClassJobLevelArray[17])
                                                          .First();
                    if (lowestFisherCharacter.ClassJobLevelArray[17] == 100)
                    {
                        if (InPostProcess)
                        {
                            IPC.AutoRetainer.ARAPI.FinishCharacterPostProcess();
                            IPC.AutoRetainer.SetMultiModeEnabled(true);
                        }

                        return;
                    }

                    await Lifestream.SwitchToChar(lowestFisherCharacter.Name, lowestFisherCharacter.World, token);
                }
                else
                    FullError("Auto Retainer not enabled! Use On A Boat - Single Character mode or enabled Auto Retainer for this feature to work!");
            }
            else
                await Lifestream.SwitchToChar(C.OceanChar, C.OceanWorld, token);

            Chat.ExecuteCommand("/nastatus off");

            await Task.Delay(8 * GeneralDelayMs, token);

            if (Player.JobId != 18)
                ChangeToHighestGearsetForClassJobId(18);

            await Task.Delay(4 * GeneralDelayMs, token);

            if (!QuestManager.IsQuestComplete(69379))
            {
                if (SubscriptionManager.IsInitialized(IPCNames.Questionable))
                {
                    ErrorThrowIf(!Lifestream.Teleport(8, 0), "Could not teleport to Limsa Lominsa");
                    await WaitUntilAsync(() => Player.Territory == 129 && !Player.IsBusy, "Waiting for Teleport to Limsa Lominsa", token);
                    await Questionable.CompleteQuest("3843", 69379, token);
                }
                else
                    ErrorThrow($"{Player.NameWithWorld} has not unlocked ocean fishing!");
            }

            if (Player.Territory != 129)
            {
                ErrorThrowIf(!Lifestream.Teleport(8, 0), "Could not teleport to Limsa Lominsa");
                await WaitUntilAsync(() => Player.Territory == 129 && !Player.IsBusy, "Waiting for Teleport to Limsa Lominsa", token);
                bool arcGuildUnlocked;
                unsafe
                {
                    arcGuildUnlocked = UIState.Instance()->IsAetheryteUnlocked(43);
                }

                if (arcGuildUnlocked)
                {
                    if (Lifestream.AethernetTeleportById(43)) await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.BetweenAreas], "Waiting for Aethernet Transition", token);
                }
                else
                {
                    await MoveTo(new Vector3(-335f, 12f, 54f), false, token);
                    await InteractWithByBaseId(43, token);
                    await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.OccupiedInEvent], "Wait for attunement", token);
                }
            }

            if (C.UseOnlyVersatile)
            {
                var versatileAmount = InventoryHelper.GetInventoryItemCount((int)Bait.VersatileLure);
                if (versatileAmount <= 3)
                {
                    await MoveToStationaryObject(PositionMerchantMender, BaseIdMerchantMender, token: token);
                    await WaitUntilAsync(() => EventUtils.OpenEventHandler(BaseIdMerchantMender, ShopId), "Waiting to open shop", token);
                    await WaitUntilAsync(() => ShopUtils.IsShopOpen(ShopId), "Wait for Shop Open", token);
                    await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(ShopId, (uint)Bait.VersatileLure, 4 - versatileAmount), $"Buy Item {Bait.VersatileLure}", token);
                    await WaitWhileAsync(() => ShopUtils.ShopTransactionInProgress(ShopId), "Waiting for transaction", token);
                    await WaitUntilAsync(ShopUtils.CloseShop, "Close Shop", token);
                    await WaitWhileAsync(() => Player.IsBusy, "Wait for Player not busy", token);
                }
            }
            else
            {
                var ragAmount   = InventoryHelper.GetInventoryItemCount((int)Bait.Ragworm);
                var krillAmount = InventoryHelper.GetInventoryItemCount((int)Bait.Krill);
                var plumpAmount = InventoryHelper.GetInventoryItemCount((int)Bait.PlumpWorm);

                if (ragAmount < 99 || krillAmount < 99 || plumpAmount < 99)
                {
                    await MoveToStationaryObject(PositionMerchantMender, BaseIdMerchantMender, token: token);
                    await WaitUntilAsync(() => EventUtils.OpenEventHandler(BaseIdMerchantMender, ShopId), "Waiting to open shop", token);
                    await WaitUntilAsync(() => ShopUtils.IsShopOpen(ShopId), "Wait for Shop Open", token);
                    if (ragAmount < 99)
                    {
                        await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(ShopId, (uint)Bait.Ragworm, 99 - ragAmount), $"Buy Item {Bait.Ragworm}", token);
                        await WaitWhileAsync(() => ShopUtils.ShopTransactionInProgress(ShopId), "Waiting for transaction", token);
                        await Task.Delay(GeneralDelayMs * 2, token)
                                  .ConfigureAwait(true);
                    }

                    if (krillAmount < 99)
                    {
                        await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(ShopId, (uint)Bait.Krill, 99 - krillAmount), $"Buy Item {Bait.Krill}", token);
                        await WaitWhileAsync(() => ShopUtils.ShopTransactionInProgress(ShopId), "Waiting for transaction", token);
                        await Task.Delay(GeneralDelayMs * 2, token)
                                  .ConfigureAwait(true);
                    }

                    if (plumpAmount < 99)
                    {
                        await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(ShopId, (uint)Bait.PlumpWorm, 99 - plumpAmount), $"Buy Item {Bait.PlumpWorm}", token);
                        await WaitWhileAsync(() => ShopUtils.ShopTransactionInProgress(ShopId), "Waiting for transaction", token);
                        await Task.Delay(GeneralDelayMs * 2, token)
                                  .ConfigureAwait(true);
                    }

                    await WaitUntilAsync(ShopUtils.CloseShop, "Close Shop", token);
                    await WaitWhileAsync(() => ShopUtils.ShopTransactionInProgress(ShopId), "Waiting for transaction", token);
                    await WaitWhileAsync(() => Player.IsBusy, "Wait for Player not busy", token);
                }
            }

            if (InventoryHelper.GetItemAmountInNeedOfRepair(30) > 0)
            {
                await MoveToStationaryObject(PositionMerchantMender, BaseIdMerchantMender, token: token);
                await WaitUntilAsync(() => EventUtils.OpenEventHandler(BaseIdMerchantMender, RepairId), "Waiting to open repair", token);
                unsafe
                {
                    RepairManager.Instance()->RepairEquipped(true);
                }

                await Task.Delay(4 * GeneralDelayMs, token);
                unsafe
                {
                    var agentRepair = (AgentRepair*)AgentModule.Instance()->GetAgentByInternalId(AgentId.Repair);
                    agentRepair->UIModuleInterface->GetRaptureAtkModule()->CloseAddon(agentRepair->AddonId);
                }

                await WaitWhileAsync(() => Player.IsBusy, "Wait for Player not busy", token);
            }

            var randomPoint = GetRandomPoint(dryskthotaA, dryskthotaB);
            await MoveTo(new Vector3(randomPoint.X, 4, randomPoint.Y), false, token);

            await InteractWithByBaseId(BaseIdDryskthota, token);
            await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringBoardOCShip), "Waiting for boarding SelectString", token);
            if (QuestManager.IsQuestComplete(68089)) await WaitUntilAsync(() => TrySelectEntryNumber(0), "Waiting for route SelectString", token);

            await WaitUntilAsync(() => RegexYesNo(true, Lang.SelectYesNoEmbark), "Waiting for SelectYesNo Embark", token);
            await WaitUntilAsync(() => Svc.Condition[ConditionFlag.WaitingForDutyFinder], "Waiting for accepting duty finder", token);
            await WaitUntilAsync(() => TryConfirmContentsFinder(), "Waiting for contentsFinder Confirm", token);

            await WaitUntilAsync(() => dutyStarted, "Waiting for duty to start", token);
            await WaitUntilAsync(() => GetStatus == InstanceContentOceanFishing.OceanFishingStatus.Fishing, "Waiting for voyage to begin", token);
            await Task.Delay(2 * GeneralDelayMs, token);

            await WalkToRailing(token);
            await Task.Delay(4 * GeneralDelayMs, token);

            AutoHook.SetPluginState(true);

            while (dutyStarted)
            {
                if (Player.Available && Player.Territory is 900 or 1163)
                {
                    SpectralActiveCache = IsSpectralActive;
                    if (GetStatus == InstanceContentOceanFishing.OceanFishingStatus.NewZone)
                    {
                        await WaitUntilAsync(() => GetStatus == InstanceContentOceanFishing.OceanFishingStatus.Fishing, "Waiting for new zone", token);
                        while (!Svc.Condition[ConditionFlag.Fishing])
                        {
                            unsafe
                            {
                                ActionManager.Instance()->UseAction(ActionType.Action, 289);
                            }

                            await Task.Delay(GeneralDelayMs, token);
                        }
                    }

                    if (EventUtils.OceanFishingTimeLeft > 32)
                    {
                        AutoHook.SetPreset(IsSpectralActive
                                                   ? PresetSpectralName
                                                   : PresetNormalName);

                        if (C.UseOnlyVersatile)
                            ChangeBait((int)Bait.VersatileLure);
                        else
                            ChangeBait((int)GetCurrentBait);

                        unsafe
                        {
                            if (!Svc.Condition[ConditionFlag.Fishing])
                                ActionManager.Instance()->UseAction(ActionType.Action, 289);
                        }

                        await WaitUntilAsync(() => !Svc.Condition[ConditionFlag.Fishing] || SpectralActiveCache != IsSpectralActive, "Waiting for reel in", token);
                        if (SpectralActiveCache != IsSpectralActive)
                        {
                            unsafe
                            {
                                ActionManager.Instance()->UseAction(ActionType.Action, 296);
                            }
                        }
                    }
                }

                await Task.Delay(GeneralDelayMs, token);
            }

            await Task.Delay(GeneralDelayMs * 4, token);

            await WaitUntilAsync(() => CloseIKDResult(), "Waiting for Ocean Fishing results", token);

            await Task.Delay(Random.Shared.Next(16) * GeneralDelayMs, token);

            await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);

            if (C.OCFishingHandleAR && SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
            {
                if (C.DiscardAfterVoyage)
                {
                    Chat.ExecuteCommand("/ays discard");
                    await WaitWhileAsync(IPC.AutoRetainer.IsBusy, "Wait until discard finished", token);
                }

                if (InPostProcess)
                {
                    IPC.AutoRetainer.ARAPI.FinishCharacterPostProcess();
                    IPC.AutoRetainer.SetMultiModeEnabled(true);
                }
                else if (CachedMultiMode)
                    IPC.AutoRetainer.SetMultiModeEnabled(true);
            }
        }
    }

    internal async Task WalkToRailing(CancellationToken token = default)
    {
#if PRIVATE
        var positionalData = ReleaseUtils.GetRandomFishingPositionWithRotation();
        var position       = positionalData.position;
        var rotation       = positionalData.rotation;
#else
        var position = GetFishingPosition;
        var rotation = position.X > 0 ? 1.5f : -1.5F;
#endif
        await MoveTo(position, token: token);
        unsafe
        {
            Player.GameObject->SetRotation(rotation);
        }
    }

    public void OnCharacterPostProcessStep()
    {
        if (AskARforAccess)
        {
            IPC.AutoRetainer.ARAPI.RequestCharacterPostprocess();
            Log("Requesting AR post process");
        }
        else
            Verbose("Outside of Voyage window. Skipping post process request.");
    }

    public void OnCharacterReadyToPostProcess()
    {
        IPC.AutoRetainer.SetMultiModeEnabled(false);
        IPC.AutoRetainer.SetSuppressed(true);
        IPC.AutoRetainer.AbortAllTasks();
        IPC.AutoRetainer.SetSuppressed(false);
        PluginLog.Verbose("AutoRetainer MultiMode disabled.");
        InPostProcess = true;
    }

    internal void SubscribeEvents()
    {
        if (EventsSubscribed) return;
        PluginLog.Log("Subscribe");
        IPC.AutoRetainer.ARAPI.OnCharacterPostprocessStep    += OnCharacterPostProcessStep;
        IPC.AutoRetainer.ARAPI.OnCharacterReadyToPostProcess += OnCharacterReadyToPostProcess;
        Svc.DutyState.DutyStarted                            += DutyStarted;
        Svc.DutyState.DutyCompleted                          += DutyCompleted;
        EventsSubscribed                                     =  true;
    }

    internal void UnsubscribeEvents()
    {
        if (!EventsSubscribed) return;
        PluginLog.Log("Unsubscribe");
        IPC.AutoRetainer.OnCharacterPostprocessStep    -= OnCharacterPostProcessStep;
        IPC.AutoRetainer.OnCharacterReadyToPostProcess -= OnCharacterReadyToPostProcess;
        Svc.DutyState.DutyStarted                      -= DutyStarted;
        Svc.DutyState.DutyCompleted                    -= DutyCompleted;
        AutoHook.DeleteAllAnonymousPresets();
        EventsSubscribed = false;
    }

    private void DutyStarted(object?   sender, ushort e) => dutyStarted = true;
    private void DutyCompleted(object? sender, ushort e) => dutyStarted = false;

    internal async Task OnError()
    {
        UnsubscribeEvents();
        if (Player.Available)
            await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone);
    }

    internal List<OfflineCharacterData> GetCurrentARCharacterData()
    {
        characters.Clear();
        var cids = IPC.AutoRetainer.GetRegisteredCIDs();
        foreach (var cid in cids) characters.Add(IPC.AutoRetainer.GetOfflineCharacterData(cid));

        characters = characters.OrderBy(x => x.ClassJobLevelArray[17])
                               .ToList();
        return characters;
    }

    internal static async Task<bool> CloseIKDResult()
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("IKDResult", out var addon) && IsAddonReady(addon))
            {
                Callback.Fire(addon, true, 0);
                return true;
            }
        }

        await Task.Delay(100);
        return false;
    }

    internal struct BaitData(Bait normal, Bait daytime, Bait sunset, Bait nighttime)
    {
        public Bait NormalBait = normal;

        public Dictionary<TimeOfDay, Bait> SpectralBait = new()
                                                          {
                                                                  { TimeOfDay.Daytime, daytime },
                                                                  { TimeOfDay.Sunset, sunset },
                                                                  { TimeOfDay.Nighttime, nighttime }
                                                          };
    }
}
