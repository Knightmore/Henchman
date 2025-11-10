using System.Threading;
using System.Threading.Tasks;

namespace Henchman.TaskManager;

public sealed class TaskRecord
{
    public TaskRecord(Func<CancellationToken, Task> func, string? name, Func<Task>? onError = null, Action? onDone = null, Action? onAbort = null, TaskRecord? chainedTask = null)
    {
        Task              = token => func(token);
        Name              = name;
        OnErrorTask       = onError;
        OnDone            = onDone;
        OnAbort           = onAbort;
        ChainedTaskRecord = chainedTask;
    }

    public Func<CancellationToken, Task> Task              { get; private set; }
    public string?                       Name              { get; private set; }
    public TaskRecord?                   ChainedTaskRecord { get; private set; }
    public Func<Task>?                   OnErrorTask       { get; private set; }
    public Action?                       OnDone            { get; private set; }
    public Action?                       OnAbort           { get; private set; }
}
