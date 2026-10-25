using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Henchman.Data;
using Lumina.Excel.Sheets;
using Underlings.GameHelpers;
using Underlings.TaskManager;
using AutoDuty = Underlings.IPC.AutoDuty;
using Module = Underlings.Modules.Module;
using Treasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;
using static Underlings.Helpers.ClientStructs.SaddlebagHelper;

namespace Henchman.Features.AlexAndrite;

internal class AlexAndrite : Module
{
    internal const uint MapItemId = 7884;
    internal const uint DecipheredMapItemId = 2001223;
    internal const uint PoeticsItemId = 28;
    internal const int MapCost = 75;
    private static Configuration? Configuration => GetFeatureConfig<AlexAndriteUI, Configuration>();
    private static readonly Vector3 MapNpcPosition = new(62.86072f, 31.15822f, -736.90344f);

    public override void RunTask()
    {
        if (!IsCombat(Player.ClassJob.RowId))
        {
            Chat.Warning("Equip a combat class or job before starting Alex Andrite.");
            return;
        }

        if (!AutoRotation.CheckForAvailability(C.AutoRotationPlugin)) return;
        if (Configuration?.FarmPoetics == true && Configuration.MapsToFarm <= 0)
        {
            Chat.Warning("Set a positive map target before enabling Poetics farming.");
            return;
        }
        if (Configuration?.FarmPoetics == true && !SubscriptionManager.IsLoaded(IPCNames.AutoDuty))
        {
            Chat.Warning("AutoDuty is required to farm Poetics.");
            return;
        }
        TryStartTask(new TaskRecord(Start, "Alex Andrite", onDone: CleanupCombatAutomation, onAbort: CleanupCombatAutomation));
    }

    internal async Task Start(CancellationToken token = default)
    {
        var config = Configuration;
        var useSaddlebag = config?.UseSaddlebag ?? false;
        var mapsToFarm = Math.Max(0, config?.MapsToFarm ?? 0);
        var farmPoetics = config?.FarmPoetics ?? false;
        var selectedDuty = config?.SelectedDuty ?? PoeticsDuty.CastrumMeridianum;
        ErrorThrowIf(farmPoetics && mapsToFarm == 0, "Set a positive map target before farming Poetics.");
        if (useSaddlebag && !CanUseSaddlebag())
        {
            Chat.Warning("Saddlebag unavailable; continuing with inventory maps only.");
            useSaddlebag = false;
        }
        if (useSaddlebag) await OpenSaddlebag(token);
        var decipheredAtStart = GetDecipheredMapCount();
        ErrorThrowIf(decipheredAtStart < 0, "Key items are not loaded; cannot check for a deciphered Alexandrite Map.");
        var saddlebagMapsAtStart = useSaddlebag ? GetItemAmountInSaddlebag(MapItemId) : 0;
        ErrorThrowIf(saddlebagMapsAtStart < 0, "Saddlebag contents are not loaded; cannot check for held maps.");
        if (useSaddlebag) await CloseSaddlebag(token);
        var heldMapsToFinish = decipheredAtStart + InventoryHelper.GetInventoryItemCount(MapItemId) +
                               saddlebagMapsAtStart;
        var completed = 0;
        while (mapsToFarm == 0 || completed < mapsToFarm)
        {
            var decipheredMaps = GetDecipheredMapCount();
            ErrorThrowIf(decipheredMaps < 0, "Key items are not loaded; cannot check for a deciphered Alexandrite Map.");
            if (decipheredMaps == 0)
            {
                ErrorThrowIf(GetTreasureRank() != 0, "Finish the currently deciphered treasure map before farming Alexandrite Maps.");
                var retrievedFromSaddlebag = false;
                if (InventoryHelper.GetInventoryItemCount(MapItemId) == 0)
                {
                    if (useSaddlebag && await RetrieveFromSaddlebag(token)) retrievedFromSaddlebag = true;
                    else if (completed < heldMapsToFinish)
                        ErrorThrow("A map held at startup is no longer available.");
                    else if (GetPoetics() >= MapCost) await ObtainMap(token);
                    else if (farmPoetics)
                    {
                        await EarnPoetics(selectedDuty, token);
                        continue;
                    }
                    else break;
                }

                if (completed >= heldMapsToFinish && useSaddlebag && CanUseSaddlebag() && !retrievedFromSaddlebag &&
                    !HasMapInSaddlebag() && GetPoetics() >= MapCost &&
                    (mapsToFarm == 0 || completed + 1 < mapsToFarm))
                {
                    await WaitWhileAsync(() => Player.IsBusy, "Wait to store map", token, TimeSpan.FromSeconds(10));
                    await OpenSaddlebag(token);
                    if (MoveMapToSaddlebag() is { } moveContextId)
                    {
                        await WaitWhileAsync(() => IsMapMovePending(moveContextId), "Confirm stored map", token, TimeSpan.FromSeconds(10));
                        await WaitUntilAsync(() => InventoryHelper.GetInventoryItemCount(MapItemId) == 0 && HasMapInSaddlebag(),
                                             "Store map", token, TimeSpan.FromSeconds(10));
                        await ObtainMap(token);
                    }
                }

                ErrorThrowIf(InventoryHelper.GetInventoryItemCount(MapItemId) == 0, "No map in inventory to decipher.");
                await CloseSaddlebag(token);
                await Underlings.TaskManager.MovementTasks.Dismount(token);
                await WaitWhileAsync(() => Player.IsBusy, "Wait to decipher", token, TimeSpan.FromSeconds(10));
                await DecipherMap(token);
            }

            ErrorThrowIf(GetDecipheredMapCount() != 1 || GetTreasureRank() == 0,
                         "Expected one deciphered Alexandrite Map with a valid location.");
            if (completed >= heldMapsToFinish && useSaddlebag && HasMapInSaddlebag() &&
                InventoryHelper.GetInventoryItemCount(MapItemId) == 0 &&
                GetPoetics() >= MapCost && (mapsToFarm == 0 || completed + 2 < mapsToFarm))
                await ObtainMap(token);

            await GoToTreasure(token);
            await WaitUntilAsync(() => GetTreasureRank() == 0 && GetDecipheredMapCount() == 0,
                                 "Finish treasure", token, TimeSpan.FromSeconds(15));
            await WaitWhileAsync(() => Player.IsBusy, "Wait after treasure", token, TimeSpan.FromSeconds(10));
            completed++;
        }

        if (completed == 0) Chat.Warning("Need a Mysterious Map or at least 75 poetics to start Alex Andrite.");
        else
        {
            if (mapsToFarm > 0 && completed < mapsToFarm)
                Chat.Warning($"Completed {completed}/{mapsToFarm} maps; no map or Poetics remain.");
            await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
        }
    }

    private static async Task EarnPoetics(PoeticsDuty selectedDuty, CancellationToken token)
    {
        var duty = selectedDuty switch
        {
            PoeticsDuty.CastrumMeridianum => 1043u,
            PoeticsDuty.Praetorium => 1044u,
            PoeticsDuty.PortaDecumana => 1048u,
            _ => throw new ArgumentOutOfRangeException(nameof(selectedDuty))
        };
        ErrorThrowIf(!SubscriptionManager.IsLoaded(IPCNames.AutoDuty), "AutoDuty is not loaded.");
        ErrorThrowIf(!IsDutyUnlocked(duty), "The selected Poetics duty is not unlocked.");
        ErrorThrowIf(!AutoDuty.ContentHasPath.Invoke(duty), "AutoDuty has no path for the selected Poetics duty.");
        ErrorThrowIf(!AutoDuty.IsStopped.Invoke(), "AutoDuty is already running.");

        var before = GetPoetics();
        AutoDuty.RunDutySupport(duty);
        await WaitUntilAsync(() => !AutoDuty.IsStopped.Invoke(), "Start Poetics duty", token, TimeSpan.FromSeconds(15));
        await WaitUntilAsync(() => AutoDuty.IsStopped.Invoke(), "Finish Poetics duty", token, TimeSpan.FromMinutes(90));
        await WaitUntilAsync(() => Player.TerritoryId != duty && Player.Available && !Player.IsBusy,
                             "Leave Poetics duty", token, TimeSpan.FromSeconds(30));
        await WaitUntilAsync(() => GetPoetics() > before, "Receive duty Poetics", token, TimeSpan.FromSeconds(15));
    }

    internal static unsafe uint GetTreasureRank() => EventItemManager.Instance()->GetTreasureHuntRank();

    internal static unsafe int GetDecipheredMapCount()
    {
        var keyItems = InventoryManager.Instance()->GetInventoryContainer(InventoryType.KeyItems);
        if (keyItems == null || !keyItems->IsLoaded) return -1;
        var count = 0;
        for (var i = 0; i < keyItems->Size; i++)
            if (keyItems->GetInventorySlot(i)->ItemId == DecipheredMapItemId) count++;
        return count;
    }

    internal static unsafe uint GetPoetics() => InventoryManager.Instance()->GetTomestoneCount(PoeticsItemId);

    private static bool HasMapInSaddlebag()
    {
        var count = GetItemAmountInSaddlebag(MapItemId);
        ErrorThrowIf(count < 0, "Saddlebag contents are not loaded; cannot check for maps.");
        return count > 0;
    }

    private static unsafe uint? MoveMapFromSaddlebag()
    {
        if (!IsSaddlebagOpen()) return null;
        var manager = InventoryManager.Instance();
        foreach (var type in SaddlebagTypes)
        {
            var bag = manager->GetInventoryContainer(type);
            if (bag == null || !bag->IsLoaded) continue;
            for (ushort slot = 0; slot < bag->Size; slot++)
            {
                if (bag->GetInventorySlot(slot)->ItemId != MapItemId) continue;
                foreach (var destination in new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 })
                {
                    var inventory = manager->GetInventoryContainer(destination);
                    if (inventory == null || !inventory->IsLoaded) continue;
                    for (ushort freeSlot = 0; freeSlot < inventory->Size; freeSlot++)
                        if (inventory->GetInventorySlot(freeSlot)->ItemId == 0)
                        {
                            var contextId = manager->NextContextId;
                            return manager->MoveItemSlot(type, slot, destination, freeSlot, true) == 0 &&
                                   manager->NextContextId != contextId ? contextId : null;
                        }
                }

                ErrorThrow("Inventory is full; cannot retrieve the map from the saddlebag.");
            }
        }

        return null;
    }

    private static unsafe uint? MoveMapToSaddlebag()
    {
        if (!IsSaddlebagOpen()) return null;
        var manager = InventoryManager.Instance();
        foreach (var source in new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 })
        {
            var inventory = manager->GetInventoryContainer(source);
            if (inventory == null || !inventory->IsLoaded) continue;
            for (ushort slot = 0; slot < inventory->Size; slot++)
            {
                if (inventory->GetInventorySlot(slot)->ItemId != MapItemId) continue;
                foreach (var destination in SaddlebagTypes)
                {
                    var bag = manager->GetInventoryContainer(destination);
                    if (bag == null || !bag->IsLoaded) continue;
                    for (ushort freeSlot = 0; freeSlot < bag->Size; freeSlot++)
                        if (bag->GetInventorySlot(freeSlot)->ItemId == 0)
                        {
                            var contextId = manager->NextContextId;
                            return manager->MoveItemSlot(source, slot, destination, freeSlot, true) == 0 &&
                                   manager->NextContextId != contextId ? contextId : null;
                        }
                }

                return null;
            }
        }

        return null;
    }

    private static unsafe bool IsMapMovePending(uint contextId)
    {
        var operations = (InventoryManager.InventoryOperation*)InventoryManager.Instance();
        for (var i = 0; i < 128; i++)
            if (!operations[i].IsEmpty && operations[i].ContextId == contextId) return true;
        return false;
    }

    private static async Task<bool> RetrieveFromSaddlebag(CancellationToken token)
    {
        if (!CanUseSaddlebag()) return false;
        await OpenSaddlebag(token);
        if (!HasMapInSaddlebag())
        {
            await CloseSaddlebag(token);
            return false;
        }
        var moveContextId = MoveMapFromSaddlebag();
        ErrorThrowIf(moveContextId is null, "Could not move the map from the saddlebag.");
        await WaitWhileAsync(() => IsMapMovePending(moveContextId.GetValueOrDefault()), "Confirm retrieved map", token, TimeSpan.FromSeconds(10));
        await WaitUntilAsync(() => InventoryHelper.GetInventoryItemCount(MapItemId) > 0, "Retrieve map", token, TimeSpan.FromSeconds(10));
        return true;
    }

    private static async Task ObtainMap(CancellationToken token)
    {
        ErrorThrowIf(GetPoetics() < MapCost, "Need at least 75 poetics to buy a Mysterious Map.");
        var poetics = GetPoetics();
        if (Player.TerritoryId != 156)
        {
            var aetheryte = GetAetheryte(156, MapNpcPosition);
            ErrorThrowIf(aetheryte == 0 || !IsAetheryteUnlocked(aetheryte), "No unlocked aetheryte for the map NPC.");
            await TeleportTo(aetheryte, token);
        }

        await MoveToStationaryObject(MapNpcPosition, 1008119, true, token: token);
        await Dismount(token);
        ErrorThrowIf(GetPoetics() < MapCost, "Need at least 75 poetics to buy a Mysterious Map.");
        await InteractWithByBaseId(1008119, token);
        await WaitUntilAsync(SelectMapOption, "Select map option", token, TimeSpan.FromSeconds(10));
        await WaitUntilAsync(() => GenericYesNo(true), "Confirm map", token, TimeSpan.FromSeconds(10));
        await WaitUntilAsync(() => InventoryHelper.GetInventoryItemCount(MapItemId) > 0, "Receive map", token, TimeSpan.FromSeconds(10));
        await WaitUntilAsync(() => GetPoetics() < poetics, "Pay for map", token, TimeSpan.FromSeconds(10));
    }

    private static unsafe bool SelectMapOption()
    {
        var selector = EventHandlerSelector.Instance();
        if (selector == null || selector->Target == null || selector->Target->BaseId != 1008119) return false;
        for (var i = 0; i < selector->OptionsCount; i++)
        {
            if (selector->Options[i].Handler != null && selector->Options[i].Handler->Info.EventId.Id == 721068)
            {
                EventFramework.Instance()->InteractWithHandlerFromSelector(i);
                return true;
            }
        }

        return false;
    }

    private static async Task DecipherMap(CancellationToken token)
    {
        await WaitWhileAsync(() => Svc.Condition[ConditionFlag.BoundByDuty] || Svc.Condition[ConditionFlag.BoundByDuty56],
                             "Wait for treasure duty to end", token, TimeSpan.FromMinutes(1));
        unsafe
        {
            var manager = InventoryManager.Instance();
            foreach (var type in new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 })
            {
                var inventory = manager->GetInventoryContainer(type);
                for (var i = 0; i < inventory->Size; i++)
                    if (inventory->GetInventorySlot(i)->ItemId == MapItemId)
                    {
                        var extraParam = ((uint)type << 16) | (uint)i;
                        ErrorThrowIf(!ActionManager.Instance()->UseAction(ActionType.Action, 1694, Player.GameObject->GetGameObjectId(), extraParam),
                                     "Could not use Decipher on the map.");
                        goto Deciphered;
                    }
            }

            ErrorThrow("Map disappeared before deciphering.");
        }

        Deciphered:
        await WaitUntilAsync(() => GetTreasureRank() > 0 && GetDecipheredMapCount() > 0,
                             "Decipher map", token, TimeSpan.FromSeconds(10));
    }

    internal static async Task GoToTreasure(CancellationToken token = default)
    {
        uint rank;
        ushort spot;
        unsafe
        {
            rank = EventItemManager.Instance()->GetTreasureHuntRank();
            spot = EventItemManager.Instance()->GetTreasureSpotSubKey();
        }

        ErrorThrowIf(rank == 0, "No deciphered map is active.");
        var location = Svc.Data.GetSubrowExcelSheet<TreasureSpot>().GetSubrow(rank, spot).Location.Value;
        var position = new Vector3(location.X, location.Y, location.Z);
        if (Player.TerritoryId != location.Territory.RowId)
        {
            var aetheryte = GetAetheryte(location.Territory.RowId, position);
            ErrorThrowIf(aetheryte == 0 || !IsAetheryteUnlocked(aetheryte), "No unlocked aetheryte for the treasure.");
            await TeleportTo(aetheryte, token);
        }

        await MoveTo(position, true, token);
        await Dismount(token);
        unsafe { ErrorThrowIf(!ActionManager.Instance()->UseAction(ActionType.Action, 1695), "Could not use Dig."); }
        await WaitUntilAsync(HasTreasure, "Wait for treasure", token, TimeSpan.FromSeconds(15));

        uint entityId;
        uint playerEntityId;
        Vector3 treasurePosition;
        unsafe
        {
            var treasure = EventObjectManager.Instance()->EventObjects.ToArray()
                .First(x => x.Value != null && x.Value->ObjectKind == ObjectKind.Treasure && x.Value->GetIsTargetable()).Cast<Treasure>();
            entityId = treasure->EntityId;
            playerEntityId = Player.GameObject->EntityId;
            treasurePosition = treasure->Position;
        }

        ErrorThrowIf(playerEntityId == 0, "Cannot identify the player-owned treasure guardians.");
        ErrorThrowIf(!AutoRotation.CheckForAvailability(C.AutoRotationPlugin), "A combat rotation plugin is required for the treasure guardians.");
        await ApproachTreasure(entityId, treasurePosition, token);
        await InteractWithByEntityId(entityId, token);
        await WaitUntilAsync(() => RegexYesNo(true, Lang.SelectYesNoOpenTreasure),
                             "Confirm treasure coffer", token, TimeSpan.FromSeconds(10));
        Svc.Targets.Target = null;
        using (CombatAutomation.Acquire(C.AutoRotationPlugin))
        {
            var combatDeadline = DateTime.UtcNow.AddMinutes(10);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                ErrorThrowIf(DateTime.UtcNow >= combatDeadline, "Timed out waiting for the treasure guardians to finish.");
                ErrorThrowIf(Svc.Condition[ConditionFlag.Unconscious], "Player died fighting the treasure guardians.");
                if (GetTreasureMob(playerEntityId, treasurePosition) is { } mob)
                {
                    var mobEntityId = mob.EntityId;
                    Svc.Targets.Target = mob;
                    ErrorThrowIf(!await MoveToMovingObject(mob, recheckPosition: true, token: token),
                                 "Could not reach a treasure guardian.");
                    if (!mob.IsValid() || mob.EntityId != mobEntityId || mob.IsDead) continue;
                    await WaitUntilAsync(() => !mob.IsValid() || mob.EntityId != mobEntityId || mob.IsDead ||
                                               Svc.Condition[ConditionFlag.Unconscious],
                                         "Defeat treasure guardian", token, TimeSpan.FromMinutes(3));
                }
                else if (IsTreasureReadyToOpen() && IsTreasureTargetable(entityId) && !Svc.Condition[ConditionFlag.InCombat])
                {
                    await ApproachTreasure(entityId, treasurePosition, token);
                    if (GetTreasureMob(playerEntityId, treasurePosition) == null && IsTreasureReadyToOpen() &&
                        IsTreasureTargetable(entityId) &&
                        !Svc.Condition[ConditionFlag.InCombat])
                    {
                        await InteractWithByEntityId(entityId, token);
                        await WaitUntilAsync(() => IsTreasureOpenedOrGone(entityId), "Open treasure", token,
                                             TimeSpan.FromSeconds(15));
                        break;
                    }
                }
                else
                    await Task.Delay(GeneralDelayMs, token);
            }
        }
    }

    private static unsafe IBattleNpc? GetTreasureMob(uint playerEntityId, Vector3 treasurePosition) => Svc.Objects.OfType<IBattleNpc>()
        .FirstOrDefault(x => x.BattleNpcKind == Dalamud.Game.ClientState.Objects.Enums.BattleNpcSubKind.Combatant && x.IsTargetable && !x.IsDead &&
                             x.Struct()->OwnerId == playerEntityId && Vector3.Distance(x.Position, treasurePosition) < 50f);

    private static async Task ApproachTreasure(uint entityId, Vector3 treasurePosition, CancellationToken token)
    {
        if (Player.DistanceTo(treasurePosition) < 5f) return;
        var chest = Svc.Objects.SearchByEntityId(entityId);
        ErrorThrowIf(chest == null, "Treasure chest disappeared before interaction.");
        ErrorThrowIf(!await MoveToMovingObject(chest!, distance: 4f, token: token), "Could not reach treasure chest.");
        Underlings.IPC.Vnavmesh.StopCompletely();
    }

    private static unsafe bool IsTreasureTargetable(uint entityId) => EventObjectManager.Instance()->EventObjects.ToArray()
        .Any(x => x.Value != null && x.Value->EntityId == entityId && x.Value->GetIsTargetable());

    private static unsafe bool IsTreasureReadyToOpen() => EventFramework.Instance()->DirectorModule.DirectorList.ToArray()
        .Any(x => x.Value != null && x.Value->EventHandlerInfo != null &&
                  x.Value->EventHandlerInfo->EventId.ContentId == EventHandlerContent.TreasureHuntDirector &&
                  x.Value->Sequence is 7 or 8);

    private static unsafe bool IsTreasureOpenedOrGone(uint entityId)
    {
        foreach (var obj in EventObjectManager.Instance()->EventObjects.ToArray())
            if (obj.Value != null && obj.Value->EntityId == entityId)
                return obj.Cast<Treasure>()->State != Treasure.TreasureState.Unopened;
        return true;
    }

    private static unsafe bool HasTreasure() => EventObjectManager.Instance()->EventObjects.ToArray()
        .Any(x => x.Value != null && x.Value->ObjectKind == ObjectKind.Treasure && x.Value->GetIsTargetable());
}
