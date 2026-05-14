using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Data;
using Henchman.Extensions;
using Henchman.Helpers;
using Henchman.Models;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Henchman.Tasks;

internal static class CombatTasks
{
    public static async Task ProcessHuntMarks(List<HuntMark> huntMarks, bool huntLog = false, int currentRank = 0, bool gcLog = false, CancellationToken token = default)
    {
        Debug("Process Hunt Marks");
        foreach (var mark in huntMarks)
        {
            token.ThrowIfCancellationRequested();

            if (mark.IsDuty) continue;
            if (C.SkipFateMarks && mark.FateId > 0) continue;
            Debug($"Needed Kills: {(huntLog ? mark.GetOpenMonsterNoteKills : mark.GetOpenMobHuntKills)}");

            var retries = 0;
            KillResult? killResult = KillResult.Default;
            var gotKilledWhileDetour = false;
            mark.IsCurrentTarget = true;
            while (retries < 3)
            {
                Debug($"Try: {retries}");
                if (Player.Territory.RowId != mark.TerritoryId)
                {
                    if (!mark.Positions.TryGetFirst(out var markPosition))
                    {
                        FullError($"HuntMark {mark.Name} has no valid position!");
                        break;
                    }

                    var closestAetheryte = GetAetheryte(mark.TerritoryId, markPosition);
                    await HandleTeleportDetour(closestAetheryte, mark.TerritoryId, markPosition, token);
                }

                await CheckChocobo(token);

                if (mark.FateId == 0)
                {
                    Verbose($"{mark.MobHuntRowId} | {mark.MobHuntSubRowId} | {mark.GetMobHuntOrderRow.MobHuntReward.RowId}");
                    if (huntLog || (!huntLog && mark.GetMobHuntOrderRow.MobHuntReward.RowId % 4 != 3))
                    {
                        if (mark.Positions.Count > 0)
                        {
                            var distanceOrderedPositions = SortListByDistance([.. mark.Positions]);

                            for (var i = 0; ;)
                            {
                                var markPosition = distanceOrderedPositions[i];
                                Debug($"Trying position {i + 1}/{distanceOrderedPositions.Count} for {mark.Name}");

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
                                else if (Player.Territory.RowId == 138 && !Player.CanFly)
                                {
                                    if (markPosition.ToVector2()
                                                    .IsWithinRadius(new Vector2(-300f, 600f), 300f))
                                    {
                                        await MoveTo(new Vector3(-317f, -36.2f, 351f), true, token);
                                        await UseFerry(1003584, Lang.SelectStringWarpIsleOfUmbra, Lang.SelectYesnoPassageToIsleOfUmbra, "Isle of Umbra", token);
                                    }
                                }
                                else if (Player.Territory.RowId == 137 && !Player.CanFly)
                                {
                                    if (markPosition.X < 200f || markPosition.Z < 57f)
                                    {
                                        if (!IsAetheryteUnlocked(12) && Player.Position.ToVector2() is { X: > 200f, Y: > 57f })
                                        {
                                            await MoveTo(new Vector3(346f, 33f, 93f), true, token);
                                            await UseFerry(1003588, null, Lang.SelectYesnoPassageToRaincatcherGully, "Raincatcher Gully", token);
                                        }
                                    }
                                    else
                                    {
                                        if (!IsAetheryteUnlocked(11) && (Player.Position.ToVector2() is { X: < 200f } || Player.Position.ToVector2() is { Y: < 57f }))

                                        {
                                            await MoveTo(new Vector3(22, 34f, 225f), true, token);
                                            await UseFerry(1003589, null, Lang.SelectYesnoPassageToHiddenFalls, "Hidden Falls", token);
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

    public static async Task ProcessDutyMarks(List<HuntMark> huntMarks, CancellationToken token = default)
    {
        Debug("Process Duty Marks");
        token.ThrowIfCancellationRequested();
        var duties = huntMarks.Select(h => h!.TerritoryId)
                              .Distinct()
                              .ToList();
        foreach (var duty in duties)
        {
            var ADPathAvailable = AutoDuty.ContentHasPath(duty);
            var dutyUnlocked = UIState.IsInstanceContentUnlocked(duty);

            if (dutyUnlocked && ADPathAvailable)
            {
                if (C.SoloUnsyncLogDuty)
                    AutoDuty.RunDutyUsync(duty);
                else
                    AutoDuty.RunDutySupport(duty);
                await WaitUntilAsync(AutoDuty.IsStopped, "Waiting for Duty to finish", token);
            }
            else if (!dutyUnlocked && ADPathAvailable)
            {
                if (!SubscriptionManager.IsInitialized(IPCNames.Questionable)) FullError($"Questionable not enabled, can't do unlock quest! Skipping duty {duty}!");
                else
                {
                    await UnlockDuty(duty, token);

                    if (C.SoloUnsyncLogDuty)
                        AutoDuty.RunDutyUsync(duty);
                    else
                        AutoDuty.RunDutySupport(duty);
                    await WaitUntilAsync(AutoDuty.IsStopped, "Waiting for Duty to finish", token);
                }

                huntMarks.ForEach(x => x.IsCurrentTarget = false);
            }
            else
                FullWarning($"There is no AutoDuty Path for Duty {Svc.Data.Excel.GetSheet<TerritoryType>().GetRow(duty).PlaceName.Value.Name.ExtractText()}");
        }
    }

    private static async Task<bool> UnlockDuty(uint dutyId, CancellationToken token = default)
    {
        if (!SubscriptionManager.IsInitialized(IPCNames.Questionable))
        {
            FullError("Questionable not enabled, can't do unlock quest!");
            return false;
        }

        switch (dutyId)
        {
            case 1245 when !IsDutyUnlocked(1245): // Halatali
                await Questionable.CompleteQuest(66233, token);
                break;
            case 1267 when !IsDutyUnlocked(1267): // Qarn
                await Questionable.CompleteQuest(66300, token);
                break;
            case 1303 when !IsDutyUnlocked(1303): // Cutter's Cry
                await Questionable.CompleteQuest(66457, token);
                break;
            case 1330 when !IsDutyUnlocked(1330): // Dzemael
                if (IsQuestAccepted(66515))
                    AbandonQuest(66515);
                await Questionable.CompleteQuest(66515, token);
                break;
            case 1331 when !IsDutyUnlocked(1331): // Aurum Vale
                if (IsQuestAccepted(66550))
                    AbandonQuest(66550);
                await Questionable.CompleteQuest(66457, token);
                break;
        }

        return true;
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
                       : huntMark.GetOpenMobHuntKills > 0)
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

            Debug($"Mobs to kill left: {(huntLog ? huntMark.GetOpenMonsterNoteKills : huntMark.GetOpenMobHuntKills)}");
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
            HuntMark? huntMark,
            IGameObject mob,
            bool isCountedHuntMark,
            bool isHuntLog,
            int openKills,
            bool logKill = false,
            CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var mobName = mob.Name.GetText();
        using var scope = new TaskDescriptionScope($"Killing Mob: {mobName}");

        if (Player.DistanceTo(mob.Position) >= C.MinMountDistance)
            await Mount(token);
        await MoveToMovingObject(mob, recheckPosition: true, token: token);
        await Dismount(token);
        Svc.Targets.Target = mob;
        Debug($"Targeted Hunt Mark: {mobName} ({mob.Position})");

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
        bool isGrouped;
        unsafe
        {
            isGrouped = GroupManager.Instance()->MainGroup.MemberCount > 1;
        }

        if (isGrouped)
        {
            PartyMember[] validMembers;
            unsafe
            {
                validMembers = GroupManager.Instance()->MainGroup.PartyMembers.ToArray()
                                                                 .Where(x => x.TerritoryType != 0)
                                                                 .ToArray();
            }

            foreach (var member in validMembers)
            {
                var memberHaters = Svc.Objects.Where(x => x.TargetObject != null && x.TargetObject!.GetContentId() == member.ContentId)
                                      .ToList();
                foreach (var hater in memberHaters)
                {
                    if (hater.IsDead) continue;
                    Svc.Targets.Target = hater;
                    await MoveToMovingObject(hater, recheckPosition: true, token: token);
                    await IsTargetDead(hater, token);
                }
            }
        }

        while (GetHaterCount() > 0)
        {
            var hater = Svc.Objects.FirstOrDefault(x => x.EntityId == GetHaters()[0].EntityId && !x.IsDead);

            if (hater == null) break;

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
