using System.Threading;
using System.Threading.Tasks;
using Henchman.Helpers;

namespace Henchman.Tasks;

internal class InventoryTasks
{
    internal static async Task DiscardItem(uint itemId, CancellationToken token = default)
    {
        if (!InventoryHelper.Discard(itemId)) return;
        await WaitUntilAsync(() => ProcessYesNo(true, "Discard"), $"Discard ItemId {itemId}", token);
    }
}
