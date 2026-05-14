using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommons.GameHelpers;
using Henchman.Extensions;
using Henchman.Multiboxing.Command;
using Henchman.TaskManager;

namespace Henchman.Multiboxing.RPC;

[CommandGroup]
internal static class MovementRPC
{
    [Command]
    internal static async Task<bool> GoToPlayer(uint territoryId, Vector3 position, string world, ulong CID, CancellationToken token = default)
    {
        if (token.IsCancellationRequested) return false;
        using var scope = new TaskDescriptionScope($"RPC: GoTo ({territoryId} | {position})");

        if (Player.CurrentWorldName != world)
        {
            if (Lifestream.ChangeWorld(world))
                await WaitPulseConditionAsync(() => Lifestream.IsBusy(), "Waiting for World change", token);
            else
                return false;
        }

        if (Player.Territory.RowId != territoryId)
        {
            var aetheryteId = GetAetheryte(territoryId, position);
            if (aetheryteId == 0 || !IsAetheryteUnlocked(aetheryteId)) return false;

            await TeleportTo(aetheryteId, token);
        }

        await MoveCloseToPlayer(position, CID, token);
        return true;
    }

    [Command]
    internal static bool RidePillion(ulong contentId, CancellationToken token = default)
    {
        if (Player.Mounted) return true;
        if (Svc.Party.FirstOrDefault(o => o?.ContentId == contentId && o?.EntityId != Player.Object?.GameObjectId && o?.GameObject?.YalmDistanceX < 3 && o.GameObject.CanRidePillion(), null) is { GameObject: { } target }) target.RidePillion();

        return false;
    }
}
