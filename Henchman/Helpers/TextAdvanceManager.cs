using ECommons.EzSharedDataManager;

namespace Henchman.Helpers;

internal static class TextAdvanceManager
{
    private static bool WasChanged;
    private static bool TemporarilyNeeded;
    private static bool IsBusy => Utils.IsPluginBusy;

    internal static void Tick()
    {
        if (WasChanged)
        {
            if (!IsBusy || TemporarilyNeeded)
            {
                WasChanged = false;
                UnlockTa();
                PluginLog.Debug("TextAdvance unlocked");
            }
        }
        else
        {
            if (IsBusy && !TemporarilyNeeded)
            {
                WasChanged = true;
                LockTa();
                PluginLog.Debug("TextAdvance locked");
            }
        }
    }

    internal static void LockTa()
    {
        if (EzSharedData.TryGet<HashSet<string>>("TextAdvance.StopRequests", out var data)) data.Add(P.Name);
    }

    internal static void UnlockTa()
    {
        if (EzSharedData.TryGet<HashSet<string>>("TextAdvance.StopRequests", out var data)) data.Remove(P.Name);
    }

    internal static void SetTemporary() => TemporarilyNeeded = true;

    internal static void UnsetTemporary() => TemporarilyNeeded = false;
}
