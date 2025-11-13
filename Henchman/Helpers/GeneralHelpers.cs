using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
        Default,
        Success,
        Died,
        NoSpawns
    }

    public static bool IsPlayerMelee => Svc.Data.GetExcelSheet<ClassJob>()
                                           .GetRow(Player.JobId)
                                           .Role is 1 or 2;

    public static          bool  IsPlayerBusy                                                       => IsOccupied() || Player.Object.IsCasting || Player.IsMoving || Player.IsAnimationLocked;
    internal static unsafe float GetChocoboTimeLeft                                                 => UIState.Instance()->Buddy.CompanionInfo.TimeLeft;
    internal static        bool  IsPlayerInObjectRange3D(IGameObject gameObj,  float distance = 5f) => Player.DistanceTo(gameObj)  < distance;
    internal static        bool  IsPlayerInObjectRange2D(IGameObject gameObj,  float distance = 5f) => Player.DistanceTo(gameObj)  < distance;
    internal static        bool  IsPlayerInPositionRange3D(Vector3   position, float distance = 5f) => Player.DistanceTo(position) < distance;
    internal static        bool  IsPlayerInPositionRange2D(Vector2   position, float distance = 5f) => Player.DistanceTo(position) < distance;

    internal static unsafe byte? GetFirstGearsetForClassJob(ClassJob cj)
    {
        byte? backup        = null;
        var   gearsetModule = RaptureGearsetModule.Instance();
        for (var i = 0; i < 100; i++)
        {
            var gearset = gearsetModule->GetGearset(i);
            if (gearset == null) continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            if (gearset->Id       != i) continue;
            if (gearset->ClassJob == cj.RowId) return gearset->Id;
            if (backup == null && cj.ClassJobParent.RowId != 0 && gearset->ClassJob == cj.ClassJobParent.RowId) backup = gearset->Id;
        }

        return backup;
    }

    internal static unsafe bool ChangeToHighestGearsetForClassJobId(uint classJobId)
    {
        var gearsetModule = RaptureGearsetModule.Instance();
        var filtered = gearsetModule->Entries.ToArray()
                                             .Where(x => x.ClassJob == classJobId);

        var gearset = filtered.Any()
                              ? filtered.MaxBy(x => x.ItemLevel)
                              : (RaptureGearsetModule.GearsetEntry?)null;


        if (gearset != null)
        {
            Verbose($"Highest set is {gearset.Value.Id + 1}");
            RaptureGearsetModule.Instance()->EquipGearset(gearset.Value.Id);
            return true;
        }

        return false;
    }


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

    internal static async Task IsTargetDead(IGameObject? target, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        while (target is { IsDead: false })
            await Task.Delay(GeneralDelayMs, token);
    }

    private static float DistanceToHitboxEdge(Vector3 targetPos, float hitboxRadius) => Player.DistanceTo(targetPos) - hitboxRadius;

    internal static bool IsInAttackRange(this IGameObject target, float baseHitboxRadius)
    {
        const float DefaultRangeBuffer = 1.5f;
        const float RangedAdjustment   = 8f;
        const float MaxRange           = 2f;

        var jobRole = Svc.Data.GetExcelSheet<ClassJob>()
                        ?.GetRow(Player.JobId)
                         .Role ??
                      0;
        var adjustedRadius = baseHitboxRadius +
                             (jobRole is 1 or 2
                                      ? 0f
                                      : RangedAdjustment) +
                             DefaultRangeBuffer;

        return DistanceToHitboxEdge(target.Position, adjustedRadius) <= MaxRange;
    }


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

    internal static unsafe byte GetGrandCompanyRank() => PlayerState.Instance()->GetGrandCompanyRank();

    internal static unsafe void ChangeBait(int baitId) => ((FishingEventHandler*)EventFramework.Instance()->GetEventHandlerById(0x150001))->ChangeBait(baitId);



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
        var level     = aetheryte.Level[0].ValueNullable;

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

    private static float ConvertWorldCoordToMapCoord(float worldCoord, uint scale, int offset) => (worldCoord * 0.02f) + 1.0f + (2048f / scale) + (0.02f * offset);

    internal static bool IsWithinRadius(this Vector2 x, Vector2 y, float radius = 50f) => Vector2.DistanceSquared(x, y) <= radius * radius;

    internal static unsafe int GetHaterCount() => UIState.Instance()->Hater.HaterCount;

    internal static unsafe Span<HaterInfo> GetHaters() => UIState.Instance()->Hater.Haters;

    internal static float TotalDistance(this List<Vector3> points) => Enumerable.Range(0, Math.Max(0, points.Count - 1))
                                                                                .Where(i => i + 1 < points.Count)
                                                                                .Select(i => Vector3.Distance(points[i], points[i + 1]))
                                                                                .Sum();

    internal static unsafe bool AddonReady(string addonName) => TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon);

    public static Vector2 GetRandomPoint(Vector2 A, Vector2 B)
    {
        var rand = new Random();
        var  minX = Math.Min(A.X, B.X);
        var  maxX = Math.Max(A.X, B.X);
        var  minY = Math.Min(A.Y, B.Y);
        var  maxY = Math.Max(A.Y, B.Y);

        var randomX = (float)(minX + (rand.NextDouble() * (maxX - minX)));
        var randomY = (float)(minY + (rand.NextDouble() * (maxY - minY)));

        return new Vector2(randomX, randomY);
    }
}
