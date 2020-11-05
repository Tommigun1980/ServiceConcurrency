using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace ServiceConcurrency
{
#pragma warning disable CS1591
    /// <summary>
    /// The first call, and any concurrent calls, will execute only once for a
    /// given argument in the argument collection. Subsequent calls will fetch value
    /// from cache.
    /// </summary>
    /// <remarks>
    /// The argument collection is stripped of values for which a cached value exists
    /// or an operation is alredy in flight.
    ///
    /// So simultaneously calling with ["A", "B", "C"]) and ["B", "C", "D"] will
    /// result in one call with ["A", "B", "C"] and one with ["D"]).
    /// The next time "A", "B", "C" or "D" is called with, it will be stripped from
    /// the collection and a value for it will be fetched from the cache.
    /// </remarks>
    public sealed class TakesEnumerationArgReturnsValue<TArg, TValue> : TakesEnumerationArgReturnsValue<TArg, TValue, TValue>
    {
        public TakesEnumerationArgReturnsValue(ValueChangedAction onValueChanged = null) : base(onValueChanged)
        {
        }
        public TakesEnumerationArgReturnsValue(IMemoryCache cache, ValueChangedAction onValueChanged = null) : base(cache, onValueChanged)
        {
        }
    }

    public class TakesEnumerationArgReturnsValue<TArg, TSourceValue, TValue> : BaseMemoryCache<TArg, TValue>
    {
        private readonly Dictionary<TArg, Task<IEnumerable<TSourceValue>>> runningTasksMap = new Dictionary<TArg, Task<IEnumerable<TSourceValue>>>();

        public TakesEnumerationArgReturnsValue(ValueChangedAction onValueChanged = null) : base(onValueChanged)
        {
        }
        public TakesEnumerationArgReturnsValue(IMemoryCache cache, ValueChangedAction onValueChanged = null) : base(cache, onValueChanged)
        {
        }

        public async Task<EnumerableResultAndStateInfo<TArg, TValue>> ExecuteAndGetExecutorInfo(
            Func<IEnumerable<TArg>, Task<IEnumerable<TSourceValue>>> taskGetter,
            Func<TArg, IEnumerable<TValue>, TValue> valueForArgGetter,
            IEnumerable<TArg> requestArg,
            Func<IEnumerable<TSourceValue>, IEnumerable<TValue>> valueConverter = null,
            bool bypassCacheRead = false)
        {
            requestArg = requestArg.Distinct();

            var argsNotInCache = bypassCacheRead ? requestArg : requestArg.Where(t => !this.ContainsKey(t)).ToList();

            var tasksInFlightForRelevantArgs = this.runningTasksMap.Where(t => argsNotInCache.Contains(t.Key)).ToList();
            var argsRequiringCall = argsNotInCache.Where(t => !tasksInFlightForRelevantArgs.Select(u => u.Key).Contains(t)).ToList();

            IEnumerable<TValue> result;
            if (argsRequiringCall.Any())
            {
                var task = taskGetter(argsRequiringCall);
                try
                {
                    foreach (var arg in argsRequiringCall)
                        this.runningTasksMap.Add(arg, task);

                    result = ConvertValue.Convert(await task, valueConverter);

                    foreach (var arg in argsRequiringCall)
                    {
                        var resultValue = valueForArgGetter(arg, result);
                        this.Set(arg, resultValue);
                    }
                }
                finally
                {
                    foreach (var arg in argsRequiringCall)
                        this.runningTasksMap.Remove(arg);
                }
            }
            else
            {
                result = new List<TValue>();
            }

            if (tasksInFlightForRelevantArgs.Any())
            {
                var otherTasksResults = await Task.WhenAll(tasksInFlightForRelevantArgs.Select(t => t.Value));
                var otherResult = ConvertValue.Convert(otherTasksResults.SelectMany(t => t), valueConverter);
                result = result.Union(otherResult);
            }

            var argsFromCache = requestArg.Where(t => !argsNotInCache.Contains(t));
            if (argsFromCache.Any())
            {
                var fromCache = this.Where(t => argsFromCache.Contains(t.Key)).Select(t => t.Value);
                result = result.Union(fromCache);
            }

            return new EnumerableResultAndStateInfo<TArg, TValue>(argsRequiringCall, result);
        }

        public async Task<IEnumerable<TValue>> Execute(
            Func<IEnumerable<TArg>, Task<IEnumerable<TSourceValue>>> taskGetter,
            Func<TArg, IEnumerable<TValue>, TValue> valueForArgGetter,
            IEnumerable<TArg> requestArg,
            Func<IEnumerable<TSourceValue>, IEnumerable<TValue>> valueConverter = null,
            bool bypassCacheRead = false)
        {
            return (await this.ExecuteAndGetExecutorInfo(taskGetter, valueForArgGetter, requestArg, valueConverter, bypassCacheRead)).Result;
        }

        public void Reset()
        {
            this.runningTasksMap.Clear();
            this.ResetCache();
        }

        public bool IsExecuting(TArg requestArg)
        {
            return this.runningTasksMap.ContainsKey(requestArg);
        }
    }
#pragma warning restore CS1591
}
