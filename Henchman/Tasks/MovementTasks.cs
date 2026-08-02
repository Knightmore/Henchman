using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Features.BringYourXGame;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using Underlings.GameHelpers;
using Underlings.TaskManager;

namespace Henchman.Tasks;

internal static class MovementTasks
{
    private const uint TeleportActionId = 5;

    private static MovementOptions Options => new()
                                              {
                                                      UseMount         = C.UseMount,
                                                      MountId          = C.MountId,
                                                      UseMountRoulette = C.UseMountRoulette,
                                                      MinRunDistance   = C.MinRunDistance,
                                                      MinMountDistance = C.MinMountDistance,
                                                      HandleCombat     = token => HandleHaters(token: token),
                                                      EnableCombatAutomation = () =>
                                                                               {
                                                                                   AutoRotation.Enable(C.AutoRotationPlugin);
                                                                                   Bossmod.EnableAI();
                                                                               },
                                                      DisableCombatAutomation = () =>
                                                                                {
                                                                                    Bossmod.DisableAI();
                                                                                    AutoRotation.Disable(C.AutoRotationPlugin);
                                                                                }
                                              };

    private static async Task<bool> IsBusy(CancellationToken token)
    {
        if (Svc.Condition[ConditionFlag.InCombat] && !HandlingHaters)
        {
            AutoRotation.Enable(C.AutoRotationPlugin);
            Bossmod.EnableAI();
            await HandleHaters(token: token);
            Bossmod.DisableAI();
            AutoRotation.Disable(C.AutoRotationPlugin);
        }

        return Player.IsBusy;
    }

    private static Func<bool> MakePositionStallDetector(TimeSpan window)
    {
        Func<bool>? noProgress     = null;
        var         wasPathfinding = false;

        return () =>
               {
                   if (Vnavmesh.SimpleMovePathfindInProgress.Invoke())
                   {
                       wasPathfinding = true;
                       noProgress     = null;
                       return false;
                   }

                   if (wasPathfinding || noProgress == null)
                   {
                       noProgress     = StallDetectors.NoProgress(() => Player.Position, (a, b) => Vector3.Distance(a, b) < 1f, window);
                       wasPathfinding = false;
                   }

                   return noProgress();
               };
    }

    internal static async Task WaitForPathToSettle(string description = "Wait for path to settle", CancellationToken token = default)
    {
        using var scope = new TaskDescriptionScope(description);

        var stuckDetector = MakePositionStallDetector(TimeSpan.FromSeconds(6));
        var stallCount    = 0;

        while (Vnavmesh.PathIsRunning.Invoke() || Vnavmesh.NavPathfindInProgress.Invoke())
        {
            token.ThrowIfCancellationRequested();

            if (stuckDetector())
            {
                stallCount++;
                TaskLog.Warning($"{description}: stuck, attempt {stallCount}/3");
                ErrorThrowIf(stallCount >= 3, $"{description}: player made no progress after {stallCount} jump-recovery attempts.");

                unsafe
                {
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                }

                var positionAfterJump = Player.Position;
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                if (Vector3.Distance(Player.Position, positionAfterJump) < 1f)
                    Vnavmesh.StopCompletely();

                stuckDetector = MakePositionStallDetector(TimeSpan.FromSeconds(6));
            }

            await Task.Delay(GeneralDelayMs, token);
        }
    }

    internal static async Task<bool> Mount(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (Player.Mounted) return true;
        using var scope = new TaskDescriptionScope("Mounting");
        await WaitWhileAsync(() => IsBusy(token), "Waiting for Player Status not busy!", token);
        if (!Player.CanMount)
        {
            TaskLog.Debug("Can not mount in current territory.");
            return false;
        }

        await CheckIfPlayerIsStunned(token);

        bool useRoulette;
        unsafe
        {
            useRoulette = C.UseMountRoulette || !PlayerState.Instance()->IsMountUnlocked(C.MountId);
        }

        var actionType = useRoulette
                                 ? ActionType.GeneralAction
                                 : ActionType.Mount;
        var actionId = useRoulette
                               ? 9u
                               : C.MountId;

        var castTime      = Player.GetAdjustedCastTime(actionType, actionId);
        var deadGapBuffer = TimeSpan.FromSeconds(3);

        var step = new ResilientStep(
                                     "Mount",
                                     () =>
                                     {
                                         unsafe
                                         {
                                             if (ActionManager.Instance()->GetActionStatus(actionType, actionId) == 0)
                                                 ActionManager.Instance()->UseAction(actionType, actionId);
                                         }

                                         return Task.CompletedTask;
                                     },
                                     () => Player.Mounted,
                                     () => StallDetectors.FlagMismatch(
                                                                       () => Player.IsBusy,
                                                                       () => Player.Mounted,
                                                                       castTime + deadGapBuffer),
                                     async () =>
                                     {
                                         if (Svc.Condition[ConditionFlag.InCombat])
                                         {
                                             AutoRotation.Enable(C.AutoRotationPlugin);
                                             Bossmod.EnableAI();
                                             await HandleHaters(token: token);
                                             Bossmod.DisableAI();
                                             AutoRotation.Disable(C.AutoRotationPlugin);
                                         }

                                         await WaitWhileAsync(() => Player.IsBusy, "Wait while Player is busy", token);
                                     },
                                     3,
                                     castTime + deadGapBuffer + TimeSpan.FromSeconds(10));

        await step.RunAsync(token);
        return true;
    }

    internal static async Task<bool> Mount(uint mountId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        unsafe
        {
            if (Player.Mounted && Player.BattleChara->Mount.MountId == mountId) return true;
        }

        using var scope = new TaskDescriptionScope("Mounting");
        await WaitWhileAsync(() => IsBusy(token), "Waiting for Player Status not busy!", token);
        if (!Player.CanMount)
        {
            TaskLog.Debug("Can not mount in current territory.");
            return false;
        }

        await CheckIfPlayerIsStunned(token);
        unsafe
        {
            var actionManager = ActionManager.Instance();
            if (PlayerState.Instance()->IsMountUnlocked(mountId))
            {
                if (actionManager->GetActionStatus(ActionType.Mount, mountId) != 0) return false;
                actionManager->UseAction(ActionType.Mount, mountId);
            }
            else
                return false;
        }

        await WaitUntilAsync(() => Player.Mounted, "Waiting for Mount", token);
        return true;
    }

    internal static async Task Dismount(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope("Dismounting");

        if (!Player.Mounted) return;

        TaskLog.Debug("Dismounting");
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
        await WaitUntilAsync(async () => Vnavmesh.NavIsReady.Invoke() && !await IsBusy(token), "Wait for navmesh", token);
        if (Player.DistanceTo(interactablePosition) >= 30f) await Mount(token);
        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo.Invoke(interactablePosition, Player.Mounted && Player.CanFly), $"Could not find path to {interactablePosition}");
        if (!Player.Mounted && Player.DistanceTo(interactablePosition) > C.MinRunDistance) UseSprint();
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning.Invoke(), "Wait for pathing to start", token);
        if (stopAtDistance)
        {
            await WaitUntilAsync(() => IsPlayerInObjectRange(baseId, distance), "Check for distance", token);
            Vnavmesh.StopCompletely();
        }
        else
        {
            await WaitUntilAsync(() => !Vnavmesh.PathIsRunning.Invoke()         &&
                                       !Vnavmesh.NavPathfindInProgress.Invoke() &&
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
        await WaitUntilAsync(async () => Vnavmesh.NavIsReady.Invoke() && !await IsBusy(token), "Wait for navmesh", token);
        var position = gameObject.Position;

        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo.Invoke(position, Player.DistanceTo(position) > 25 && Player.Mounted && Player.CanFly), $"Could not find path to {gameObject.Position}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning.Invoke(), "Wait for pathing to start", token);

        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        if (recheckPosition)
        {
            var stuckDetector = MakePositionStallDetector(TimeSpan.FromSeconds(6));
            var stallCount    = 0;

            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (gameObject is IBattleNpc { IsDead: true }) return;
                if (gameObject.IsInAttackRange(gameObject.HitboxRadius))
                {
                    Vnavmesh.StopCompletely();
                    break;
                }

                if (Vector3.Distance(position, gameObject.Position) > gameObject.HitboxRadius)
                {
                    if (Vnavmesh.SimpleMovePathfindAndMoveTo.Invoke(gameObject.Position, Player.Mounted && Player.CanFly))
                    {
                        await WaitUntilAsync(() => Vnavmesh.PathIsRunning.Invoke(), "Wait for pathing to start", token);
                        position = gameObject.Position;
                    }
                    else
                        TaskLog.Verbose($"Move to moving object {gameObject.Name}: repath busy, retrying next tick");
                }

                if (stuckDetector())
                {
                    stallCount++;
                    TaskLog.Warning($"Move to moving object {gameObject.Name}: stuck on approach, attempt {stallCount}/3");
                    ErrorThrowIf(stallCount >= 3, $"Stuck approaching {gameObject.Name}: player made no progress after {stallCount} jump-recovery attempts.");

                    unsafe
                    {
                        ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                    }

                    var positionAfterJump = Player.Position;
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                    if (Vector3.Distance(Player.Position, positionAfterJump) < 1f)
                        Vnavmesh.StopCompletely();

                    ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo.Invoke(gameObject.Position, Player.Mounted && Player.CanFly), $"Could not find path to {gameObject.Position}");
                    await WaitUntilAsync(() => Vnavmesh.PathIsRunning.Invoke(), "Wait for pathing to start", token);
                    position      = gameObject.Position;
                    stuckDetector = MakePositionStallDetector(TimeSpan.FromSeconds(6));
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
        await WaitUntilAsync(async () => Vnavmesh.NavIsReady.Invoke() && !await IsBusy(token), "Wait for navmesh", token);
        if (Player.DistanceTo(position) < 1) return;
        if (mount) await Mount(token);
        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo.Invoke(position, Player.Mounted && Player.CanFly), $"Could not find path to {position}");
        await WaitPulseConditionAsync(() => Vnavmesh.PathIsRunning.Invoke(), "Wait for pathing to finish", token);
    }

    internal static async Task MoveCloseToPlayer(Vector3 position, ulong CID, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope = new TaskDescriptionScope($"Move To {position}");
        await WaitUntilAsync(async () => Vnavmesh.NavIsReady.Invoke() && !await IsBusy(token), "Wait for navmesh", token);
        await Task.Delay(2 * GeneralDelayMs, token);
        if (Player.DistanceTo(position) < 2) return;
        if (Player.DistanceTo(position) > C.MinMountDistance) await Mount(token);
        if (!Player.Mounted && Player.DistanceTo(position) > C.MinRunDistance) UseSprint();
        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo.Invoke(position, Player.Mounted && Player.CanFly), $"Could not find path to {position}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning.Invoke(), "Wait for pathing to finish", token);
        await WaitUntilAsync(() =>
                             {
                                 if (Svc.Objects.OfType<IPlayerCharacter>()
                                        .TryGetFirst(x => x.GetContentId() == CID, out var boss))
                                 {
                                     if (Player.DistanceTo(boss!.Position) < 2)
                                     {
                                         Vnavmesh.StopCompletely();
                                         return true;
                                     }
                                 }

                                 return false;
                             }, "Wait until close to player", token);
    }

    internal static async Task MoveToArea(Vector3 position, CancellationToken token = default) => await Underlings.TaskManager.MovementTasks.MoveToArea(position, Options, 50f, token);

    internal static async Task TeleportTo(uint territoryId, uint aetheryteTerritoryId, uint aetheryteId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (Player.TerritoryId != aetheryteTerritoryId && Player.TerritoryId != territoryId)
        {
            using var scope = new TaskDescriptionScope($"Teleport to {territoryId}");
            await TeleportToCore($"Teleport to {territoryId}", aetheryteId,
                                 () => Player.TerritoryId == aetheryteTerritoryId || Player.TerritoryId == territoryId, token);
        }
    }

    internal static async Task TeleportTo(uint aetheryteId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var territory = Svc.Data.GetExcelSheet<Aetheryte>()
                           .GetRow(aetheryteId)
                           .Territory.Value;

        if (Player.TerritoryId != territory.RowId)
        {
            var destinationName = Svc.Data.Excel.GetSheet<Aetheryte>()
                                     .GetRow(aetheryteId)
                                     .PlaceName.Value.Name.ExtractText();
            using var scope = new TaskDescriptionScope($"Teleport to aetheryte {destinationName} ({aetheryteId})");
            await TeleportToCore($"Teleport to {destinationName}", aetheryteId, () => Player.TerritoryId == territory.RowId, token);

            await WaitWhileAsync(() => IsBusy(token), "Wait for meshing/transition", token);
            await WaitUntilAsync(() => Vnavmesh.NavIsReady.Invoke(), "Wait for meshing/transition", token);
        }
    }

    private static async Task TeleportToCore(string name, uint aetheryteId, Func<bool> succeeded, CancellationToken token)
    {
        await CheckIfPlayerIsStunned(token);
        await WaitWhileAsync(() => IsBusy(token), "Wait while Player is busy", token);

        var castTime      = Player.GetAdjustedCastTime(ActionType.Action, TeleportActionId);
        var deadGapBuffer = TimeSpan.FromSeconds(5);

        var step = new ResilientStep(
                                     name,
                                     () =>
                                     {
                                         bool success;
                                         unsafe
                                         {
                                             success = Telepo.Instance()->Teleport(aetheryteId, 0);
                                         }

                                         if (!success)
                                             throw new InvalidOperationException($"Teleport to Aetheryte {aetheryteId} failed.");
                                         return Task.CompletedTask;
                                     },
                                     succeeded,
                                     () => StallDetectors.CastInterrupted(
                                                                          () => Player.IsCasting,
                                                                          () => Player.CastActionId,
                                                                          TeleportActionId,
                                                                          () => Player.CurrentCastTime,
                                                                          () => Player.TotalCastTime),
                                     async () =>
                                     {
                                         if (Svc.Condition[ConditionFlag.InCombat])
                                         {
                                             AutoRotation.Enable(C.AutoRotationPlugin);
                                             Bossmod.EnableAI();
                                             await HandleHaters(token: token);
                                             Bossmod.DisableAI();
                                             AutoRotation.Disable(C.AutoRotationPlugin);
                                         }
                                     },
                                     3,
                                     castTime + deadGapBuffer + TimeSpan.FromSeconds(20));

        await step.RunAsync(token);
    }

    internal static async Task MoveToNextZone(Vector3 zoneTransitionPosition, uint nextTerritoryId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        await WaitWhileAsync(() => IsBusy(token), "Wait for VNav/Player", token);
        await WaitUntilAsync(() => Vnavmesh.NavIsReady.Invoke(), "Wait for VNav/Player", token);
        using var scope = new TaskDescriptionScope($"Move to Zone {nextTerritoryId}");
        await Mount(token);
        ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo.Invoke(zoneTransitionPosition, Player.Mounted && Player.CanFly),
                     $"Could not find path to {zoneTransitionPosition}");
        await WaitUntilAsync(() => Vnavmesh.PathIsRunning.Invoke(), "Wait for pathing to start", token);

        if (!Player.Mounted) UseSprint();

        await WaitUntilAsync(() => Svc.Condition[ConditionFlag.BetweenAreas], "Wait for area change start", token);
        Vnavmesh.StopCompletely();
        await WaitWhileAsync(() => Svc.Condition[ConditionFlag.BetweenAreas], "Wait for area change end", token);
        await WaitUntilAsync(() => Player.TerritoryId == nextTerritoryId, "Check for right territory", token);
    }

    internal static async Task<bool> RoamUntilTargetNearby(List<Vector3> pointList, uint targetNameId, bool gotKilledWhileDetour, bool detourForARanks, float distanceToSpot = 30f, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var scope        = new TaskDescriptionScope("Roam until Target nearby");
        var       path         = SortListByDistance(pointList);
        uint      killedARanks = 0;
        await WaitWhileAsync(() => IsBusy(token), "Wait for VNav/Player", token);
        await WaitUntilAsync(() => Vnavmesh.NavIsReady.Invoke(), "Wait for VNav/Player", token);
        foreach (var point in path)
        {
            if (C.UseMount && Player.DistanceTo(point) > C.MinMountDistance)
                await Mount(token);

            if (!Player.Mounted && Player.DistanceTo(point) > C.MinRunDistance) UseSprint();

#if DEBUG || PRIVATE
            ErrorThrowIf(!Vnavmesh.SimpleMovePathfindAndMoveTo.Invoke(point, Player.Mounted && Player.CanFly), $"Could not find path from {Player.Position} to {point}");
#else
            if (WarningIf(!Vnavmesh.SimpleMovePathfindAndMoveTo.Invoke(point, Player.Mounted && Player.CanFly),
                          $"Could not find path from {Player.Position} to {point}. Skipping to next!"))
                continue;
#endif
            await WaitUntilAsync(() => Vnavmesh.PathIsRunning.Invoke(), "Wait for pathing to start", token);

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
                        AutoRotation.Enable(C.AutoRotationPlugin);
                        Bossmod.EnableAI();

                        if (!await KillTarget(detourTarget, token))
                        {
                            AutoRotation.Disable(C.AutoRotationPlugin);
                            Bossmod.DisableAI();
                            return false;
                        }

                        killedARanks++;

                        var isDummyTarget = targetNameId == int.MaxValue;
                        var exVersion = Svc.Data.GetExcelSheet<TerritoryType>()
                                           .GetRow(Player.TerritoryId)
                                           .ExVersion.RowId;

                        if ((isDummyTarget && exVersion == 0 && killedARanks == 1) ||
                            (isDummyTarget && exVersion > 0  && killedARanks == 2))
                        {
                            AutoRotation.Disable(C.AutoRotationPlugin);
                            Bossmod.DisableAI();
                            return true;
                        }

                        AutoRotation.Disable(C.AutoRotationPlugin);
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
                        if (!plugin!.FoundSpawns.Any(x => x.ToVector2()
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

    internal static async Task UseFerry(uint ferryNpcId, ReadOnlySeString? ferrySelection, ReadOnlySeString ferryConfirmaton, string logDestination, CancellationToken token = default)
    {
        await InteractWithByBaseId(ferryNpcId, token);
        var ferryNpc = Svc.Data.GetExcelSheet<ENpcBase>()
                          .GetRow(ferryNpcId);
        if (ferryNpc.ENpcData.Count > 1 && ferryNpc.ENpcData.Count(x => x.Is<Warp>()) > 1)
            await WaitUntilAsync(() => TrySelectSpecificEntry(ferrySelection!.Value), $"Select passage to {logDestination}", token);
        await WaitUntilAsync(() => ConfirmSpecificSelectOk(ferryConfirmaton), $"Confirm passage to {logDestination}", token);
        await WaitWhileAsync(() => IsPlayerBusy, "Waiting for transition", token);
    }

    private static async Task CheckIfPlayerIsStunned(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        uint[] paralyzeIds = { 17, 216, 482, 988, 3463, 3963 };
        if (paralyzeIds.Any(x => Player.Status.Any(y => y.StatusId == x))) await WaitWhileAsync(() => paralyzeIds.Any(x => Player.Status.Any(y => y.StatusId == x)), "Wait for paralyze status to end", token);
    }

    // TODO: Replace with Mapping The Realm
    internal static async Task HandleTeleportDetour(uint closestAetheryte, uint destinationTerritoryId, Vector3 destinationPosition, CancellationToken token = default)
    {
        if (closestAetheryte > 0 && !IsAetheryteUnlocked(closestAetheryte) && destinationTerritoryId is 139 or 152 or 154 or 155 or 180)
        {
            switch (destinationTerritoryId)
            {
                // Reroute through Western La Noscea if target is on the left side of Upper La Noscea
                case 139:
                {
                    ErrorThrowIf(!IsAetheryteUnlocked(14), $"You aren't attuned to Western La Noscea Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(destinationTerritoryId).PlaceName.Value.Name.ExtractText()} ({destinationTerritoryId})");
                    await TeleportTo(14, token);
                    if (destinationPosition.ToVector2()
                                           .IsWithinRadius(new Vector2(-460f, 150f), 150f))
                        await MoveToNextZone(new Vector3(412f, 31f, -15f), 139, token);
                    else
                    {
                        await MoveToNextZone(new Vector3(812f, 50f, 400f), 134, token);
                        await MoveToNextZone(new Vector3(-162f, 36f, -740f), 137, token);

                        if (!IsAetheryteUnlocked(12))
                        {
                            await MoveTo(new Vector3(-15f, 70.6f, 7f), true, token);
                            await InteractWithByBaseId(12, token);
                            await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.OccupiedInEvent], "Wait for attunement", token);
                        }

                        if (!IsAetheryteUnlocked(15))
                        {
                            await MoveToNextZone(new Vector3(82f, 80f, -125f), 139, token);
                            await MoveTo(new Vector3(427f, 4.11f, 92f), true, token);
                            await InteractWithByBaseId(15, token);
                            await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.OccupiedInEvent], "Wait for attunement", token);
                        }
                    }

                    break;
                }
                case 152:
                {
                    ErrorThrowIf(!IsAetheryteUnlocked(3), $"You aren't attuned to Central Shroud Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(destinationTerritoryId).PlaceName.Value.Name.ExtractText()} ({destinationTerritoryId})");
                    await TeleportTo(3, token);
                    await MoveToNextZone(new Vector3(390f, -3.3f, -186f), 152, token);
                    await MoveTo(new Vector3(-191f, 4.44f, 297f), true, token);
                    await InteractWithByBaseId(4, token);
                    await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.OccupiedInEvent], "Wait for attunement", token);
                    break;
                }
                case 154:
                {
                    ErrorThrowIf(!IsAetheryteUnlocked(2), $"You aren't attuned to New Gridania Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(destinationTerritoryId).PlaceName.Value.Name.ExtractText()} ({destinationTerritoryId})");
                    await TeleportTo(2, token);
                    await MoveToNextZone(new Vector3(-106f, 1.1f, 8f), 133, token);
                    await MoveToNextZone(new Vector3(-208f, 10.4f, -95f), 154, token);

                    await MoveTo(new Vector3(-34f, -40.45f, 232f), true, token);
                    await InteractWithByBaseId(7, token);
                    await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.OccupiedInEvent], "Wait for attunement", token);
                    break;
                }
                case 155:
                {
                    if (IsAetheryteUnlocked(7))
                    {
                        await TeleportTo(7, token);
                        await MoveToNextZone(new Vector3(-369f, -7f, 185f), 155, token);
                    }
                    else
                    {
                        ErrorThrowIf(!IsAetheryteUnlocked(2), $"You aren't attuned to New Gridania Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(destinationTerritoryId).PlaceName.Value.Name.ExtractText()} ({destinationTerritoryId})");
                        await TeleportTo(2, token);
                        await MoveToNextZone(new Vector3(-106f, 1.1f, 8f), 133, token);
                        await MoveToNextZone(new Vector3(-208f, 10.4f, -95f), 154, token);
                        await MoveToNextZone(new Vector3(-369f, -7f, 185f), 155, token);
                    }

                    await MoveTo(new Vector3(229f, 312f, -238f), true, token);
                    await InteractWithByBaseId(23, token);
                    await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.OccupiedInEvent], "Wait for attunement", token);
                    break;
                }
                case 180:
                {
                    ErrorThrowIf(!IsAetheryteUnlocked(14), $"You aren't attuned to Western La Noscea Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(destinationTerritoryId).PlaceName.Value.Name.ExtractText()} ({destinationTerritoryId})");
                    await TeleportTo(14, token);
                    await MoveToNextZone(new Vector3(412f, 31f, -15f), 139, token);
                    await MoveToNextZone(new Vector3(-339f, 48.60f, -19f), 180, token);
                    await MoveTo(new Vector3(-114f, 64.65f, -216f), true, token);
                    await InteractWithByBaseId(16, token);
                    await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.OccupiedInEvent], "Wait for attunement", token);
                    break;
                }
            }
        }
        else if (closestAetheryte > 0)
        {
            if (destinationTerritoryId == 139 &&
                destinationPosition.ToVector2()
                                   .IsWithinRadius(new Vector2(-460f, 150f), 150f) &&
                IsAetheryteUnlocked(14))
            {
                await TeleportTo(14, token);
                await MoveToNextZone(new Vector3(412f, 31f, -15f), 139, token);
            }
            else
                await TeleportTo(closestAetheryte, token);
        }
        else
        {
            ErrorThrowIf(closestAetheryte == 0 || !IsAetheryteUnlocked(closestAetheryte),
                         $"You aren't attuned to any Aetheryte for the Hunt Marks territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(destinationTerritoryId).PlaceName.Value.Name.ExtractText()} ({destinationTerritoryId})");
        }

        if (Player.TerritoryId == 478) await MoveToNextZone(new Vector3(164f, 207f, 129f), 399, token);
    }
}
