using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace ServiceConcurrency
{
#pragma warning disable CS1591
    /// <summary>
    /// Concurrent calls will execute only once for a given argument in the argument
    /// collection.
    /// </summary>
    /// <remarks>
    /// The argument collection is stripped of values for which an operation is already
    /// in flight.
    ///
    /// So simultaneously calling with ["A", "B", "C"] and ["B", "C", "D"] will result
    /// in one call with ["A", "B", "C"] and one with ["D"]).
    /// </remarks>
    public sealed class TakesEnumerationArg<TArg>
    {
        private readonly Dictionary<TArg, Task> runningTasksMap = new Dictionary<TArg, Task>();

        public async Task<EnumerableStateInfo<TArg>> ExecuteAndGetExecutorInfo(
            Func<IEnumerable<TArg>, Task> taskGetter,
            IEnumerable<TArg> requestArg)
        {
            requestArg = requestArg.Distinct();

            var tasksInFlightForRelevantArgs = this.runningTasksMap.Where(t => requestArg.Contains(t.Key)).ToList();
            var argsRequiringCall = requestArg.Where(t => !tasksInFlightForRelevantArgs.Select(u => u.Key).Contains(t)).ToList();
            if (argsRequiringCall.Any())
            {
                var task = taskGetter(argsRequiringCall);
                try
                {
                    foreach (var arg in argsRequiringCall)
                        this.runningTasksMap.Add(arg, task);

                    await task;
                }
                finally
                {
                    foreach (var arg in argsRequiringCall)
                        this.runningTasksMap.Remove(arg);
                }
            }

            if (tasksInFlightForRelevantArgs.Any())
                await Task.WhenAll(tasksInFlightForRelevantArgs.Select(t => t.Value));

            return new EnumerableStateInfo<TArg>(argsRequiringCall);
        }

        public Task Execute(
            Func<IEnumerable<TArg>, Task> taskGetter,
            IEnumerable<TArg> requestArg)
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
