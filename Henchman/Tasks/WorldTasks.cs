using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;

namespace Henchman.Tasks;

internal static class WorldTasks
{
    internal static async Task InteractWithByBaseId(uint baseId, CancellationToken token = default)
    {
        await WaitUntilAsync(() => TargetNearestByBaseId(baseId, token), $"Target {baseId}", token);
        await Task.Delay(GeneralDelayMs * 2, token);
        unsafe
        {
            TargetSystem.Instance()->InteractWithObject(TargetSystem.Instance()->Target, false);
        }
    }

    internal static async Task<IGameObject> GetNearestGameObjectByBaseId(uint baseId, CancellationToken token = default)
    {
        IGameObject? gameObject = null;
        while (gameObject == null)
        {
            token.ThrowIfCancellationRequested();
            if (!IsOccupied())
            {
                gameObject = Svc.Objects.Where(obj => obj.BaseId == baseId && obj.IsTargetable)
                                .OrderBy(x => Player.DistanceTo(x))
                                .FirstOrDefault();

                if (gameObject != null) break;

                await Task.Delay(GeneralDelayMs, token);
            }
        }

        return gameObject;
    }

    internal static async Task<IGameObject?> GetNearestMobByNameId(uint nameId, bool moveOnTimeout = false, CancellationToken token = default)
    {
        IBattleNpc? gameObject = null;
        var         iterations = 0;
        while (gameObject == null)
        {
            token.ThrowIfCancellationRequested();
            if (!IsOccupied())
            {
                var foundTargets = Svc.Objects.OfType<IBattleNpc>()
                                      .Where(bnpc => bnpc is { IsTargetable: true, IsDead: false } && bnpc.NameId == nameId && (bnpc.TargetObject == null || bnpc.TargetObject.Equals(Player.Object)))
                                      .OrderBy(x => Player.DistanceTo(x))
                                      .ToList();

                if (foundTargets.Count == 1)
                    gameObject = foundTargets.First();
                else if (foundTargets.Count > 1)
                {
                    var paths = new List<(IBattleNpc target, List<Vector3> pathTo, float pathLength)>();
                    foreach (var target in foundTargets)
                    {
                        var navTask = await Vnavmesh.NavPathfind(Player.Position, target.Position, false);
                        paths.Add(new ValueTuple<IBattleNpc, List<Vector3>, float>(target, navTask, navTask.TotalDistance()));
                    }

                    var shortestPath = paths.OrderBy(p => p.pathLength)
                                            .First();
                    gameObject = shortestPath.target;
                }

                if (gameObject != null) return gameObject;

                // 120 * 500 ms iterations to move on after 1 minute
                if (moveOnTimeout && iterations == 120) return null;

                await Task.Delay(GeneralDelayMs * 2, token);
                iterations++;
            }
        }

        return gameObject;
    }

    internal static Task<bool> IsPlayerInObjectRange(uint baseId, float distance = 5f)
    {
        var x = Svc.Objects.Where(obj => obj.BaseId == baseId && obj.IsTargetable)
                   .OrderBy(x => Player.DistanceTo(x))
                   .FirstOrDefault();

        return x == null
                       ? Task.FromResult(false)
                       : Task.FromResult(Player.DistanceTo(x) < distance);
    }

    internal static async Task<bool> TargetByEntityId(uint entityId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!IsOccupied())
        {
            var x = Svc.Objects.SearchByEntityId(entityId);
            if (x != null)
            {
                await Task.Delay(GeneralDelayMs, token);
                Svc.Targets.Target = x;
                return true;
            }
        }

        return false;
    }

    internal static async Task<bool> TargetByName(string targetName, CancellationToken token = default) => await TargetNearestByName([targetName], token);

    internal static async Task<bool> TargetNearestByName(IEnumerable<string> targetName, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!IsOccupied())
        {
            var gameObject = Svc.Objects.Where(obj => targetName.Contains(obj.Name.TextValue) && obj.IsTargetable)
                                .OrderBy(x => Player.DistanceTo(x))
                                .FirstOrDefault();
            if (gameObject != null)
            {
                await Task.Delay(GeneralDelayMs, token);
                Svc.Targets.Target = gameObject;
                return true;
            }
        }

        return false;
    }

    internal static async Task<bool> TargetNearestByBaseId(uint baseId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!IsOccupied())
        {
            var x = Svc.Objects.Where(obj => obj.BaseId == baseId && obj.IsTargetable)
                       .OrderBy(x => Player.DistanceTo(x))
                       .FirstOrDefault();
            if (x != null)
            {
                Svc.Targets.Target = x;
                await Task.Delay(GeneralDelayMs, token);
                return true;
            }
        }

        return false;
    }


    internal static async Task<bool> TargetNearestByBaseId(IEnumerable<uint> baseId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!IsOccupied())
        {
            var x = Svc.Objects.Where(obj => baseId.Contains(obj.BaseId) && obj.IsTargetable)
                       .OrderBy(x => Player.DistanceTo(x))
                       .FirstOrDefault();
            if (x != null)
            {
                await Task.Delay(GeneralDelayMs, token);
                Svc.Targets.Target = x;
                return true;
            }
        }

        return false;
    }

    internal static async Task<bool> IsInFate(ushort fateId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        await Task.Delay(GeneralDelayMs, token);
        unsafe
        {
            return FateManager.Instance()->CurrentFate != null && FateManager.Instance()->CurrentFate->FateId == fateId;
        }
    }

    internal static async Task<bool> IsFateActive(ushort fateId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        await Task.Delay(GeneralDelayMs, token);
        return Svc.Fates.Any(x => x.FateId == fateId && x.Progress < 50);
    }
}
