using System.Linq;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Henchman.Helpers;

internal static unsafe class EventUtils
{
    internal static float OceanFishingTimeLeft   => EventFramework.Instance()->GetInstanceContentDirector()->ContentDirector.ContentTimeLeft - OceanFishingTimeOffset;
    public static   uint  OceanFishingTimeOffset => EventFramework.Instance()->GetInstanceContentOceanFishing()->TimeOffset;

    internal static bool OpenEventHandler(uint baseId, uint shopId)
    {
        if (Svc.Targets.Target != null && Svc.Targets.Target.BaseId == baseId)
            return OpenEventHandler(TargetSystem.Instance()->Target, shopId);
        var vendor = Svc.Objects.Where(x => x.BaseId == baseId && x.IsTargetable)
                        .OrderBy(x => Player.DistanceTo(x))
                        .FirstOrDefault();

        if (vendor == null)
        {
            FullError($"Failed to find vendor {baseId:X}");
            return false;
        }

        return OpenEventHandler(vendor.Struct(), shopId);
    }

    internal static bool OpenEventHandler(GameObject* eNpc, uint handlerId)
    {
        TargetSystem.Instance()->InteractWithObject(eNpc, false);
        var selector = EventHandlerSelector.Instance();
        if (selector->Target == null)
            return true;

        if (selector->Target != eNpc)
        {
            FullError($"Unexpected selector target {(ulong)selector->Target->GetGameObjectId():X} when trying to interact with {(ulong)eNpc->GetGameObjectId():X}");
            return false;
        }

        for (var i = 0; i < selector->OptionsCount; ++i)
        {
            if (selector->Options[i].Handler->Info.EventId.Id == handlerId)
            {
                Log($"Selecting selector option {i} for handler {handlerId:X}");
                EventFramework.Instance()->InteractWithHandlerFromSelector(i);
                return true;
            }
        }

        FullError($"Failed to find handler {handlerId:X} in selector for {(ulong)eNpc->GetGameObjectId():X}");
        return false;
    }
}
