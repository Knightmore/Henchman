using ECommons.EzSharedDataManager;

namespace Henchman.Helpers;

internal static class YesAlreadyManager
{
    private static bool WasChanged;
    internal static bool TemporarilyNeeded;
    private static bool IsBusy => Utils.IsPluginBusy;

    internal static void Tick()
    {
        if (WasChanged)
        {
            if (!IsBusy || TemporarilyNeeded)
            {
                WasChanged = false;
                Unlock();
                PluginLog.Debug("YesAlready unlocked");
            }
        }
        else
        {
            if (IsBusy && !TemporarilyNeeded)
            {
                WasChanged = true;
                Lock();
                PluginLog.Debug("YesAlready locked");
            }
        }
    }

    internal static void Lock()
    {
        if (EzSharedData.TryGet<HashSet<string>>("YesAlready.StopRequests", out var data)) data.Add(P.Name);
    }

    internal static void Unlock()
    {
        if (EzSharedData.TryGet<HashSet<string>>("YesAlready.StopRequests", out var data)) data.Remove(P.Name);
    }

    internal static bool? WaitForYesAlreadyDisabledTask()
    {
        if (EzSharedData.TryGet<HashSet<string>>("YesAlready.StopRequests", out var data)) return data.Contains(P.Name);
        return true;
    }
}
