using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Features.BringYourXGame;
using Henchman.Helpers;
using Henchman.Multibox.Command;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;

namespace Henchman.Tasks;

internal static class MovementTasks
{
    internal static async Task<bool> Mount(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (Player.Mounted) return true;
        using var scope = new TaskDescriptionScope("Mounting");
        await WaitUntilAsync(() => !Player.IsBusy, "Waiting for Player Status not busy!", token);
        if (!Player.CanMount)
        {
            Verbose("Can not mount in current territory.");
            return false;
        }

        Verbose("Using Mount");
        await CheckIfPlayerIsStunned(token);
        unsafe
        {
            var actionManager = ActionManager.Instance();
            if (C.UseMountRoulette || !PlayerState.Instance()->IsMountUnlocked(C.MountId))
            {
                if (actionManager->GetActionStatus(ActionType.GeneralAction, 9) != 0) return false;
                actionManager->UseAction(ActionType.GeneralAction, 9);
            }
            else
            {
                if (actionManager->GetActionStatus(ActionType.Mount, C.MountId) != 0) return false;
                actionManager->UseAction(ActionType.Mount, C.MountId);
            }
        }

        await WaitUntilAsync(() => Player.Mounted, "Waiting for Mount", token);
        return true;
    }

    internal static async Task Dismount(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope("Dismounting");

        if (!Player.Mounted) return;

        Verbose("Dismounting");
        do
        {
            token.ThrowIfCancellationRequested();
            if (!IsActionUsable(ActionType.GeneralAction, 23)) continue;
            UseAction(ActionType.GeneralAction, 23);
            await Task.Delay(GeneralDelayMs, token);
        }
        while (Svc.Condition[ConditionFlag.InFlight] || Player.Mounted);
    }

    internal static async Task MoveToStationaryObject(
            Vector3           interactablePosition,
            uint              baseId,
            bool              stopAtDistance = false,
            float             distance       = 5f,
            CancellationToken token          = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope($"Move To Object {baseId}");
        if (Player.DistanceTo(interactablePosition) <= distance) return;
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !IsPlayerBusy, "Wait for navmesh", token);
        if (Player.DistanceTo(interactablePosition) >= 30f) await Mount(token);
        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(interactablePosition, Player.Mounted && Player.CanFly), $"Could not find path to {interactablePosition}");
        if (!Player.Mounted && Player.DistanceTo(interactablePosition) > C.MinRunDistance) UseSprint();
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);
        if (stopAtDistance)
        {
            await WaitUntilAsync(() => IsPlayerInObjectRange(baseId, distance), "Check for distance", token);
            Vnavmesh.StopCompletely();
        }
        else
        {
            await WaitUntilAsync(() => !Vnavmesh.PathIsRunning()         &&
                                       !Vnavmesh.NavPathfindInProgress() &&
                                       IsPlayerInObjectRange(baseId, distance)
                                              .Result, "Wait for walking to Object", token);
        }
    }

    internal static async Task MoveToMovingObject(
            IGameObject       gameObject,
            float             distance        = 3f,
            bool              recheckPosition = false,
            CancellationToken token           = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope("Move To moving Object");
        if (Player.DistanceTo(gameObject.Position) < distance) return;
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !IsPlayerBusy, "Wait for navmesh", token);
        var position = gameObject.Position;

        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(position, Player.DistanceTo(position) > 25 && Player.Mounted && Player.CanFly), $"Could not find path to {gameObject.Position}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);

        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        if (recheckPosition)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (gameObject.IsInAttackRange(gameObject.HitboxRadius))
                {
                    Vnavmesh.StopCompletely();
                    break;
                }

                if (Vector3.Distance(position, gameObject.Position) > gameObject.HitboxRadius)
                {
                    ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(gameObject.Position, Player.Mounted && Player.CanFly), $"Could not find path to {gameObject.Position}");
                    await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);
                    position = gameObject.Position;
                }

                await Task.Delay(GeneralDelayMs, token);
            }
        }
        else
            await WaitUntilAsync(() => IsPlayerInPositionRange2D(new Vector2(position.X, position.Z), distance), "Check for distance", token);
    }

    internal static async Task MoveTo(Vector3 position, bool mount = false, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope($"Move To {position}");
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !IsPlayerBusy, "Wait for navmesh", token);
        if (Player.DistanceTo(position) < 1) return;
        if (mount) await Mount(token);
        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(position, Player.Mounted && Player.CanFly), $"Could not find path to {position}");
        await WaitPulseConditionAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to finish", token);
    }

    internal static async Task MoveCloseToPlayer(Vector3 position, ulong CID, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope($"Move To {position}");
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !IsPlayerBusy, "Wait for navmesh", token);
        await Task.Delay(2 * GeneralDelayMs, token);
        if (Player.DistanceTo(position) < 2) return;
        if (Player.DistanceTo(position) > C.MinMountDistance) await Mount(token);
        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(position, Player.Mounted && Player.CanFly), $"Could not find path to {position}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to finish", token);
        await WaitUntilAsync(() =>
                             {
                                 unsafe
                                 {
                                     if (Svc.Objects.OfType<IPlayerCharacter>()
                                            .TryGetFirst(x => x.Struct()->ContentId == CID, out var boss))
                                     {
                                         if (Player.DistanceTo(boss.Position) < 2)
                                         {
                                             Vnavmesh.StopCompletely();
                                             return true;
                                         }
                                     }
                                 }

                                 return false;
                             }, "Wait until close to player", token);
    }

    internal static async Task MoveToArea(Vector3 position, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope($"Move To Area {position}");
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !IsPlayerBusy, "Wait for navmesh", token);

        if (Player.DistanceTo(position) < 5) return;
        if (C.UseMount      && Player.DistanceTo(position) > C.MinMountDistance) await Mount(token);
        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(position, Player.Mounted && Player.CanFly), $"Could not find path to {position}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);

        // "Stuck check" jump. Mostly if it get stuck in water.
        var lastPlayerPosition = Player.Position;
        while (true)
        {
            await Task.Delay(GeneralDelayMs * 4, token);
            token.ThrowIfCancellationRequested();
            ErrorIf(!Vnavmesh.PathIsRunning(), "Pathing was randomly stopped");
            if (Player.DistanceTo(position) < 50) break;
            if (Player.DistanceTo(lastPlayerPosition) < 1f)
            {
                unsafe
                {
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                }
            }

            lastPlayerPosition = Player.Position;
        }
    }

    internal static async Task TeleportTo(uint territoryId, uint aetheryteTerritoryId, uint aetheryteId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (Player.Territory.RowId != aetheryteTerritoryId && Player.Territory.RowId != territoryId)
        {
            using var scope = new TaskDescriptionScope($"Teleport to {territoryId}");
            await CheckIfPlayerIsStunned(token);
            ErrorThrowIf(!Lifestream.Teleport(aetheryteId, 0), $"Teleport to {aetheryteId} failed.");
            await WaitUntilAsync(() => Player.Territory.RowId == aetheryteTerritoryId || Player.Territory.RowId == territoryId, "Check for right territory", token);
        }
    }

    internal static async Task TeleportTo(uint aetheryteId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var territory = Svc.Data.GetExcelSheet<Aetheryte>()
                           .GetRow(aetheryteId)
                           .Territory.Value;

        if (Player.Territory.RowId != territory.RowId)
        {
            using var scope = new TaskDescriptionScope($"Teleport to aetheryte {aetheryteId}");
            await CheckIfPlayerIsStunned(token);
            await WaitWhileAsync(() => Player.IsBusy, "Wait while Player is busy", token);
            while (!Svc.Condition[ConditionFlag.Casting])
            {
                ErrorThrowIf(!Lifestream.Teleport(aetheryteId, 0), $"Teleport to Aetheryte {aetheryteId} in {territory.PlaceName.Value.Name.ExtractText()} ({territory.RowId}) failed.");
                await Task.Delay(GeneralDelayMs * 8, token);
            }

            await WaitUntilAsync(() => Player.Territory.RowId == territory.RowId, $"Check for right territory {territory.RowId}", token);
            await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !Player.IsBusy, "Wait for Transition", token);
        }
    }

    internal static async Task MoveToNextZone(Vector3 zoneTransitionPosition, uint nextTerritoryId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !Player.IsBusy, "Wait for VNav/Player", token);
        using var scope = new TaskDescriptionScope($"Move to Zone {nextTerritoryId}");
        await Mount(token);
        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(zoneTransitionPosition, Player.Mounted && Player.CanFly),
                     $"Could not find path to {zoneTransitionPosition}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);

        if (!Player.Mounted) UseSprint();
        await WaitUntilAsync(() => Player.Territory.RowId == nextTerritoryId, "Check for right territory", token);
    }

    internal static async Task<bool> RoamUntilTargetNearby(List<Vector3> pointList, uint targetNameId, bool gotKilledWhileDetour, bool detourForARanks, float distanceToSpot = 60f, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope        = new TaskDescriptionScope("Roam until Target nearby");
        var       path         = Utils.SortListByDistance(pointList);
        uint      killedARanks = 0;
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !Player.IsBusy, "Wait for VNav/Player", token);
        foreach (var point in path)
        {
            if (C.UseMount && Player.DistanceTo(point) > C.MinMountDistance)
                await Mount(token);

            if (!Player.Mounted && Player.DistanceTo(point) > C.MinRunDistance) UseSprint();

#if DEBUG || PRIVATE
            ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(point, Player.Mounted && Player.CanFly), $"Could not find path from {Player.Position} to {point}");
#else
            if (WarningIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(point, Player.Mounted && Player.CanFly),
                          $"Could not find path from {Player.Position} to {point}. Skipping to next!"))
                continue;
#endif
            await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);

            if (detourForARanks && !gotKilledWhileDetour)
            {
                using var multiTaskToken = new CancellationTokenSource();
                using var linkedCts      = CancellationTokenSource.CreateLinkedTokenSource(token, multiTaskToken.Token);

                var playerCloseToPoint = WaitUntilAsync(() => Player.DistanceTo(point.ToVector2()) < distanceToSpot, $"Moving close to next roaming point: {point}.", linkedCts.Token);
                var scanningForARanks = WaitUntilAsync(() => Svc.Objects.OfType<IBattleNpc>()
                                                                .FirstOrDefault(x => Svc.Data.GetExcelSheet<NotoriousMonster>()
                                                                                        .Any(y => y.BNpcBase.RowId == x.BaseId && y.Rank == 2)) is { Level: <= 70, IsDead: false } detourTarget, "Scanning for A-Ranks", linkedCts.Token);

                var completedTask = await Task.WhenAny(scanningForARanks, playerCloseToPoint);
                linkedCts.Cancel();
                await completedTask;

                Vnavmesh.StopCompletely();

                if (completedTask == scanningForARanks)
                {
                    if (Svc.Objects.OfType<IBattleNpc>()
                           .FirstOrDefault(x => Svc.Data.GetExcelSheet<NotoriousMonster>()
                                                   .Any(y => y.BNpcBase.RowId == x.BaseId && y.Rank == 2)) is { Level: <= 70, IsDead: false } detourTarget)
                    {
                        AutoRotation.Enable();
                        Bossmod.EnableAI();

                        if (!await KillTarget(detourTarget, token))
                        {
                            AutoRotation.Disable();
                            Bossmod.DisableAI();
                            return false;
                        }

                        killedARanks++;

                        var isDummyTarget = targetNameId == int.MaxValue;
                        var exVersion = Svc.Data.GetExcelSheet<TerritoryType>()
                                           .GetRow(Player.Territory.RowId)
                                           .ExVersion.RowId;

                        if ((isDummyTarget && exVersion == 0 && killedARanks == 1) ||
                            (isDummyTarget && exVersion > 0  && killedARanks == 2))
                        {
                            AutoRotation.Disable();
                            Bossmod.DisableAI();
                            return true;
                        }

                        AutoRotation.Disable();
                        Bossmod.DisableAI();
                    }
                }
            }
            else
            {
                await WaitUntilAsync(() => Player.DistanceTo(point.ToVector2()) < 70f, $"Moving close to next roaming point: {point}!", token);
                Vnavmesh.StopCompletely();
            }

            if (GetMobNearby(targetNameId) is { } mob)
            {
                if (TaskName == "Bring Your B Game" && C.TrackBRankSpots)
                {
                    if (TryGetFeature<BringYourXGameUI>(out var plugin))
                    {
                        if (!plugin.FoundSpawns.Any(x => x.ToVector2()
                                                          .IsWithinRadius(mob.Position.ToVector2())))
                        {
                            plugin.FoundSpawns.Add(new Vector3(MathF.Round(mob.Position.X, 2), MathF.Round(mob.Position.Y, 2), MathF.Round(mob.Position.Z, 2)));
                            plugin.PossibleSpawnPoints.Remove(point);
                        }
                    }
                }

                return true;
            }
        }

        return true;
    }

    private static async Task CheckIfPlayerIsStunned(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        uint[] paralyzeIds = { 17, 216, 482, 988, 3463, 3963 };
        if (paralyzeIds.Any(x => Player.Status.Any(y => y.StatusId == x)))
        {
            await WaitWhileAsync(() => paralyzeIds.Any(x => Player.Status.Any(y => y.StatusId == x)), "Wait for paralyze status to end", token);
        }
    }
}

[CommandGroup]
internal static class MovementRPCs
{
    [Command]
    internal static async Task<bool> GoToPlayer(uint territoryId, Vector3 position, string world, ulong CID, CancellationToken token = default)
    {
        if (token.IsCancellationRequested) return false;
        using var scope = new TaskDescriptionScope($"RPC: GoTo ({territoryId} | {position})");

        if (Player.CurrentWorldName != world)
        {
            if (Lifestream.ChangeWorld(world))
                await WaitPulseConditionAsync(() => Lifestream.IsBusy(), "Waiting for World change", token);
            else
                return false;
        }

        var aetheryteId = GetAetheryte(territoryId, position);
        if (aetheryteId == 0 || !IsAetheryteUnlocked(aetheryteId)) return false;

        await TeleportTo(aetheryteId, token);
        await MoveCloseToPlayer(position, CID, token);
        return true;
    }
}
