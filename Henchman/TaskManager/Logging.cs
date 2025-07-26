using System.Linq;

namespace Henchman.TaskManager;

internal static class Logging
{
    internal static void Log(string message) => PluginLog.Log($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");

    internal static void Warning(string message)
    {
        ChatPrint($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");
        PluginLog.Warning($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");
    }

    internal static bool WarningIf(bool condition, string message)
    {
        if (condition)
        {
            Warning(message);
            return true;
        }

        return false;
    }

    internal static void ErrorThrow(string message)
    {
        Svc.Chat.PrintError($"[Henchman] [{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}] Plugin stopped! Check the error log!");
        throw new Exception($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}] {message}");
    }

    internal static void ErrorThrowIf(bool condition, string message)
    {
        if (condition)
        {
            ErrorThrow(message);
            CancelAllTasks();
        }
    }

    internal static void Error(string message)
    {
        Svc.Chat.PrintError($"[Henchman] [{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}] Plugin stopped! Check the error log!");
        PluginLog.Error($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}] Plugin stopped! Error Reason: \n {message}");
    }

    internal static void ErrorIf(bool condition, string message)
    {
        if (condition)
        {
            ErrorThrow(message);
        }
    }

    internal static void Verbose(string message) => PluginLog.Verbose($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");

    internal static void Debug(string message) => PluginLog.Debug($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");

    internal static void ChatPrint(string message) => Svc.Chat.Print($"[Henchman] {message}");
}
