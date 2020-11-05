using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ServiceConcurrency
{
#pragma warning disable CS1591
    /// <summary>
    /// Only one call will be made and subsequent calls will fetch the value from
    /// cache. Also prevents any concurrent calls from occurring, by allowing only
    /// one active call at a time, where concurrent calls will wait for the active
    /// call to finish.
    /// </summary>
    /// <remarks>
    /// If a cached value exists, the value will be returned from cache. If not,
    /// concurrent calls won't invoke the fetch callback multiple times - only the
    /// first call will invoke it, and the rest will wait until it finishes. When
    /// it does, all concurrent calls will return that value.
    /// </remarks>
    public sealed class ReturnsValue<TValue> : ReturnsValue<TValue, TValue>
    {
        public ReturnsValue(ValueChangedAction onValueChanged = null) : base(onValueChanged)
        {
        }
        public ReturnsValue(IMemoryCache cache, ValueChangedAction onValueChanged = null) : base(cache, onValueChanged)
        {
        }
    }

    public class ReturnsValue<TSourceValue, TValue> : BaseMemoryCache<Guid, TValue>
    {
        public TValue Value
        {
            get
            {
                TValue value;
                if (this.TryGetValue(this.cacheKey, out value))
                    return value;
                return default(TValue);
            }

            set
            {
                this.Set(this.cacheKey, value);
            }
        }

        private readonly Guid cacheKey = Guid.NewGuid();
        private Task<TSourceValue> runningTask;

        public ReturnsValue(ValueChangedAction onValueChanged = null) : base(onValueChanged)
        {
        }
        public ReturnsValue(IMemoryCache cache, ValueChangedAction onValueChanged = null) : base(cache, onValueChanged)
        {
        }

        public async Task<ResultAndStateInfo<TValue>> ExecuteAndGetExecutorInfo(
            Func<Task<TSourceValue>> taskGetter,
            Func<TSourceValue, TValue> valueConverter = null,
            bool bypassCacheRead = false)
        {
            TValue value;
            if (!bypassCacheRead && this.TryGetValue(this.cacheKey, out value))
                return new ResultAndStateInfo<TValue>(false, value);

            if (this.runningTask != null)
                return new ResultAndStateInfo<TValue>(false, ConvertValue.Convert(await this.runningTask, valueConverter));

            this.runningTask = taskGetter();
            try
            {
                var result = await this.runningTask;
                var convertedResult = ConvertValue.Convert(result, valueConverter);

                this.Set(this.cacheKey, convertedResult);
                return new ResultAndStateInfo<TValue>(true, convertedResult);
            }
            finally
            {
                this.runningTask = null;
            }
        }

        public async Task<TValue> Execute(
            Func<Task<TSourceValue>> taskGetter,
            Func<TSourceValue, TValue> valueConverter = null,
            bool bypassCacheRead = false)
        {
            return (await this.ExecuteAndGetExecutorInfo(taskGetter, valueConverter, bypassCacheRead)).Result;
        }

        public void Reset()
        {
            this.runningTask = null;
            this.ResetCache();
        }

        public bool IsExecuting()
        {
            return this.runningTask != null;
        }
    }
#pragma warning restore CS1591
}
