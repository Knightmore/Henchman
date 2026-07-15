using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Underlings.GameHelpers;
using InstanceContent = Lumina.Excel.Sheets.InstanceContent;

namespace Henchman.Helpers;

public static class GeneralHelpers
{
    public enum KillResult
    {
        Default,
        Success,
        Died,
        NoSpawns
    }

    public static bool IsPlayerMelee => Svc.Data.GetExcelSheet<ClassJob>()
                                           .GetRow(Player.ClassJob.RowId)
                                           .Role is 1 or 2;

    // Collapsed into the broader Player.IsBusy (no InCombat and TerritoryLoadState).
    // public static bool IsPlayerBusy => IsOccupied() || Player.Object.IsCasting || Player.IsMoving || Player.IsAnimationLocked;
    public static          bool  IsPlayerBusy       => Player.IsBusy;
    internal static unsafe float GetChocoboTimeLeft => UIState.Instance()->Buddy.CompanionInfo.TimeLeft;

    internal static unsafe IEnumerable<PartyMember> PartyMembers => GroupManager.Instance()->MainGroup.PartyMembers.ToArray()
                                                                                                      .Where(x => x.TerritoryType != 0);

    internal static bool IsPlayerInObjectRange3D(IGameObject gameObj,  float distance = 5f) => Player.DistanceTo(gameObj)  < distance;
    internal static bool IsPlayerInObjectRange2D(IGameObject gameObj,  float distance = 5f) => Player.DistanceTo(gameObj)  < distance;
    internal static bool IsPlayerInPositionRange3D(Vector3   position, float distance = 5f) => Player.DistanceTo(position) < distance;
    internal static bool IsPlayerInPositionRange2D(Vector2   position, float distance = 5f) => Player.DistanceTo(position) < distance;

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
                                             .Where(x => x.ClassJob == classJobId && x.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists));

        var gearset = filtered.Any()
                              ? filtered.MaxBy(x => x.ItemLevel)
                              : (RaptureGearsetModule.GearsetEntry?)null;

        if (gearset != null)
        {
            TaskLog.Verbose($"Highest set is {gearset.Value.Id + 1} {gearset.Value.NameString}");
            gearsetModule->EquipGearset(gearset.Value.Id);
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
        {
            if (Svc.Targets.Target == null) return;
            await Task.Delay(GeneralDelayMs, token);
        }
    }

    private static float DistanceToHitboxEdge(Vector3 targetPos, float hitboxRadius) => Player.DistanceTo(targetPos) - hitboxRadius;

    internal static bool IsInAttackRange(this IGameObject target, float baseHitboxRadius)
    {
        const float DefaultRangeBuffer = 1.5f;
        const float RangedAdjustment   = 8f;
        const float MaxRange           = 2f;

        var jobRole = Svc.Data.GetExcelSheet<ClassJob>()
                        ?.GetRow(Player.ClassJob.RowId)
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

    internal static unsafe uint GetGrandCompany() => PlayerState.Instance()->GrandCompany;

    internal static int GetRankInfo(bool gcLog) => gcLog
                                                           ? HuntLogHelper.GetGrandCompanyRankInfo()
                                                           : HuntLogHelper.GetClassJobRankInfo();

    public static unsafe int GetGrandCompanyRank()
    {
        var playerState = PlayerState.Instance();
        var playerGC    = playerState->GrandCompany;

        switch (playerGC)
        {
            case 1:
                return playerState->GCRanks[0];
            case 2:
                return playerState->GCRanks[1];
            case 3:
                return playerState->GCRanks[2];
            default:
                return -1;
        }
    }

    internal static unsafe void ChangeBait(int baitId) => ((FishingEventHandler*)EventFramework.Instance()->GetEventHandlerById(0x150001))->ChangeBait(baitId);

    internal static unsafe int GetHaterCount() => UIState.Instance()->Hater.HaterCount;

    internal static unsafe Span<HaterInfo> GetHaters() => UIState.Instance()->Hater.Haters;

    internal static unsafe bool AddonReady(string addonName) => TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon);

    public static Vector2 GetRandomPoint(Vector2 A, Vector2 B)
    {
        var rand = new Random();
        var minX = Math.Min(A.X, B.X);
        var maxX = Math.Max(A.X, B.X);
        var minY = Math.Min(A.Y, B.Y);
        var maxY = Math.Max(A.Y, B.Y);

        var randomX = (float)(minX + (rand.NextDouble() * (maxX - minX)));
        var randomY = (float)(minY + (rand.NextDouble() * (maxY - minY)));

        return new Vector2(randomX, randomY);
    }

    internal static bool IsDutyUnlocked(uint dutyId) => Svc.UnlockState.IsInstanceContentUnlocked(Svc.Data.Excel.GetSheet<InstanceContent>()
                                                                                                     .GetRow(Svc.Data.Excel.GetSheet<ContentFinderCondition>()
                                                                                                                .First(x => x.TerritoryType.RowId == dutyId)
                                                                                                                .RowId));

    internal static        bool IsQuestCompleted(uint questId) => QuestManager.IsQuestComplete(questId);
    internal static unsafe bool IsQuestAccepted(uint  questId) => QuestManager.Instance()->IsQuestAccepted(questId);
    internal static unsafe void AbandonQuest(uint     questID) => Journal.Instance()->AbandonQuest(Journal.QuestType.NormalQuest, questID - 65536);

    private static ClassJob ClassRow(uint classId) => Svc.Data.GetExcelSheet<ClassJob>()
                                                         .GetRow(classId);

    internal static bool IsCombat(uint classId) => ClassRow(classId)
                                                  .ClassJobCategory.Value.RowId ==
                                                   30 ||
                                                   ClassRow(classId)
                                                          .ClassJobCategory.Value.RowId ==
                                                   31;

    internal static void CleanupCombatAutomation()
    {
        Bossmod.DisableAI();
        AutoRotation.Disable(C.AutoRotationPlugin);
        ResetCurrentTarget();
    }
}
