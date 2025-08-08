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

    public static async Task CompleteQuest(string qstString, uint questId, CancellationToken token = default)
    {
        TextAdvanceManager.SetTemporary();
        StartSingleQuest(qstString);
        TextAdvanceManager.UnsetTemporary();
        await WaitUntilAsync(() => QuestManager.IsQuestComplete(questId), $"Waiting for Quest '{Svc.Data.GetExcelSheet<Quest>().GetRow(questId).Name.GetText()}' to finish.", token);
    }
}
