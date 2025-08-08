using System.Threading;
using System.Threading.Tasks;

namespace Henchman.TaskManager;

public sealed class TaskRecord
{
    public TaskRecord(Func<CancellationToken, Task> func, string? name, Func<Task>? onError = null, Action? onAbort = null, TaskRecord? onCompleted = null)
    {
        Task              = token => func(token);
        Name              = name;
        OnErrorTask       = onError;
        OnAbort           = onAbort;
        ChainedTaskRecord = onCompleted;
    }

    public Func<CancellationToken, Task> Task              { get; private set; }
    public string?                       Name              { get; private set; }
    public TaskRecord?                   ChainedTaskRecord { get; private set; }
    public Func<Task>?                   OnErrorTask       { get; private set; }
    public Action?                       OnAbort           { get; private set; }
}
