using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Henchman.Features.OnYourBGame;
using Henchman.Helpers;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Henchman.Tasks;

internal static class MovementTasks
{
    internal static async Task MoveToStationaryObject(
            Vector3 interactablePosition,
            uint dataId,
            bool stopAtDistance = false,
            float distance = 5f,
            CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope("Move To Object");
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !IsPlayerBusy, "Wait for navmesh", token);
        ErrorIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(interactablePosition, Player.Mounted && Player.CanFly), $"Could not find path to {interactablePosition}");
        if (!Player.Mounted && Player.DistanceTo(interactablePosition) > C.MinRunDistance) UseSprint();
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);
        if (stopAtDistance)
        {
            await WaitUntilAsync(() => IsPlayerInObjectRange(dataId, distance), "Check for distance", token);
            Vnavmesh.StopCompletely();
        }
        else
            await WaitUntilAsync(() => !Vnavmesh.PathIsRunning() &&
                                       !Vnavmesh.NavPathfindInProgress() &&
                                       IsPlayerInObjectRange(dataId, distance)
                                              .Result, "Wait for walking to Object", token);
    }

    internal static async Task MoveToMovingObject(
            IGameObject gameObject,
            float distance = 3f,
            bool recheckPosition = false,
            CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope("Move To moving Object");
        if (Player.DistanceTo(gameObject.Position) < distance) return;
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !IsPlayerBusy, "Wait for navmesh", token);
        var position = gameObject.Position;
        ErrorIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(position, Player.Mounted && Player.CanFly), $"Could not find path to {gameObject.Position}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);

        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        if (recheckPosition)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (IsPlayerInObjectRange2D(gameObject, gameObject.HitboxRadius))
                {
                    Vnavmesh.StopCompletely();
                    break;
                }

                /*if (!IsInMeleeRange(gameObject.Position, gameObject.HitboxRadius +
                                                                      (C.UseMeleeRange
                                                                               ? 0
                                                                               : 15)))*/
                if (Vector3.Distance(position, gameObject.Position) > gameObject.HitboxRadius)
                {
                    ErrorIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(gameObject.Position, Player.Mounted && Player.CanFly), $"Could not find path to {gameObject.Position}");
                    await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);
                    position = gameObject.Position;
                }

                await Task.Delay(50, token);
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
        if (Player.DistanceTo(position) < 5) return;
        if (mount) await Mount(token);
        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        ErrorIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(position, Player.Mounted && Player.CanFly), $"Could not find path to {position}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);
        await WaitUntilAsync(() => !Vnavmesh.PathIsRunning() && !Vnavmesh.NavPathfindInProgress() && Player.DistanceTo(position) < 1, "Wait for walking to destination", token);
    }

    internal static async Task MoveToArea(Vector3 position, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope($"Move To Area {position}");
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !IsPlayerBusy, "Wait for navmesh", token);

        if (Player.DistanceTo(position) < 5) return;
        if (C.UseMount && Player.DistanceTo(position) > C.MinMountDistance) await Mount(token);
        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        ErrorIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(position, Player.Mounted && Player.CanFly), $"Could not find path to {position}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);

        // "Stuck check" jump. Mostly if it get stuck in water.
        var lastPlayerPosition = Player.Position;
        while (true)
        {
            await Task.Delay(1000, token);
            token.ThrowIfCancellationRequested();
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
        if (Player.Territory != aetheryteTerritoryId && Player.Territory != territoryId)
        {
            using var scope = new TaskDescriptionScope($"Teleport to {territoryId}");
            ErrorIf(!Lifestream.Teleport(aetheryteId, 0), $"Teleport to {aetheryteId} failed.");
            await WaitUntilAsync(() => Player.Territory == aetheryteTerritoryId || Player.Territory == territoryId, "Check for right territory", token);
        }
    }

    internal static async Task TeleportTo(uint aetheryteId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var territory = Svc.Data.GetExcelSheet<Aetheryte>()
                           .GetRow(aetheryteId)
                           .Territory.Value;

        if (Player.Territory != territory.RowId)
        {
            using var scope = new TaskDescriptionScope($"Teleport to aetheryte {aetheryteId}");
            await WaitWhileAsync(() => Player.IsBusy, "Wait while Player is busy", token);
            while (!Svc.Condition[ConditionFlag.Casting])
            {
                ErrorIf(!Lifestream.Teleport(aetheryteId, 0), $"Teleport to Aetheryte {aetheryteId} in {territory.PlaceName.Value.Name.ExtractText()} ({territory.RowId}) failed.");
                await Task.Delay(2000, token);
            }

            await WaitUntilAsync(() => Player.Territory == territory.RowId, $"Check for right territory {territory.RowId}", token);
            await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !Player.IsBusy, "Wait for Transition", token);
        }
    }

    internal static async Task MoveToNextZone(Vector3 zoneTransitionPosition, uint nextTerritoryId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        await WaitUntilAsync(() => Vnavmesh.NavIsReady() && !Player.IsBusy, "Wait for VNav/Player", token);
        using var scope = new TaskDescriptionScope($"Move to Zone {nextTerritoryId}");
        await Mount(token);
        ErrorIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(zoneTransitionPosition, Player.Mounted && Player.CanFly),
                $"Could not find path to {zoneTransitionPosition}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);

        if (!Player.Mounted) UseSprint();
        await WaitUntilAsync(() => Player.Territory == nextTerritoryId, "Check for right territory", token);
    }

    internal static async Task<bool> RoamUntilTargetNearby(List<Vector3> pointList, uint targetId, bool gotKilledWhileDetour, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope("Roam until Target nearby");
        var path = Utils.SortListByDistance(pointList);

        foreach (var point in path)
        {
            if (C.UseMount && Player.DistanceTo(point) > C.MinMountDistance)
                await Mount(token);

            if (!Player.Mounted && Player.DistanceTo(point) > C.MinRunDistance) UseSprint();

#if DEBUG || PRIVATE

            ErrorIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(point, Player.Mounted && Player.CanFly), $"Could not find path from {Player.Position} to {point}");
#else
                if (WarningIf(!Vnavmesh.SimpleMovePathfindAndMoveTo(point, Player.Mounted && Player.CanFly),
                              $"Could not find path from {Player.Position} to {point}. Skipping to next!"))
                {
                    continue;
                }
#endif
            await WaitUntilAsync(() => Vnavmesh.PathIsRunning(), "Wait for pathing to start", token);


            if (C.DetourForOtherAB && !gotKilledWhileDetour)
            {
                using var multiTaskToken = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, multiTaskToken.Token);

                var playerCloseToPoint = WaitUntilAsync(() => Player.DistanceTo(point.ToVector2()) < 60f, $"Moving close to next roaming point: {point}!", linkedCts.Token);
                var scanningForABRanks = WaitUntilAsync(() => Svc.Objects.OfType<IBattleNpc>()
                                                                 .Any(x => Svc.Objects.OfType<IBattleNpc>()
                                                                              .FirstOrDefault(x => Svc.Data.GetExcelSheet<BNpcBase>()
                                                                                                      .TryGetRow(x.DataId, out var row) &&
                                                                                                   row.Rank == 1) is { Level: <= 70 } detourTarget), "Scanning for other A- and B-Ranks", linkedCts.Token);

                var completedTask = await Task.WhenAny(playerCloseToPoint, scanningForABRanks);
                linkedCts.Cancel();
                await completedTask;

                Vnavmesh.StopCompletely();

                if (completedTask == scanningForABRanks)
                {
                    if (Svc.Objects.OfType<IBattleNpc>()
                           .FirstOrDefault(x => Svc.Data.GetExcelSheet<BNpcBase>()
                                                   .TryGetRow(x.DataId, out var row) &&
                                                row.Rank == 1) is { Level: <= 70 } detourTarget)
                    {
                        if (!await KillTarget(detourTarget, token))
                            return false;
                    }
                }
            }
            else
            {
                await WaitUntilAsync(() => Player.DistanceTo(point.ToVector2()) < 70f, $"Moving close to next roaming point: {point}!", token);
                Vnavmesh.StopCompletely();
            }

            if (GetMobNearby(targetId) is { } mob)
            {
                if (TaskName == "On Your B Game" && C.TrackBRankSpots)
                {
                    if (TryGetFeature<OnYourBGameUI>(out var plugin))
                    {
                        if (!plugin.foundSpawns.Any(x => IsWithinRadius(x.ToVector2(), mob.Position.ToVector2())))
                        {
                            plugin.foundSpawns.Add(new Vector3(MathF.Round(mob.Position.X, 2), MathF.Round(mob.Position.Y, 2), MathF.Round(mob.Position.Z, 2)));
                            plugin.possibleSpawnPoints.Remove(point);
                        }
                    }
                }

                return true;
            }
        }

        return true;
    }
}
