using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace Henchman.TaskManager;

public static class TaskManager
{
    private static readonly Queue<TaskRecord>              taskQueue = new();
    private static readonly Queue<CancellationTokenSource> ctsQueue  = new();
    public static           TaskRecord?                    CurrentTaskRecord;
    private static          CancellationTokenSource        Cts             = new();
    public static           List<string>                   TaskDescription = [];
    private static          Func<CancellationToken, Task>? currentTask;
    private static          TaskRecord?                    ChainedTaskRecord;


    /// <summary>
    ///     General delay is 250 ms
    /// </summary>
    public static readonly int GeneralDelayMs = 250;

    public static bool IsPaused => !ResumeHandle.IsSet;

    public static ManualResetEventSlim ResumeHandle { get; } = new(true);

    public static bool Running => CurrentTaskRecord != null;

    public static string? TaskName => Running
                                              ? CurrentTaskRecord!.Name
                                              : "No Task running";

    public static void Pause()  => ResumeHandle.Reset();
    public static void Resume() => ResumeHandle.Set();

    public static void EnqueueTask(TaskRecord task)
    {
        taskQueue.Enqueue(task);
        ctsQueue.Enqueue(new CancellationTokenSource());
    }

    public static void EnqueueMultiTasks(TaskRecord[] tasks)
    {
        foreach (var task in tasks) taskQueue.Enqueue(task);
    }

    public static void TaskManagerTick(IFramework framework)
    {
        if (ChainedTaskRecord != null && CurrentTaskRecord == null)
            StartTask(ChainedTaskRecord, new CancellationTokenSource());
        else if (CurrentTaskRecord == null && taskQueue.TryDequeue(out var nextTask) && ctsQueue.TryDequeue(out var cts))
        {
            Cts.Dispose();

            StartTask(nextTask, cts);
        }
    }

    public static void Run()
    {
        Svc.Framework.Run(async () =>
                          {
                              try
                              {
                                  if (currentTask != null)
                                  {
                                      P!.StatusWindow.IsOpen = true;
                                      await currentTask.Invoke(Cts.Token)
                                                       .ConfigureAwait(true);
                                      ChatPrintInfo($"{CurrentTaskRecord!.Name} completed!");
                                      if (CurrentTaskRecord is { OnDone: { } })
                                          CurrentTaskRecord.OnDone.Invoke();
                                  }
                              }
                              catch (Exception e)
                              {
                                  if (e is PluginErrorException)
                                  {
                                      FullError(e.Message);
                                      if (CurrentTaskRecord is { OnErrorTask: { } })
                                          await CurrentTaskRecord.OnErrorTask.Invoke();
                                  }
                                  /*else if (e is not OperationCanceledException)
                                  {
                                      FullError($"Unexpected error in task execution: {e}");
                                      InternalError($"""
                                                    StackTrace:
                                                    {e.StackTrace}
                                                    """);
                                  }*/

                                  if (CurrentTaskRecord is { OnDone: { } })
                                      CurrentTaskRecord.OnDone.Invoke();

                                  CancelAllTasks();
                              } finally
                              {
                                  CurrentTaskRecord = null;
                                  currentTask       = null;
                                  TaskDescription.Clear();
                              }
                          }, Cts.Token)
           .ConfigureAwait(true);
    }

    private static void StartTask(TaskRecord task, CancellationTokenSource cts)
    {
        Cts               = cts;
        CurrentTaskRecord = task;
        currentTask       = token => task.Task(token);
        ChainedTaskRecord = task.ChainedTaskRecord;
        Run();
    }

    internal static bool IsTaskEnqueued(string taskName)
    {
        return taskQueue.Any(x => x.Name == taskName);
    }

    public static void CancelAllTasks()
    {
        if (CurrentTaskRecord is { OnAbort: { } })
            CurrentTaskRecord.OnAbort.Invoke();
        foreach (var cts in ctsQueue)
            cts.Cancel();
        Cts.Cancel();
        taskQueue.Clear();
        ctsQueue.Clear();
        Vnavmesh.StopCompletely();
        CurrentTaskRecord = null;
        ChainedTaskRecord = null;
        currentTask       = null;
        TaskDescription.Clear();
    }
}
