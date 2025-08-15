using Dalamud.Game.ClientState.Conditions;
using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Henchman.Helpers;

internal static class Memory
{
    public delegate nint ExecuteCommandDelegate(int command, int a1 = 0, int a2 = 0, int a3 = 0, int a4 = 0);

    public static readonly ExecuteCommandDelegate? ExecuteCommand = EzDelegate.Get<ExecuteCommandDelegate>("E8 ?? ?? ?? ?? 8D 46 0A");

    public static unsafe bool ChangeBait(int baitItemId)
    {
        if (PlayerState.Instance()->FishingBait == baitItemId)
            return true;

        if (InventoryHelper.GetInventoryItemCount((uint)baitItemId) == 0)
        {
            FullError($"Bait Id {baitItemId} not found in inventory.");
            return false;
        }

        if (Svc.Condition[ConditionFlag.Fishing])
        {
            FullWarning("Can't change bait while fishing.");
            return false;
        }

        ExecuteCommand?.Invoke(701, 4, baitItemId);
        return true;
    }
}
