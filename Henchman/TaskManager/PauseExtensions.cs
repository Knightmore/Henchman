using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Henchman.TaskManager
{
    public static class PauseExtensions
    {
        public static async Task PauseAwait(this Task task, CancellationToken token)
        {
            TaskManager.ResumeHandle.Wait(token);
            await task;
        }

        public static async Task<T> PauseAwait<T>(this Task<T> task, CancellationToken token)
        {
            TaskManager.ResumeHandle.Wait(token);
            return await task;
        }

        public static async Task PauseAwaitAll(this IEnumerable<Task> tasks, CancellationToken token)
        {
            TaskManager.ResumeHandle.Wait(token);
            await Task.WhenAll(tasks);
        }

        public static async Task PauseAwaitEach(this IEnumerable<Task> tasks, CancellationToken token)
        {
            foreach (var task in tasks)
            {
                TaskManager.ResumeHandle.Wait(token);
                await task;
            }
        }
    }

}
