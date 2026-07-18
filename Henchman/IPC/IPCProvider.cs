using Henchman.Features.OnABoat;
using Henchman.Features.OnYourMark;
using Henchman.Tweaks;
using Underlings.TaskManager;

namespace Henchman.IPC;

internal static class IPCProvider
{
    private const string Prefix = "Henchman";

    /*
     * General
     */

    [IPCDescription("Check if a Henchman task is running")]
    public static bool IsBusy() => Running;

    [IPCDescription("Cancel the currently running Henchman task")]
    public static void CancelAllTasks() => TaskRunner.CancelAllTasks();

    /*
     * Features
     */

    [IPCDescription("Start the On A Boat task")]
    public static void StartOnABoat()
    {
        if (TryGetFeature<OnABoatUI>(out var boat)) boat.Feature.RunTask();
    }

    [IPCDescription("Start the On Your Mark task")]
    public static void StartOnYourMark()
    {
        if (TryGetFeature<OnYourMarkUI>(out var mark)) mark.Feature.RunTask();
    }

    /*
     * Tweaks
     */

    [IPCDescription("Toggle rendering")]
    public static void SetRender(bool enabled)
    {
        Rendering.SetRender(enabled);
    }

    [IPCDescription("Toggle unfocused window rendering")]
    public static void UnfocusedRender(bool enabled)
    {
        Rendering.SetDisableRenderWhenUnfocused(enabled);
    }

    [IPCDescription("Force rendering")]
    public static void ForceRender(bool enabled)
    {
        if (enabled)
        {
            Rendering.SetForceRenderEnabled(true);
            FullWarning("You enabled ForceRender. Henchman will overwrite ANY render setting from Plugins using ECommons with what you either set through UI OR IPC 'SetRender'! Don't forget to disable it afterwards!");
        }
        else
            Rendering.SetForceRenderEnabled(false);
    }

    /*
     * Wrath
     */
    public static void WrathComboCallback(int reason, string additionalInfo)
    {
        Log.Information($"Lease was cancelled for reason {reason}. " +
                        $"Additional info: {additionalInfo}");

        if (reason == 0)
        {
            Log.Error("The user cancelled our lease." +
                      "We are suspended from creating a new lease for now.");
        }
    }

    internal static void Init()
    {
        Svc.PluginInterface.GetIpcProvider<bool>($"{Prefix}.{nameof(IsBusy)}")
           .RegisterFunc(IsBusy);
        Svc.PluginInterface.GetIpcProvider<object>($"{Prefix}.{nameof(CancelAllTasks)}")
           .RegisterAction(CancelAllTasks);
        Svc.PluginInterface.GetIpcProvider<object>($"{Prefix}.{nameof(StartOnABoat)}")
           .RegisterAction(StartOnABoat);
        Svc.PluginInterface.GetIpcProvider<object>($"{Prefix}.{nameof(StartOnYourMark)}")
           .RegisterAction(StartOnYourMark);
        Svc.PluginInterface.GetIpcProvider<bool, object>($"{Prefix}.{nameof(SetRender)}")
           .RegisterAction(SetRender);
        Svc.PluginInterface.GetIpcProvider<bool, object>($"{Prefix}.{nameof(ForceRender)}")
           .RegisterAction(ForceRender);
        Svc.PluginInterface.GetIpcProvider<int, string, object>($"{Prefix}.{nameof(WrathComboCallback)}")
           .RegisterAction(WrathComboCallback);
    }

    internal static void Dispose()
    {
        Svc.PluginInterface.GetIpcProvider<bool>($"{Prefix}.{nameof(IsBusy)}")
           .UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<object>($"{Prefix}.{nameof(CancelAllTasks)}")
           .UnregisterAction();
        Svc.PluginInterface.GetIpcProvider<object>($"{Prefix}.{nameof(StartOnABoat)}")
           .UnregisterAction();
        Svc.PluginInterface.GetIpcProvider<object>($"{Prefix}.{nameof(StartOnYourMark)}")
           .UnregisterAction();
        Svc.PluginInterface.GetIpcProvider<bool, object>($"{Prefix}.{nameof(SetRender)}")
           .UnregisterAction();
        Svc.PluginInterface.GetIpcProvider<bool, object>($"{Prefix}.{nameof(ForceRender)}")
           .UnregisterAction();
        Svc.PluginInterface.GetIpcProvider<int, string, object>($"{Prefix}.{nameof(WrathComboCallback)}")
           .UnregisterAction();
    }
}
