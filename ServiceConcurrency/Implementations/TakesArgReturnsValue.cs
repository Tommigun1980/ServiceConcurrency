using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ServiceConcurrency
{
#pragma warning disable CS1591
    /// <summary>
    /// For a given argument, only one call will be made and subsequent calls will
    /// fetch the value from cache. Also prevents any concurrent calls from occurring,
    /// by allowing only one active call at a time per argument, where concurrent
    /// calls will wait for the active call to finish.
    /// </summary>
    /// <remarks>
    /// If a cached value exists for a specific argument, it will be returned from cache.
    /// If not, concurrent calls won't invoke the fetch callback multiple times -
    /// only the first call will invoke it, and the rest will wait until it finishes.
    /// When it does, all concurrent calls will return that value.
    ///
    /// So simultaneously calling with "A", "A", "B" will result in one call for
    /// "A" and one for "B". The next time "A" or "B" is called with, a value for
    /// it will be fetched from the cache and no request will be made.
    /// </remarks>
    public sealed class TakesArgReturnsValue<TArg, TValue> : TakesArgReturnsValue<TArg, TValue, TValue>
    {
        public TakesArgReturnsValue(ValueChangedAction onValueChanged = null) : base(onValueChanged)
        {
        }
        public TakesArgReturnsValue(IMemoryCache cache, ValueChangedAction onValueChanged = null) : base(cache, onValueChanged)
        {
        }
    }

    public class TakesArgReturnsValue<TArg, TSourceValue, TValue> : BaseMemoryCache<TArg, TValue>
    {
        private readonly Dictionary<TArg, Task<TSourceValue>> runningTasksMap = new Dictionary<TArg, Task<TSourceValue>>();

        public TakesArgReturnsValue(ValueChangedAction onValueChanged = null) : base(onValueChanged)
        {
        }
        public TakesArgReturnsValue(IMemoryCache cache, ValueChangedAction onValueChanged = null) : base(cache, onValueChanged)
        {
        }

        public async Task<ResultAndStateInfo<TValue>> ExecuteAndGetExecutorInfo(
            Func<TArg, Task<TSourceValue>> taskGetter,
            TArg requestArg,
            Func<TSourceValue, TValue> valueConverter = null,
            bool bypassCacheRead = false)
        {
            TValue value;
            if (!bypassCacheRead && this.TryGetValue(requestArg, out value))
                return new ResultAndStateInfo<TValue>(false, value);

            Task<TSourceValue> taskInFlightFetchingRelevantArg;
            if (this.runningTasksMap.TryGetValue(requestArg, out taskInFlightFetchingRelevantArg))
                return new ResultAndStateInfo<TValue>(false, ConvertValue.Convert(await taskInFlightFetchingRelevantArg, valueConverter));

            var task = taskGetter(requestArg);
            this.runningTasksMap.Add(requestArg, task);
            try
            {
                var result = await task;
                var convertedResult = ConvertValue.Convert(result, valueConverter);

                this.Set(requestArg, convertedResult);
                return new ResultAndStateInfo<TValue>(true, convertedResult);
            }
            finally
            {
                this.runningTasksMap.Remove(requestArg);
            }
        }

        public async Task<TValue> Execute(
            Func<TArg, Task<TSourceValue>> taskGetter,
            TArg requestArg,
            Func<TSourceValue, TValue> valueConverter = null,
            bool bypassCacheRead = false)
        {
            return (await this.ExecuteAndGetExecutorInfo(taskGetter, requestArg, valueConverter, bypassCacheRead)).Result;
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
