namespace Henchman.TaskManager;

internal sealed class TaskDescriptionScope : IDisposable
{
    private readonly string description;

    public TaskDescriptionScope(string description)
    {
        this.description = description;
        TaskDescription.Add(description);
        PluginLog.Verbose($"[{TaskName}] Entering Task: {description}");
    }

    public void Dispose()
    {
        PluginLog.Verbose($"[{TaskName}] Exiting Task: {description}");
        TaskDescription.Remove(description);
    }
}
