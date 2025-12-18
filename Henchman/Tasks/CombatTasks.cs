using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Data;
using Henchman.Helpers;
using Henchman.Models;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;

namespace Henchman.Tasks;

internal static class CombatTasks
{
    public static async Task ProcessHuntMarks(List<HuntMark> huntMarks, bool huntLog = false, int currentRank = 0, bool gcLog = false, CancellationToken token = default)
    {
        Verbose("Process Hunt Marks");
        foreach (var mark in huntMarks)
        {
            token.ThrowIfCancellationRequested();

            if (mark.IsDuty) continue;
            if (C.SkipFateMarks && mark.FateId > 0) continue;
            Verbose($"Needed Kills: {(huntLog ? mark.GetOpenMonsterNoteKills : mark.GetOpenMobHuntKills)}");

            var         retries              = 0;
            KillResult? killResult           = KillResult.Default;
            var         gotKilledWhileDetour = false;
            mark.IsCurrentTarget = true;
            while (retries < 3)
            {
                Verbose($"Try: {retries}");
                if (Player.Territory.RowId != mark.TerritoryId)
                {
                    if (!mark.Positions.TryGetFirst(out var markPosition))
                    {
                        FullError($"HuntMark {mark.Name} has no valid position!");
                        break;
                    }

                    var closestAetheryte = GetAetheryte(mark.TerritoryId, markPosition);
                    // TODO: Switch to MappingTheRealm once/if ever released.
                    if (closestAetheryte > 0 && !IsAetheryteUnlocked(closestAetheryte) && mark.TerritoryId is 139 or 152 or 154 or 155 or 180)
                    {
                        switch (mark.TerritoryId)
                        {
                            // Reroute through Western La Noscea if target is on the left side of Upper La Noscea
                            case 139:
                            {
                                ErrorThrowIf(!IsAetheryteUnlocked(14), $"You aren't attuned to Western La Noscea Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(mark.TerritoryId).PlaceName.Value.Name.ExtractText()} ({mark.TerritoryId})");
                                await TeleportTo(14, token);
                                if (markPosition.ToVector2()
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
                                ErrorThrowIf(!IsAetheryteUnlocked(3), $"You aren't attuned to Central Shroud Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(mark.TerritoryId).PlaceName.Value.Name.ExtractText()} ({mark.TerritoryId})");
                                await TeleportTo(3, token);
                                await MoveToNextZone(new Vector3(390f, -3.3f, -186f), 152, token);
                                await MoveTo(new Vector3(-191f, 4.44f, 297f), true, token);
                                await InteractWithByBaseId(4, token);
                                break;
                            }
                            case 154:
                            {
                                ErrorThrowIf(!IsAetheryteUnlocked(2), $"You aren't attuned to New Gridania Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(mark.TerritoryId).PlaceName.Value.Name.ExtractText()} ({mark.TerritoryId})");
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
                                    ErrorThrowIf(!IsAetheryteUnlocked(2), $"You aren't attuned to New Gridania Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(mark.TerritoryId).PlaceName.Value.Name.ExtractText()} ({mark.TerritoryId})");
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
                                ErrorThrowIf(!IsAetheryteUnlocked(14), $"You aren't attuned to Western La Noscea Aetheryte for rerouting to territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(mark.TerritoryId).PlaceName.Value.Name.ExtractText()} ({mark.TerritoryId})");
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
                        if (mark.TerritoryId == 139 &&
                            markPosition.ToVector2()
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
                                     $"You aren't attuned to any Aetheryte for the Hunt Marks territory {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(mark.TerritoryId).PlaceName.Value.Name.ExtractText()} ({mark.TerritoryId})");
                    }

                    // TODO: Switch to MappingTheRealm once/if ever released.
                    if (Player.Territory.RowId == 478) await MoveToNextZone(new Vector3(164f, 207f, 129f), 399, token);
                }

                await CheckChocobo(token);

                if (mark.FateId == 0)
                {
                    Verbose($"{mark.MobHuntRowId} | {mark.MobHuntSubRowId} | {mark.GetMobHuntOrderRow.MobHuntReward.RowId}");
                    if (huntLog || (!huntLog && mark.GetMobHuntOrderRow.MobHuntReward.RowId % 4 != 3))
                    {
                        if (mark.Positions.Count > 0)
                        {
                            var distanceOrderedPositions = Utils.SortListByDistance([.. mark.Positions]);

                            for (var i = 0;;)
                            {
                                var markPosition = distanceOrderedPositions[i];
                                Verbose($"Trying position {i + 1}/{distanceOrderedPositions.Count} for {mark.Name}");

                                // TODO: Remove once MappingTheRealm is done. This is a reroute to properly get inside the underwater dome.
                                if (Player.Territory.RowId == 613)
                                {
                                    if (markPosition.ToVector2()
                                                    .IsWithinRadius(new Vector2(175f, 223f)))
                                    {
                                        await MoveTo(new Vector3(198.923f, -161.5932f, 115.7771f), true, token);
                                        await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.BetweenAreas], "Waiting for area change", token);
                                    }
                                    else
                                    {
                                        if (Player.Position.ToVector2()
                                                  .IsWithinRadius(new Vector2(175f, 223f)))
                                        {
                                            await MoveTo(new Vector3(201f, -162f, 118f), true, token);
                                            await WaitPulseConditionAsync(() => Svc.Condition[ConditionFlag.BetweenAreas], "Waiting for area change", token);
                                        }
                                    }
                                    
                                }

                                if (Vnavmesh.QueryMeshPointOnFloor(markPosition with { Y = markPosition.Y + 5 }, false, 0.05f) is { } landablePosition)
                                    await MoveToArea(landablePosition, token);
                                else
                                    await MoveToArea(markPosition, token);

                                await WaitUntilAsync(() => !Vnavmesh.PathIsRunning() && !Vnavmesh.NavPathfindInProgress(), "Wait for moving to Area", token);

                                killResult = await KillCountedHuntMark(mark, huntLog, currentRank, gcLog, token);
                                if (killResult == KillResult.Success)
                                    break;

                                if (killResult == KillResult.Died)
                                {
                                    retries++;
                                    break;
                                }

                                i++;
                                if (i >= distanceOrderedPositions.Count) i = 0;
                                await Task.Delay(GeneralDelayMs * 2, token);
                            }
                        }
                        else
                            FullError($"HuntMark {mark.Name} has no positions!");

                        if (killResult != KillResult.Died)
                            break;
                    }
                    else if (mark.Positions.Count > 0)
                    {
                        if (!await RoamUntilTargetNearby(mark.Positions, mark.BNpcNameRowId, gotKilledWhileDetour, C.DetourForARanks, token: token))
                        {
                            gotKilledWhileDetour = true;
                            retries++;
                            continue;
                        }

                        killResult = await KillCountedHuntMark(mark, huntLog, currentRank, gcLog, token);
                        if (killResult == KillResult.Success)
                            break;

                        if (killResult == KillResult.Died) retries++;
                    }
                }
                else
                {
                    await WaitUntilAsync(() => IsFateActive((ushort)mark.FateId, token), $"Checking for Fate {mark.FateId}", token);
                    Vector3 fatePosition;
                    unsafe
                    {
                        fatePosition = FateManager.Instance()->GetFateById((ushort)mark.FateId)->Location;
                    }

                    if (Vnavmesh.QueryMeshPointOnFloor(fatePosition with { Y = fatePosition.Y + 20 }, false, 0.05f) is { } landableFatePosition)
                        await MoveToArea(landableFatePosition, token);
                    else
                        await MoveToArea(fatePosition, token);

                    await WaitUntilAsync(() => !Vnavmesh.PathIsRunning() && !Vnavmesh.NavPathfindInProgress(), "Wait for walking to Area", token);

                    killResult = await KillCountedHuntMark(mark, huntLog, currentRank, gcLog, token);
                    if (killResult == KillResult.Success)
                        break;

                    if (killResult == KillResult.Died) retries++;
                }
            }

            mark.IsCurrentTarget = false;
        }
    }

    public static async Task ProcessDutyMarks(List<HuntMark?> huntMarks, CancellationToken token = default)
    {
        Verbose("Process Duty Marks");
        token.ThrowIfCancellationRequested();
        var duties = huntMarks.Select(h => h!.TerritoryId)
                              .Distinct()
                              .ToList();
        foreach (var duty in duties)
        {
            var ADPathAvailable = AutoDuty.ContentHasPath(duty);
            var dutyUnlocked    = UIState.IsInstanceContentUnlocked(duty);

            if (dutyUnlocked && ADPathAvailable)
            {
                AutoDuty.RunDutySupport(duty);
                await WaitUntilAsync(AutoDuty.IsStopped, "Waiting for Duty to finish", token);
            }
            else if (!dutyUnlocked && ADPathAvailable)
            {
                if (!SubscriptionManager.IsInitialized(IPCNames.Questionable)) FullWarning("Questionable not enabled! Skipping duty!");
                switch (duty)
                {
                    case 1245:
                        await Questionable.CompleteQuest("697", 66233, token);
                        AutoDuty.RunDutySupport(duty);
                        await WaitUntilAsync(AutoDuty.IsStopped, "Waiting for Duty to finish", token);
                        huntMarks.ForEach(x => x.IsCurrentTarget = false);
                        break;
                    case 1267:
                        await Questionable.CompleteQuest("764", 66300, token);
                        AutoDuty.RunDutySupport(duty);
                        await WaitUntilAsync(AutoDuty.IsStopped, "Waiting for Duty to finish", token);
                        huntMarks.ForEach(x => x.IsCurrentTarget = false);
                        break;
                    case 1303:
                        await Questionable.CompleteQuest("921", 66457, token);
                        AutoDuty.RunDutySupport(duty);
                        await WaitUntilAsync(AutoDuty.IsStopped, "Waiting for Duty to finish", token);
                        huntMarks.ForEach(x => x.IsCurrentTarget = false);
                        break;
                }
                // TODO: Add Dzemael and Aurum Vale once they have Duty Support.
            }
            else
                FullWarning($"There is no AutoDuty Path for Duty {Svc.Data.Excel.GetSheet<TerritoryType>().GetRow(duty).PlaceName.Value.Name.ExtractText()}");
        }
    }

    internal static async Task<KillResult> KillHuntMark(HuntMark huntMark, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        AutoRotation.Enable();
        Bossmod.EnableAI();

        using var scope = new TaskDescriptionScope($"Hunting Mark: {huntMark.Name}");


        if (huntMark.FateId > 0)
        {
            await WaitUntilAsync(() => IsInFate((ushort)huntMark.FateId, token),
                                 $"Wait for Fate {huntMark.Fate.Name.ExtractText()} ({huntMark.FateId}) to spawn", token);
            if (Player.Level > huntMark.Fate.ClassJobLevelMax) Chat.SendMessage("/lsync");
        }


        if (await GetNearestMobByNameId(huntMark.BNpcNameRowId, true, token) is not { } targetedMark)
            return KillResult.NoSpawns;

        if (!await KillTarget(targetedMark, token))
            return KillResult.Died;

        await Task.Delay(GeneralDelayMs * 2, token);
        await HandleHaters(token);

        Bossmod.DisableAI();
        AutoRotation.Disable();

        return KillResult.Success;
    }

    private static async Task<KillResult> KillCountedHuntMark(HuntMark huntMark, bool huntLog, int currentRank, bool gcLog, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        AutoRotation.Enable();
        Bossmod.EnableAI();

        using var scope = new TaskDescriptionScope($"Killing Counted Hunt Mark: {huntMark.Name}");

        Verbose($"HuntLog: {huntLog}");
        Verbose($"Open Kills: {(huntLog ? huntMark.GetOpenMonsterNoteKills : huntMark.GetOpenMobHuntKills)}");
        Verbose($"Killing Hunt Mark: {huntMark.Name} ({huntMark.BNpcNameRowId} {huntMark.MobHuntRowId} {huntMark.MobHuntSubRowId} {huntMark.GetCurrentMobHuntKills} {huntMark.GetOpenMobHuntKills})");

        while (huntLog
                       ? huntMark.GetOpenMonsterNoteKills > 0
                       : huntMark.GetOpenMobHuntKills     > 0)
        {
            var openKills = huntLog
                                    ? huntMark.GetOpenMonsterNoteKills
                                    : huntMark.GetOpenMobHuntKills;
            if (huntMark.FateId > 0)
            {
                await WaitUntilAsync(() => IsInFate((ushort)huntMark.FateId, token),
                                     $"Wait for Fate {huntMark.Fate.Name.ExtractText()} ({huntMark.FateId}) to spawn", token);
                if (Player.Level > huntMark.Fate.ClassJobLevelMax) Chat.SendMessage("/lsync");
            }


            if (await GetNearestMobByNameId(huntMark.BNpcNameRowId, true, token) is not { } targetedMark)
                return KillResult.NoSpawns;

            if (!await KillTargetInternal(huntMark, targetedMark, true, huntLog, openKills, token: token))
                return KillResult.Died;

            Verbose($"Mobs to kill left: {(huntLog ? huntMark.GetOpenMonsterNoteKills : huntMark.GetOpenMobHuntKills)}");
            await Task.Delay(GeneralDelayMs * 4, token);
            await HandleHaters(token);
            await Task.Delay(GeneralDelayMs * 2, token);

            if (huntLog)
            {
                var checkedRank = gcLog
                                          ? HuntLogHelper.GetGrandCompanyRankInfo()
                                          : HuntLogHelper.GetClassJobRankInfo();
                if (currentRank != checkedRank) break;
            }
        }

        Bossmod.DisableAI();
        AutoRotation.Disable();

        return KillResult.Success;
    }

    internal static Task<bool> KillTarget(IGameObject mob, CancellationToken token = default) => KillTargetInternal(null, mob, false, false, 0, token: token);

    private static async Task<bool> KillTargetInternal(
            HuntMark?         huntMark,
            IGameObject       mob,
            bool              isCountedHuntMark,
            bool              isHuntLog,
            int               openKills,
            bool              logKill = false,
            CancellationToken token   = default)
    {
        token.ThrowIfCancellationRequested();
        var       mobName = mob.Name.GetText();
        using var scope   = new TaskDescriptionScope($"Killing Mob: {mobName}");

        if (Player.DistanceTo(mob.Position) >= C.MinMountDistance)
            await Mount(token);
        await MoveToMovingObject(mob, recheckPosition: true, token: token);
        await Dismount(token);
        Svc.Targets.Target = mob;
        Verbose($"Targeted Hunt Mark: {mobName} ({mob.Position})");

        unsafe
        {
            if (mob.Struct()->FateId > 0 && FateManager.Instance()->CurrentFate != null && !PlayerState.Instance()->IsLevelSynced)
            {
                if (Player.Level > FateManager.Instance()->CurrentFate->MaxLevel)
                    Chat.SendMessage("/lsync");
            }
        }

        if (isCountedHuntMark && huntMark != null)
        {
            // TODO: There is still a bug that if you have an old hunt bill (maybe only elite) and only finish that one,
            // that it won't register the kill as the game will just update internally to the new bill... and there the mark kills will be at 0 again... (╯°□°)╯︵ ┻━┻
            await WaitUntilAsync(() => openKills !=
                                       (isHuntLog
                                                ? huntMark.GetOpenMonsterNoteKills
                                                : huntMark.GetOpenMobHuntKills) ||
                                       Svc.Condition[ConditionFlag.Unconscious], "Wait for registered kill or unconscious", token);
        }
        else
            await WaitUntilAsync(() => mob.IsDead || Svc.Condition[ConditionFlag.Unconscious], "Wait for kill or unconscious", token);

        if (Svc.Condition[ConditionFlag.Unconscious])
        {
            FullWarning("Player died!");
            await WaitUntilAsync(() => RegexYesNo(true, Lang.SelectYesNoReturnTo), "Waiting for resurrection yesno", token);
            await WaitWhileAsync(() => Svc.Condition[ConditionFlag.Unconscious], "Waiting for resurrection", token);
            return false;
        }

        if (logKill) ChatPrintInfo($"Killed mob {mobName}");
        return true;
    }

    internal static async Task HandleHaters(CancellationToken token = default)
    {
        while (GetHaterCount() > 0)
        {
            var hater = Svc.Objects.First(x => x.EntityId == GetHaters()[0].EntityId && !x.IsDead);

            using var scope = new TaskDescriptionScope($"Killing Hater: {hater.Name}");
            if (hater.IsDead) continue;
            Svc.Targets.Target = hater;
            await MoveToMovingObject(hater, recheckPosition: true, token: token);
            await IsTargetDead(hater, token);
            await Task.Delay(GeneralDelayMs * 2, token);
        }
    }

    internal static async Task CheckChocobo(CancellationToken token = default)
    {
        if (InventoryHelper.GetInventoryItemCount(4868) > 0)
        {
            if (C.UseChocoboInFights && GetChocoboTimeLeft <= 300)
            {
                unsafe
                {
                    if (ActionManager.Instance()->GetActionStatus(ActionType.Item, 4868) != 0) return;
                    ActionManager.Instance()->UseAction(ActionType.Item, 4868, extraParam: 65535);
                }

                await WaitUntilAsync(() => !Svc.Condition[ConditionFlag.Casting], "Waiting for chocobo companion", token);
            }
        }
    }
}
