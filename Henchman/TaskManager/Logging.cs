using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace Henchman.TaskManager;

internal static class Logging
{
    internal static void Log(string message) => PluginLog.Log($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");

    internal static void Info(string message)
    {
        PluginLog.Log($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");
        ChatPrintInfo(message);
    }

    internal static void FullWarning(string message)
    {
        ChatPrintWarning($"[{TaskName}] [{(TaskDescription.Count  == 0 ? "No Description" : TaskDescription.Last())}]  {message}");
        PluginLog.Warning($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");
    }

    internal static void InternalWarning(string message)
    {
        PluginLog.Warning($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");
    }

    internal static bool WarningIf(bool condition, string message)
    {
        if (condition)
        {
            FullWarning(message);
            return true;
        }

        return false;
    }

    internal static void ErrorThrow(string message)
    {
        Svc.Chat.PrintError($"[Henchman] [{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}] Plugin stopped! Check the error log!");
        throw new PluginErrorException($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}] {message}");
    }

    internal static void ErrorThrowIf(bool condition, string message)
    {
        if (condition)
        {
            ErrorThrow(message);
            CancelAllTasks();
        }
    }

    internal static void InternalError(string message)
    {
        PluginLog.Error($"[{TaskName}] [{(TaskDescription.Count                == 0 ? "No Description" : TaskDescription.Last())}] Error: \n {message}");
    }

    internal static void FullError(string message)
    {
        Svc.Chat.PrintError($"[Henchman] [{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}] Error - Check the logs!");
        PluginLog.Error($"[{TaskName}] [{(TaskDescription.Count                == 0 ? "No Description" : TaskDescription.Last())}] Error: \n {message}");
    }

    internal static void ErrorIf(bool condition, string message)
    {
        if (condition) FullError(message);
    }

    internal static void Verbose(string message) => PluginLog.Verbose($"[{TaskName}]  {(TaskDescription.Count > 0 ? "[" + TaskDescription.Last() + "]" : "")} {message}");

    internal static void VerboseSpecific(string sender, string message) => PluginLog.Verbose($"[{sender}] {message}");

    internal static void Debug(string message) => PluginLog.Debug($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");

    internal static void ChatPrintRegular(string message) => Svc.Chat.Print($"[Henchman] {message}");

    internal static void ChatPrintInfo(string message)
    {
        var chatEntry = new XivChatEntry
                        {
                                Type = XivChatType.Echo,
                                Message = new SeStringBuilder().AddUiForeground($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}", 561)
                                                               .AddUiForegroundOff()
                                                               .Build()
                        };
        Svc.Chat.Print(chatEntry);
    }

    internal static void ChatPrintWarning(string message)
    {
        var chatEntry = new XivChatEntry
                        {
                                Type = XivChatType.Echo,
                                Message = new SeStringBuilder().AddUiForeground($"[{TaskName}] [{(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}", 544)
                                                               .AddUiForegroundOff()
                                                               .Build()
                        };
        Svc.Chat.Print(chatEntry);
    }

    public class PluginErrorException(string message) : Exception(message);
}
