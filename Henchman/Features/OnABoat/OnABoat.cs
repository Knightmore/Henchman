using System.Linq;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Lumina.Excel.Sheets;
using System.Threading;
using System.Threading.Tasks;
using AutoRetainerAPI.Configuration;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Data;
using Henchman.Helpers;

namespace Henchman.Features.OnABoat;

internal class OnABoat
{
    private List<OfflineCharacterData> characters = [];
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

    internal struct BaitData(Bait normal, Bait daytime, Bait sunset, Bait nighttime)
    {
        public Bait NormalBait = normal;

        public Dictionary<TimeOfDay, Bait> SpectralBait = new()
                                                          {
                                                                  { TimeOfDay.Daytime, daytime},
                                                                  { TimeOfDay.Sunset, sunset },
                                                                  { TimeOfDay.Nighttime, nighttime }
                                                          };
    }

    internal string PresetNormal     = "AH4_H4sIAAAAAAAACu1Z227bOBD9FYPPJiCKFHV5c71pGqybBnW6fSgWC0oa2URk0aWotNnA/76gLral2LksggLZ9ZtCzhyeGR4e0co9mlRGTUVpymm2QNE9OitEnMMkz1FkdAVjZCdnsoDdZNpNXaQocoNwjK60VFqaOxSRMbooz34meZVCuhu28ZsG66NSydKC1Q+ufapxeDBG5+vrpYZyqfIURcRxesiPQx+iFvo9AOdJbtNltTpSJyMOGxBk3OvhdyAqzyExXWGMOGQ/zH2ahdKpFHkHwAnrAbA27L0sl2d3UO4t5A0Yel6vhbzbAnED86XMzDsha552oOwG5kYkNyWKvLaLPHiIu48atqhXwkgoEtjjw4d5vL+lbpeq5d8wFaYRRrfqMNsdCIK22ddLkUtxU74Xt0pbgN5AV47d/t7EZ0jULWgUEdulQ/rhwYM9H3Loc5zE6hZQlIm87DbznVyci1XdlEmxyEGXHSErhBRF1HfYg0p7awQbq++fRoveGd1ytZt2reY/xPqiMJU0UhXnQhZdKzEZo1ml4SOUpVgAihAao8uaE7pUBaAW4W4NKLI9PYA3U6X513hXGko4zBBhdGS+WbGe3/GZryExWuTTSmsozCtVOUB9tVoPskWiUMVfH6BIlitRjPDoUwKiGM3gFnJZLEZd0qAvBznW3WlUNTdqbQ1BFou5gXVtxLsKW+VN9OsUtg9Xc/hSyO8VWFzEaELjmHKcMZdiJsIAB5krsMc5T6nrCE59tBmjmSzNp8yuUaLo2329mi1gawChT/zjHP8AXQojcxjZCAt4qfRK5B+UurEQnRl9BXGzOzJ2tgRja+gOTzvUFMqIb92sS54brYrFS9Idupc+gwUUqdB3L0b4TVVxvuXeRmxPfW0vg0SXh9u8He2XZvYYH4i61nL9Ql6+59Jt5guZ9XIf4dbG2TMwyQzoqagWSzOTK/t2I83E8HDU15pKN69P+9DZMu1s2QuH5v/oDWIzRlvz6lT4Gb5XUkM6N8JU9pVqLzlvW5pfSmi00tTYBJ70+ib1umfbTko4cVLAsZNQzOLAx2GSBdgNaEJFFmduyNFmfNin2XGf/iwWP5RePW3QzxP7szV9ctX/uav+Aj2drPA/aYWe5/up8ENMaeZjFjAXh14c40SEoZs4wokpOWqF3nEr/F3LPD8Z4UmjJyM8iexN3AkzlogkSLBLiY8ZkBAHSShwmjGPeCSgHrhHjZAfN8KrvFqtR1+fdS18Wz+OTpp/k5o/GetJZL/UWMM0dDIWYO6TFDMacywyP8Wp5waxw2LmZQRt/uw+krb/Mfu2HWi81v7dfJVtffX4R+XGY/sfat2Qp0kALiYMKGY8FjhIHQ8LPwiY8AhPaIY2/wAgbFEpIRwAAA==";
    internal string PresetNormalName = "anon_Henchman - Ocean Leveling Normal";
    internal string PresetSpectral   = "AH4_H4sIAAAAAAAACu1ZTW/bOBD9KwbPIiBK1Id1S71pGqybBnW6PRSLBUWOZCKy6FJU2jTwf1/QkmzLsZJmNz0sVjeZnHnzhnp8FOgHdFYbNWOVqWZZjpIHdF6ytICzokCJ0TU4yE7OZQl28jIvlYb3SvElSjJWVOC0CaILvxQo8eKpg661VFqae5QQB11W5995UQsQ+2Ebv2nwW8QHtH3w9jz6sGHsoIv1zVJDtVSFQAlx3V6hpyudgpxGPQD3WaqzZb3aLoVACSUufYZRl6WKArg5SCSHYd7zZZUWkhUDKxMS2sOjbdZbWS3P76E6qBscEQ6CHuGweyPsFhZLmZk3TG5p24GqG1gYxm8rlAS7F/MY9xB12qJeMyOh5HDAJzzOC/sL6HWpWv6AGTONTrqqx9ne0fL7bfbNkhWS3VZv2Z3SFqA30LXjO/3xj8DVHWiUELtIpxX5SAF+j0C3nm9kfsFW28bPyrwAXXVF7bu3aZFLH3XTg4o3VsPfjWa9rbojZF/MjVp8Y+vL0tTSSFVeMFl2y4WJg+a1hvdQVSwHlCDkoKstJ3SlSkAtwv0aUGLX7QTeXFXmH+Nda6jgNEOE0cB8U3E7v+ezWAM3mhWzWmsozSt1eYT6ar2eZPuo45PVEStV+dc7KPlyxcoJnnzgwMrJHO6gkGU+uVJ6xQrktJpaGLW2W16W+cLAeuu8+/5a3Z3p12nrEG7bzadSfq3B4qJMpF7sxzGmIFxMWRziNEg55iEA+LEbxSREGwfNZWU+ZLZGhZIvD9tqtoHdFp9GJBrm+AfoihlZwMRGWMBmQd4pdWshOrv5DOx2v2HsbAXG9tBtnXaoaZSSyPpVl7wwWpX5S9Jd/yB9DjmUgun7FyN8quA3VbfxXWAz0jXUpu2MoD2Se2heaLtp8va9DIb0+J6IutFy/UICUeD5u8whCr2gJ0i0cVbqZ5kBPWN1vjRzubLHFGkmjvfA9hOm1s05aB8ODL/x3mD6+CB/4kzeOGjnUJ3YPsLXWmoQC8NMbc9G+7VyrMCfE9pP62mUzX9NNgcmyTgjQeRznDI3xjQLBGaMulgA50BTX3ipizbOaVekw674keXflF49b4ejGP/nYhw9bJTNv/MwX7jMTQnFkIYRpiJjOA5SgmlGqBtmfhR66aCHBcMe9ruWRTE62CjF0cFG2fxSBwu4iFMiYhz4PmCaZR5OXRphFpGUh2HK0mw66GDhsINdF/VqPfk8foiNehxtbJTNr7axyJtmWepFmNKIY+q7FE/pNMARETQUbuAzIGjzZ3fl1v7h8mU30Dib/d3c8bUuNnz12N1X9i/+mKDcE36GMw5TTD1OMEs9hoUv/CggQRjQEG3+BtAszL52GgAA";
    internal string PresetSpectralName = "anon_Henchman - Ocean Leveling Spectral";

    internal Vector3 PositionDryskthota     = new(-408, 4, 75);
    internal uint     BaseIdDryskthota       = 1005421;

    internal Vector3 PositionMerchantMender = new(-399, 3, 80);
    internal uint    BaseIdMerchantMender   = 1005422;
    internal uint    ShopId                 = 263015;
    internal uint    RepairId               = 720915;
    internal bool    eventsSubscribed;
    internal bool    askARforAccess = false;
    internal bool    inPostProcess  = false;
    internal bool    dutyStarted    = false;
    internal async Task Start(CancellationToken token = default)
    {
        SubscribeEvents();
        AutoHook.CreateAndSelectAnonymousPreset(PresetNormal);
        AutoHook.CreateAndSelectAnonymousPreset(PresetSpectral);
        while (!token.IsCancellationRequested)
        {
            await WaitUntilAsync(() => IsRegistrationOpen, "Waiting for Ocean Fishing time window", token);
            if (C.OCFishingHandleAR && SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
            {
                askARforAccess = true;
                
                await WaitUntilAsync(() => inPostProcess || IsInTitleScreen && !IPC.AutoRetainer.IsBusy(), "Waiting for AR PostProccess", token);
                GetCurrentARCharacterData();
                var lowestFisherCharacter = characters.Where(x => C.EnableCharacterForOCFishing.ContainsKey(x.CID) && C.EnableCharacterForOCFishing[x.CID])
                                                      .OrderBy(x => x.ClassJobLevelArray[17])
                                                      .First();
                await Lifestream.SwitchToChar(lowestFisherCharacter.Name, lowestFisherCharacter.World, token);
            }
            else
            {
                await Lifestream.SwitchToChar(C.OceanChar, C.OceanWorld, token);
            }

            if (!QuestManager.IsQuestComplete(69379))
            {
                if (SubscriptionManager.IsInitialized(IPCNames.Questionable))
                {
                    ErrorThrowIf(!Lifestream.Teleport(8, 0), "Could not teleport to Limsa Lominsa");
                    await WaitUntilAsync(() => Player.Territory == 129 && !Player.IsBusy, "Waiting for Teleport to Limsa Lominsa", token);
                    await Questionable.CompleteQuest("3843", 69379, token);
                }
                else
                {
                    ErrorThrow($"{Player.NameWithWorld} has not unlocked ocean fishing!");
                }
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
                    if (Lifestream.AethernetTeleportById(43))
                    {
                        await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.BetweenAreas], "Waiting for Aethernet Transition", token);
                    }
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
                    await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(ShopId, (uint)Bait.VersatileLure, 4-versatileAmount), $"Buy Item {Bait.VersatileLure}", token);
                    await WaitWhileAsync(() => ShopUtils.ShopTransactionInProgress(ShopId), "Waiting for transaction", token);
                    await WaitUntilAsync(ShopUtils.CloseShop, "Close Shop", token);
                    await WaitWhileAsync(() => Player.IsBusy, "Wait for Player not busy", token);
                }
            }
            else
            {
                var ragAmount = InventoryHelper.GetInventoryItemCount((int)Bait.Ragworm);
                var krillAmount = InventoryHelper.GetInventoryItemCount((int)Bait.Krill);
                var plumpAmount = InventoryHelper.GetInventoryItemCount((int)Bait.PlumpWorm);

                if (ragAmount < 99 || krillAmount < 99 || plumpAmount < 99)
                {
                    await MoveToStationaryObject(PositionMerchantMender, BaseIdMerchantMender, token: token);
                    await WaitUntilAsync(() => EventUtils.OpenEventHandler(BaseIdMerchantMender, ShopId), "Waiting to open shop", token);
                    await WaitUntilAsync(() => ShopUtils.IsShopOpen(ShopId), "Wait for Shop Open", token);
                    if (ragAmount < 99)
                    {
                        await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(ShopId, (uint)Bait.Ragworm, 99-ragAmount), $"Buy Item {Bait.Ragworm}", token);
                        await Task.Delay(GeneralDelayMs * 2, token).ConfigureAwait(true);
                    }

                    if (krillAmount < 99)
                    {
                        await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(ShopId, (uint)Bait.Krill, 99 - krillAmount), $"Buy Item {Bait.Krill}", token);
                        await Task.Delay(GeneralDelayMs * 2, token).ConfigureAwait(true);
                    }

                    if (plumpAmount < 99)
                    {
                        await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(ShopId, (uint)Bait.PlumpWorm, 99 - plumpAmount), $"Buy Item {Bait.PlumpWorm}", token);
                        await Task.Delay(GeneralDelayMs * 2, token).ConfigureAwait(true);
                    }
                    await WaitUntilAsync(ShopUtils.CloseShop, "Close Shop", token);
                    await WaitWhileAsync(() => ShopUtils.ShopTransactionInProgress(ShopId), "Waiting for transaction", token);
                    await WaitWhileAsync(() => Player.IsBusy, "Wait for Player not busy", token);
                }
            }

            if (InventoryHelper.GetItemAmountInNeedOfRepair(30) > 0)
            {
                await MoveToStationaryObject(PositionMerchantMender, BaseIdMerchantMender, token: token);
                await WaitUntilAsync(() => EventUtils.OpenEventHandler(BaseIdMerchantMender, ShopId), "Waiting to open repair", token);
                unsafe
                {
                    RepairManager.Instance()->RepairEquipped(true);
                    var agentRepair = (AgentRepair*)AgentModule.Instance()->GetAgentByInternalId(AgentId.Repair);
                    agentRepair->UIModuleInterface->GetRaptureAtkModule()->CloseAddon(agentRepair->AddonId);
                }
                await WaitWhileAsync(() => Player.IsBusy, "Wait for Player not busy", token);
            }

            await MoveToStationaryObject(PositionDryskthota, BaseIdDryskthota, token: token);
            
            await InteractWithByBaseId(1005421, token);
            await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringBoardOCShip), "Waiting for boarding SelectString", token);
            if (QuestManager.IsQuestComplete(68089))
            {
                await WaitUntilAsync(() => TrySelectEntryNumber(0), "Waiting for route SelectString", token);
            }

            await WaitUntilAsync(() => RegexYesNo(true, Lang.SelectYesNoEmbark), "Waiting for SelectYesNo Embark", token);
            await WaitUntilAsync(() => Svc.Condition[ConditionFlag.WaitingForDutyFinder], "Waiting for accepting duty finder", token);
            await WaitUntilAsync(() => TryConfirmContentsFinder(), "Waiting for contentsFinder Confirm", token);

            await WaitUntilAsync(() => dutyStarted, "Waiting for duty to start", token);
            await WaitUntilAsync(() => GetStatus == InstanceContentOceanFishing.OceanFishingStatus.Fishing, "Waiting for voyage to begin", token);
            await Task.Delay(2 * GeneralDelayMs, token);

            await WalkToRailing(token);
            await Task.Delay(4 * GeneralDelayMs, token);

            while (dutyStarted)
            {
                if (Player.Available && Player.Territory is 900 or 1163)
                {
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

                    if(EventUtils.OceanFishingTimeLeft > 32)
                    {
                        AutoHook.SetPreset(IsSpectralActive
                                                   ? PresetSpectralName
                                                   : PresetNormalName);

                        if (C.UseOnlyVersatile)
                        {
                            ChangeBait(29717);
                        }
                        else
                        {
                            ChangeBait((int)GetCurrentBait);
                        }

                        unsafe
                        {
                            if(!Svc.Condition[ConditionFlag.Fishing])
                                ActionManager.Instance()->UseAction(ActionType.Action, 289);
                        }

                        await WaitUntilAsync(() => !Svc.Condition[ConditionFlag.Fishing], "Waiting for reel in", token);
                    }
                }

                await Task.Delay(GeneralDelayMs, token);
            }

            await Task.Delay(GeneralDelayMs * 4, token);

            await WaitUntilAsync(() => CloseIKDResult(), "Waiting for Ocean Fishing results", token);

            await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);

            if (C.OCFishingHandleAR && SubscriptionManager.IsInitialized(IPCNames.AutoRetainer))
            {
                if(C.DiscardAfterVoyage)
                {
                    Chat.ExecuteCommand("/ays discard");
                    await WaitWhileAsync(IPC.AutoRetainer.IsBusy, "Wait until discard finished", token);
                }
                IPC.AutoRetainer.ARAPI.FinishCharacterPostProcess();
                IPC.AutoRetainer.SetMultiModeEnabled(true);
            }
        }
    }

    internal async Task WalkToRailing(CancellationToken token = default)
    {
#if PRIVATE
        var positionalData = Private.ReleaseUtils.GetRandomFishingPositionWithRotation();
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
    private static readonly Random  Rng = new();
    internal                Vector3 GetFishingPosition => new(Rng.Next(2) == 0 ? 7 : -7, 6.711f, Rng.NextSingle() * -10);

    public void OnCharacterPostProcessStep()
    {
        if (askARforAccess)
        {
            IPC.AutoRetainer.ARAPI.RequestCharacterPostprocess();
            askARforAccess = false;
        }
    }

    public void OnCharacterReadyToPostProcess() 
    {
        PluginLog.Verbose("In PostProcess");
        IPC.AutoRetainer.SetMultiModeEnabled(false);
        IPC.AutoRetainer.SetSuppressed(true);
        IPC.AutoRetainer.AbortAllTasks();
        IPC.AutoRetainer.SetSuppressed(false);
        PluginLog.Verbose("AutoRetainer MultiMode disabled.");
        inPostProcess = true;
    }

    internal void SubscribeEvents()
    {
        if (eventsSubscribed) return;
        PluginLog.Log("Subscribe");
        IPC.AutoRetainer.ARAPI.OnCharacterPostprocessStep    += OnCharacterPostProcessStep;
        IPC.AutoRetainer.ARAPI.OnCharacterReadyToPostProcess += OnCharacterReadyToPostProcess;
        Svc.DutyState.DutyStarted                            += DutyStarted;
        Svc.DutyState.DutyCompleted                          += DutyCompleted;
        eventsSubscribed                                     =  true;
    }

    internal void UnsubscribeEvents()
    {
        if (!eventsSubscribed) return;
        PluginLog.Log("Unsubscribe");
        IPC.AutoRetainer.OnCharacterPostprocessStep    -= OnCharacterPostProcessStep;
        IPC.AutoRetainer.OnCharacterReadyToPostProcess -= OnCharacterReadyToPostProcess;
        Svc.DutyState.DutyStarted                      -= DutyStarted;
        Svc.DutyState.DutyCompleted                    -= DutyCompleted;
        AutoHook.DeleteAllAnonymousPresets();
        eventsSubscribed                           =  false;
    }

    private void DutyStarted(object? sender, ushort e) => dutyStarted = true;
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
        foreach (var cid in cids)
        {
            characters.Add(IPC.AutoRetainer.GetOfflineCharacterData(cid));
        }

        characters = characters.OrderBy(x => x.ClassJobLevelArray[17]).ToList();
        return characters;
    }

    internal bool IsRegistrationOpen => DateTime.UtcNow.Hour % 2 == 0 && DateTime.UtcNow.Minute < 13;
    private unsafe bool IsInTitleScreen    => TryGetAddonByName<AtkUnitBase>("_Title", out var addon) && addon->IsVisible;
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
}
