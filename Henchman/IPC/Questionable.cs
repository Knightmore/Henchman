using System.Threading;
using System.Threading.Tasks;
using ECommons.EzIpcManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using Henchman.Helpers;
using Lumina.Excel.Sheets;

namespace Henchman.IPC;

[IPC(IPCNames.Questionable)]
public static class Questionable
{
    [EzIPC]
    public static Func<bool> IsRunning;

    [EzIPC]
    public static Func<string, bool> StartQuest;

    [EzIPC]
    public static Func<string, bool> StartSingleQuest;

    public static async Task CompleteQuest(uint questId, CancellationToken token = default)
    {
        ErrorThrowIf(!SubscriptionManager.IsInitialized(IPCNames.Questionable), "Questionable not enabled!");
        TextAdvanceManager.SetTemporary();
        StartSingleQuest((questId - 65536).ToString());
        await WaitUntilAsync(() => QuestManager.IsQuestComplete(questId), $"Waiting for Quest '{Svc.Data.GetExcelSheet<Quest>().GetRow(questId).Name.GetText()}' to finish.", token);
        TextAdvanceManager.UnsetTemporary();
    }

    public static async Task GetAndProgressQuest(uint questId, CancellationToken token = default)
    {
        ErrorThrowIf(!SubscriptionManager.IsInitialized(IPCNames.Questionable), "Questionable not enabled!");
        TextAdvanceManager.SetTemporary();
        StartSingleQuest((questId - 65536).ToString());
        await WaitUntilAsync(() => !IsRunning(), $"Waiting for Quest '{Svc.Data.GetExcelSheet<Quest>().GetRow(questId).Name.GetText()}' to progress.", token);
        TextAdvanceManager.UnsetTemporary();
    }
}
