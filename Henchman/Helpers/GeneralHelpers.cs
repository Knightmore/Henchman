using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Map = Lumina.Excel.Sheets.Map;

namespace Henchman.Helpers;

internal static class GeneralHelpers
{
    public enum KillResult
    {
        Success,
        Died,
        NoSpawns
    }

    public static bool IsPlayerBusy => IsOccupied() || Player.Object.IsCasting || Player.IsMoving || Player.IsAnimationLocked;
    internal static unsafe float GetChocoboTimeLeft => UIState.Instance()->Buddy.CompanionInfo.TimeLeft;
    internal static bool IsPlayerInObjectRange3D(IGameObject gameObj, float distance = 5f) => Player.DistanceTo(gameObj) < distance;
    internal static bool IsPlayerInObjectRange2D(IGameObject gameObj, float distance = 5f) => Player.DistanceTo(gameObj) < distance;
    internal static bool IsPlayerInPositionRange3D(Vector3 position, float distance = 5f) => Player.DistanceTo(position) < distance;
    internal static bool IsPlayerInPositionRange2D(Vector2 position, float distance = 5f) => Player.DistanceTo(position) < distance;

    internal static bool IsMobNearby(uint nameId)
    {
        var x = Svc.Objects.OfType<IBattleNpc>()
                   .Where(obj => obj.NameId == nameId && obj is { IsTargetable: true, IsDead: false })
                   .OrderBy(x => Player.DistanceTo(x))
                   .FirstOrDefault();
        return x != null;
    }

    internal static IGameObject? GetMobNearby(uint nameId)
    {
        var x = Svc.Objects.OfType<IBattleNpc>()
                   .Where(obj => obj.NameId == nameId && obj is { IsTargetable: true, IsDead: false })
                   .OrderBy(x => Player.DistanceTo(x))
                   .FirstOrDefault();
        return x;
    }

    internal static IGameObject? GetNearestSummoningBell()
    {
        return Svc.Objects.Where(obj => obj.Name.ToString()
                                           .EqualsIgnoreCase(Svc.Data.Excel.GetSheet<EObjName>()
                                                                .GetRow(2000072)
                                                                .Singular.ExtractText()))
                  .OrderBy(x => Player.DistanceTo(x))
                  .FirstOrDefault();
    }

    internal static async Task<bool> Mount(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (Player.Mounted)
        {
            return true;
        }
        using var scope = new TaskDescriptionScope("Mounting");
        // ToDo: Recheck after a while. Sometimes the player was forced to walk, because the Player was somehow still AnimationLocked and Walking. Remove again if other problems occur with it.
        if (Player.IsBusy)
            await WaitUntilAsync(() => !Player.IsBusy, "Waiting for Player Status not busy!", token);
        /*if (Player.IsBusy)
        {
            Verbose("Can not mount. Player is busy.");
            Verbose($"IsOccupied: {IsOccupied()} | IsCasting: {Player.Object.IsCasting} | IsMoving: {Player.IsMoving} | IsAnimationLocked: {Player.IsAnimationLocked} | InCombat: {Svc.Condition[ConditionFlag.InCombat]}");
            return false;
        }*/
        if (!Svc.Data.GetExcelSheet<TerritoryType>()
                .GetRow(Svc.ClientState.TerritoryType)
                .Mount)
        {
            Verbose("Can not mount in current territory.");
            return false;
        }

        Verbose("Using Mount");
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
        ;
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

    internal static async Task IsTargetDead(IGameObject? target, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        while (target is { IsDead: false })
            await Task.Delay(GeneralDelayMs, token);
    }

    private static float DistanceToHitboxEdge(Vector3 targetPos, float hitboxRadius) => Player.DistanceTo(targetPos) - hitboxRadius;

    internal static bool IsInMeleeRange(Vector3 targetPos, float hitboxRadius) => DistanceToHitboxEdge(targetPos, hitboxRadius + 1.5f) <= 2;

    internal static unsafe bool UseAction(ActionType actionType, uint actionId) => ActionManager.Instance()->UseAction(actionType, actionId);

    internal static unsafe bool IsActionUsable(ActionType actionType, uint actionId) => ActionManager.Instance()->GetActionStatus(actionType, actionId) == 0;


    internal static bool UseSprint()
    {
        if (IsActionUsable(ActionType.GeneralAction, 4))
            UseAction(ActionType.GeneralAction, 4);
        else
            return true;

        return false;
    }

    internal static async Task<bool> IsInFate(ushort fateId, CancellationToken token = default)
    {
        await Task.Delay(GeneralDelayMs, token);
        unsafe
        {
            return FateManager.Instance()->CurrentFate != null && FateManager.Instance()->CurrentFate->FateId == fateId;
        }
    }

    internal static async Task<bool> IsFateActive(ushort fateId, CancellationToken token = default)
    {
        await Task.Delay(GeneralDelayMs, token);
        return Svc.Fates.Any(x => x.FateId == fateId && x.Progress < 50);
    }

    /*
     * TODO: Switch to MappingTheRealm once/if ever released.
     */
    internal static uint GetAetheryte(uint territoryId, Vector3 destinationPos)
    {
        if (territoryId == 399)
            return 75;
        var aetherytes = GetSheet<Aetheryte>()
                       ?.Where(x => x.Territory.RowId == territoryId && x.IsAetheryte)
                        .ToList();

        if (aetherytes == null || aetherytes.Count == 0)
            return 0;


        var closest = aetherytes
                     .OrderBy(x => Vector2.DistanceSquared(destinationPos.ToVector2(), GetAetherytePosition(x.RowId)
                                                                  .ToVector2()))
                     .First();

        return closest.RowId;
    }

    internal static uint GetAetheryte(uint territoryId)
    {
        if (territoryId == 399)
            return 75;

        var aetheryte = GetSheet<Aetheryte>()
                      ?.Where(x => x.Territory.RowId == territoryId && x.IsAetheryte)
                       .FirstOrDefault();

        if (aetheryte == null)
            return 0;

        return aetheryte.Value.RowId;
    }

    internal static unsafe bool IsAetheryteUnlocked(uint aetheryteId) => UIState.Instance()->IsAetheryteUnlocked(aetheryteId);

    internal static Vector3 GetAetherytePosition(uint aetheryteId)
    {
        var aetheryte = GetRow<Aetheryte>(aetheryteId)!.Value;
        var level = aetheryte.Level[0].ValueNullable;

        if (level != null)
            return new Vector3(level.Value.X, level.Value.Y, level.Value.Z);

        var marker = FindRow<MapMarker>(m => m.DataType == 3 && m.DataKey.RowId == aetheryte.RowId) ?? FindRow<MapMarker>(m => m.DataType == 4 && m.DataKey.RowId == aetheryte.AethernetName.RowId)!;

        return ConvertMapCoordXZToWorldCoord(marker.Value.X, marker.Value.Y, aetheryte.Territory.Value.Map.Value);
    }

    internal static Vector3 ConvertMapCoordXZToWorldCoord(float x, float z, Map map) => new((x / map.SizeFactor * 100.0f) - (map.OffsetX / (map.SizeFactor * 100.0f)) - 1024.0f, 0, (z / map.SizeFactor * 100.0f) - (map.OffsetY / (map.SizeFactor * 100.0f)) - 1024.0f);

    internal static Vector2 ConvertCurrentToMapXZ()
    {
        var map = Svc.Data.GetExcelSheet<TerritoryType>()
                     .GetRow(Player.Territory)
                     .Map.Value;
        return WorldToMap(Player.Position.ToVector2(), map.OffsetX, map.OffsetY, map.SizeFactor);
    }

    internal static Vector2 MapToWorld(Vector2 mapCoordinates, Map map) => MapToWorld(mapCoordinates, map.OffsetX, map.OffsetY, map.SizeFactor);

    internal static Vector2 MapToWorld(Vector2 mapCoordinates, int xOffset = 0, int yOffset = 0, uint scale = 100) => new(ConvertMapCoordToWorldCoord(mapCoordinates.X, scale, xOffset),
                                                                                                                          ConvertMapCoordToWorldCoord(mapCoordinates.Y, scale, yOffset));

    private static float ConvertMapCoordToWorldCoord(float mapCoord, uint scale, int offset) => (mapCoord - 1.0f - (2048f / scale) - (0.02f * offset)) / 0.02f;

    internal static Vector2 WorldToMap(Vector2 worldCoordinates, int xOffset = 0, int yOffset = 0, uint scale = 100) => new(MathF.Round(ConvertWorldCoordToMapCoord(worldCoordinates.X, scale, xOffset), 1), MathF.Round(ConvertWorldCoordToMapCoord(worldCoordinates.Y, scale, yOffset), 1));

    private static float ConvertWorldCoordToMapCoord(float worldCoord, uint scale, int offset) => worldCoord * 0.02f + 1.0f + (2048f / scale) + (0.02f * offset);


    internal static bool IsWithinRadius(Vector2 x, Vector2 y, float radius = 50f) => Vector2.DistanceSquared(x, y) <= radius * radius;

    internal static unsafe int GetHaterCount() => UIState.Instance()->Hater.HaterCount;

    internal static unsafe Span<HaterInfo> GetHaters() => UIState.Instance()->Hater.Haters;

    internal static unsafe HaterInfo[] GetHatersArray() => UIState.Instance()->Hater.Haters.ToArray();

    internal static unsafe int GetItemAmount(uint itemId) => InventoryManager.Instance()->GetInventoryItemCount(itemId);
    internal static float TotalDistance(this List<Vector3> points) => Enumerable.Range(0, points.Count - 1).Select(i => Vector3.Distance(points[i], points[i + 1])).Sum();
}
