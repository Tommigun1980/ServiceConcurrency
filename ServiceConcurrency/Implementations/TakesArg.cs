using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServiceConcurrency
{
#pragma warning disable CS1591
    /// <summary>
    /// Prevents concurrent calls when sharing the same argument, by allowing only
    /// one active call at a time per argument, where concurrent calls will wait for
    /// the active call to finish.
    /// </summary>
    /// <remarks>
    /// Concurrent calls won't invoke the callback multiple times for the same
    /// argument - only the first call will invoke it, and the rest will wait
    /// until it finishes.
    /// 
    /// So simultaneously calling with "A", "A", "B" will result in one call for
    /// "A" and one for "B".
    /// </remarks>
    public sealed class TakesArg<TArg>
    {
        private readonly Dictionary<TArg, Task> runningTasksMap = new Dictionary<TArg, Task>();

        public async Task<StateInfo> ExecuteAndGetExecutorInfo(
            Func<TArg, Task> taskGetter,
            TArg requestArg)
        {
            Task runningTask;
            if (this.runningTasksMap.TryGetValue(requestArg, out runningTask))
            {
                await runningTask;
                return new StateInfo(false);
            }

            var task = taskGetter(requestArg);
            this.runningTasksMap.Add(requestArg, task);
            try
            {
                await task;

                return new StateInfo(true);
            }
            finally
            {
                this.runningTasksMap.Remove(requestArg);
            }
        }

        public Task Execute(
            Func<TArg, Task> taskGetter,
            TArg requestArg)
        {
            return this.ExecuteAndGetExecutorInfo(taskGetter, requestArg);
        }

        public void Reset()
        {
            this.runningTasksMap.Clear();
        }

        public bool IsExecuting(TArg requestArg)
        {
            return this.runningTasksMap.ContainsKey(requestArg);
        }
    }
#pragma warning restore CS1591
}
