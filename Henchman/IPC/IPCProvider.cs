using ECommons.EzIpcManager;
using Henchman.Features.OnABoat;
using Henchman.Features.OnYourMark;
using Henchman.Tweaks;
using System.Linq;
using System.Reflection;

namespace Henchman.IPC;

internal static class IPCProvider
{
    /*
     * General
     */

    [EzIPC]
    [IPCDescription("Check if a Henchman task is running")]
    public static bool IsBusy() => Running;

    [EzIPC]
    [IPCDescription("Cancel the currently running Henchman task")]
    public static void CancelAllTasks() => TaskManager.TaskManager.CancelAllTasks();

    /*
     * Features
     */
    [EzIPC]
    [IPCDescription("Start the On A Boat task")]
    public static void StartOnABoat()
    {
        if (TryGetFeature<OnABoatUI>(out var boat)) boat.Feature.RunTask();
    }

    [EzIPC]
    [IPCDescription("Start the On Your Mark task")]
    public static void StartOnYourMark()
    {
        if (TryGetFeature<OnYourMarkUI>(out var mark)) mark.Feature.RunTask();
    }

    /*
     * Tweaks
     */

    [EzIPC]
    [IPCDescription("Toggle Rendering")]
    public static void SetRender(bool enabled)
    {
        GeneralTweaks.ActiveRenderFlag = enabled
                                                   ? (byte)0
                                                   : (byte)1;
    }


    [EzIPC]
    [IPCDescription("Force Rendering")]
    public static void ForceRender(bool enabled)
    {
        if (enabled)
        {
            GeneralTweaks.RenderDisableProcessed ??= Svc.PluginInterface.GetOrCreateData<uint[]>(
                                                                                   "ECommons.RenderDisableProcessingFramecount",
                                                                                   () => [0]
                                                                                  );

            Svc.Framework.Update += GeneralTweaks.ForceRender;
            FullWarning("You enabled ForceRender. Henchman will overwrite ANY render setting from Plugins using ECommons with what you either set through UI OR IPC 'SetRender'! Don't forget to disable it afterwards!");
        }
        else
        {
            Svc.Framework.Update -= GeneralTweaks.ForceRender;
            GeneralTweaks.ActiveRenderFlag = 0;
        }
    }

    /*
     * Wrath
     */
    [EzIPC]
    public static void WrathComboCallback(int reason, string additionalInfo)
    {
        Log($"Lease was cancelled for reason {reason}. " +
            $"Additional info: {additionalInfo}");

        if (reason == 0)
        {
            InternalError("The user cancelled our lease." +
                          "We are suspended from creating a new lease for now.");
        }
    }

    public static List<IpcEntry> BuildIpcList(Type staticClass)
    {
        var list = new List<IpcEntry>();

        var methods = staticClass
                     .GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m =>
                                    m.GetCustomAttribute<EzIPCAttribute>() != null &&
                                    m.GetCustomAttribute<IPCDescriptionAttribute>() != null);

        foreach (var method in methods)
        {
            var desc = method.GetCustomAttribute<IPCDescriptionAttribute>()!.Text;

            var returnType = method.ReturnType.Name;

            var parameters = string.Join(", ",
                                         method.GetParameters()
                                               .Select(p => $"{p.ParameterType.Name} {p.Name}"));

            var signature = parameters.Length == 0
                                    ? $"{method.Name}()"
                                    : $"{method.Name}({parameters})";

            list.Add(new IpcEntry(returnType, signature, desc));
        }

        return list;
    }

    public record IpcEntry(string ReturnType, string Signature, string Description);
}
