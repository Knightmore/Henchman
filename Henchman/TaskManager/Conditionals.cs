using System.Threading;
using System.Threading.Tasks;

namespace Henchman.TaskManager;

internal static class Conditionals
{
    internal static async Task WaitWhileAsync(Func<bool> asyncCondition, string taskDescription = "", CancellationToken token = default)
    {
        using var scope = new TaskDescriptionScope(taskDescription);
        while (asyncCondition())
        {
            token.ThrowIfCancellationRequested();
            //ResumeHandle.Wait(token);
            await Task.Delay(GeneralDelayMs, token)
                      .ConfigureAwait(true);
        }
    }

    internal static async Task WaitWhileAsync(Func<Task<bool>> asyncCondition, string taskDescription = "", CancellationToken token = default)
    {
        using var scope = new TaskDescriptionScope(taskDescription);
        while (await asyncCondition())
        {
            token.ThrowIfCancellationRequested();
            //ResumeHandle.Wait(token);
            await Task.Delay(GeneralDelayMs, token)
                      .ConfigureAwait(true);
        }
    }

    internal static async Task WaitUntilAsync(Func<bool> condition, string taskDescription = "", CancellationToken token = default) => await WaitWhileAsync(() => !condition(), taskDescription, token);

    internal static async Task WaitUntilAsync(Func<Task<bool>> condition, string taskDescription = "", CancellationToken token = default) => await WaitWhileAsync(async () => !await condition(), taskDescription, token);

    internal static async Task WaitPulseConditionAsync(Func<bool> condition, string taskDescription = "", CancellationToken token = default)
    {
        await WaitUntilAsync(() => condition(), taskDescription, token);
        await WaitWhileAsync(() => condition(), taskDescription, token);
    }

    internal static async Task WaitPulseConditionAsync(Func<Task<bool>> condition, string taskDescription = "", CancellationToken token = default)
    {
        await WaitUntilAsync(() => condition(), taskDescription, token);
        await WaitWhileAsync(() => condition(), taskDescription, token);
    }
}
