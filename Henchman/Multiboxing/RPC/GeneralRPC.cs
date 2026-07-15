using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Henchman.Multiboxing.Command;

namespace Henchman.Multiboxing.RPC;

[CommandGroup]
internal static class GeneralRPC
{
    [Command]
    internal static bool AcceptInvitation(string inviterName)
    {
        unsafe
        {
            var partyInvite = InfoProxyPartyInvite.Instance();
            if (partyInvite->InviterName.EqualToString(inviterName))
            {
                InfoProxyPartyInvite.Instance()->RespondToInvitation(InfoProxyPartyInvite.Instance()->InviterName.StringPtr, true);
                return true;
            }

            InfoProxyPartyInvite.Instance()->RespondToInvitation(InfoProxyPartyInvite.Instance()->InviterName.StringPtr, false);
            return false;
        }
    }

    [Command]
    internal static async Task HandlePartyTeleport(uint targetAetheryte, uint targetDestination, Vector3 targetPosition, CancellationToken token = default)
    {
        bool activeRequest;
        uint selectedAetheryteId;
        unsafe
        {
            activeRequest       = Telepo.Instance()->ActiveTeleportRequest;
            selectedAetheryteId = AgentTeleport.Instance()->PendingAetheryteId;
        }

        if (activeRequest && selectedAetheryteId == targetAetheryte)
            GameMain.ExecuteCommand(203);
        else
            await HandleTeleportDetour(targetAetheryte, targetDestination, targetPosition, token);
    }
}
