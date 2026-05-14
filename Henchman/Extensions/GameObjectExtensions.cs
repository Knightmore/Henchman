using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;

namespace Henchman.Extensions;

public static class GameObjectExtensions
{
    extension(IGameObject obj)
    {
        public unsafe Character*   Character()   => (Character*)obj.Address;
        public unsafe BattleChara* BattleChara() => (BattleChara*)obj.Address;

        public unsafe bool CanRidePillion()
        {
            var cont = obj.Character()->Mount;
            return cont.MountedEntityIds[1..]
                       .ToArray()
                       .Count(x => x != 0) <
                   Svc.Data.GetExcelSheet<Mount>()
                      .GetRow(cont.MountId)
                      .ExtraSeats;
        }

        public unsafe bool RidePillion(uint seatIndex = 10)
        {
            if (obj.CanRidePillion())
            {
                obj.BattleChara()->RidePillion(seatIndex);
                return true;
            }

            return false;
        }

        public unsafe ulong GetContentId() => obj.Character()->ContentId;
    }
}
