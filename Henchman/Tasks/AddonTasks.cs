using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Henchman.Tasks;

internal class AddonTasks
{
    internal static async Task<bool> TrySelectFirstExplorationVenture(uint retainerClassId)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("RetainerTaskList", out var addon) && IsAddonReady(addon))
            {
                if (IsCombat(retainerClassId))
                {
                    addon->FireCallback(true, 11, 343);
                    return true;
                }

                switch (retainerClassId)
                {
                    // Miner
                    case 16:
                        addon->FireCallback(true, 11, 356);
                        break;
                    // Botanist
                    case 17:
                        addon->FireCallback(true, 11, 369);
                        break;
                    // Fisher
                    case 18:
                        addon->FireCallback(true, 11, 382);
                        break;
                }

                return true;
            }
        }

        await Task.Delay(100);
        return false;
    }
}
