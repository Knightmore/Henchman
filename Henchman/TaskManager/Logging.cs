using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace Henchman.TaskManager;

internal static class Logging
{
    private static  string Prefix                  => $"[{TaskName}] {(TaskDescription.Count > 0 ? "[" + string.Join(" -> ", TaskDescription) + "]" : "")}";
    internal static void   TaskLog(string message) => PluginLog.Log($"{Prefix} {message}");

    internal static void Log(string message) => PluginLog.Log($"{message}");

    internal static void Info(string message)
    {
        PluginLog.Log($"{Prefix} {message}");
        ChatPrintInfo(message);
    }

    internal static void FullWarning(string message)
    {
        ChatPrintWarning($"{message}");
        PluginLog.Warning($"{Prefix}  {message}");
    }

    internal static void InternalTaskWarning(string message)
    {
        PluginLog.Warning($"{Prefix} {message}");
    }

    internal static void InternalWarning(string message)
    {
        PluginLog.Warning($"{message}");
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
        Svc.Chat.PrintError($"[Henchman] [{TaskName}] Plugin stopped! Check the error log!");
        throw new PluginErrorException($"{Prefix} {message}");
    }

    internal static void ErrorThrowIf(bool condition, string message)
    {
        if (condition)
        {
            ErrorThrow(message);
            CancelAllTasks();
        }
    }

    internal static void InternalTaskError(string message)
    {
        PluginLog.Error($"{Prefix} Error: \n {message}");
    }

    internal static void InternalError(string message)
    {
        PluginLog.Error($"{message}");
    }

    internal static void FullError(string message)
    {
        Svc.Chat.PrintError("[Henchman] Error - Check the logs!");
        PluginLog.Error($"{Prefix} Error: \n {message}");
    }

    internal static void ErrorIf(bool condition, string message)
    {
        if (condition) FullError(message);
    }

    internal static void Verbose(string message) => PluginLog.Verbose($"{Prefix} {message}");

    internal static void VerboseSpecific(string sender, string message) => PluginLog.Verbose($"[{sender}] {message}");

    internal static void Debug(string message) => PluginLog.Debug($"{Prefix} {message}");

    internal static void ChatPrintRegular(string message) => Svc.Chat.Print($"[Henchman] {message}");

    internal static void ChatPrintInfo(string message)
    {
        var chatEntry = new XivChatEntry
                        {
                                Type = XivChatType.Echo,
                                Message = new SeStringBuilder().AddUiForeground($"[Henchman] {message}", 561)
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
                                Message = new SeStringBuilder().AddUiForeground($"[Henchman] {message}", 544)
                                                               .AddUiForegroundOff()
                                                               .Build()
                        };
        Svc.Chat.Print(chatEntry);
    }

    public class PluginErrorException(string message) : Exception(message);
}
