using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace Henchman.Extensions;

public static class PlayerCharacterExtensions
{
    extension(IPlayerCharacter player)
    {
        public unsafe bool HasPartyMembersPillion(int partyMemberAmount) => player.Character()->Mount.MountedEntityIds[1..]
                                                                                                     .ToArray()
                                                                                                     .Count(x => x != 0) ==
                                                                            partyMemberAmount;
    }
}
