using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace ServiceConcurrency
{
#pragma warning disable CS1591
    /// <summary>
    /// A library for handling concurrency and state, primarily for code that
    /// calls out to web services.
    ///
    /// First, it prevents unnecessary calls from happening.When a call with
    /// matching arguments is already in flight, the concurrent caller is parked
    /// and will resume when the originating request finishes.If the argument
    /// is a collection, entities already in flight are stripped.
    /// 
    /// Second, it caches the state of value returning requests. When a cached
    /// value exists for any given request, it will be returned instead of a
    /// call being made. This value can be accessed at any time, preventing a
    /// need for additional backing fields in your code. If the argument
    /// is a collection, cached entities are stripped.
    /// 
    /// See https://github.com/Tommigun1980/ServiceConcurrency for examples and
    /// documentation.
    /// </summary>
    internal class NamespaceDoc
    {
    }

    internal static class ConvertValue
    {
        public static TValue Convert<TSourceValue, TValue>(object value, Func<TSourceValue, TValue> valueConverter = null)
        {
            return valueConverter != null ? valueConverter((TSourceValue)value) : DefaultConvert<TValue>(value);
        }

        private static TValue DefaultConvert<TValue>(object value)
        {
            return (TValue)value;
        }
    }

    /// <summary>
    /// Prevents concurrent calls by allowing only one active call at a time,
    /// where concurrent calls will wait for the active call to finish.
    /// </summary>
    public sealed class NoArgNoValue
    {
        private Task runningTask;

        public async Task Execute(
            Func<Task> taskGetter)
        {
            if (this.runningTask == null)
            {
                this.runningTask = taskGetter();
                try
                {
                    await this.runningTask;
                }
                finally
                {
                    this.runningTask = null;
                }
            }
            else
            {
                await this.runningTask;
            }
        }

        public void Reset()
        {
            this.runningTask = null;
        }

        public bool IsExecuting()
        {
            return this.runningTask != null;
        }
    }

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
    }

    public class ReturnsValue<TSourceValue, TValue>
    {
        public TValue Value { get; set; }
        public bool EnableCache { get; set; } = true;

        private bool hasValue;
        private Task<TSourceValue> runningTask;

        public async Task<TValue> Execute(
            Func<Task<TSourceValue>> taskGetter,
            Func<TSourceValue, TValue> valueConverter = null,
            bool bypassCache = false)
        {
            if (this.EnableCache && !bypassCache && this.hasValue)
                return this.Value;

            if (this.runningTask != null)
                return ConvertValue.Convert(await this.runningTask, valueConverter);

            this.runningTask = taskGetter();
            try
            {
                var result = await this.runningTask;

                var convertedResult = ConvertValue.Convert(result, valueConverter);

                if (this.EnableCache)
                {
                    this.Value = convertedResult;
                    this.hasValue = true;
                }

                return convertedResult;
            }
            finally
            {
                this.runningTask = null;
            }
        }

        public void Reset()
        {
            this.runningTask = null;
            this.ResetCache();
        }

        public void ResetCache()
        {
            this.Value = default(TValue);
            this.hasValue = false;
        }

        public bool IsExecuting()
        {
            return this.runningTask != null;
        }
    }

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

        public async Task Execute(
            Func<TArg, Task> taskGetter,
            TArg requestArg)
        {
            Task runningTask;
            if (this.runningTasksMap.TryGetValue(requestArg, out runningTask))
            {
                await runningTask;
                return;
            }

            var task = taskGetter(requestArg);
            this.runningTasksMap.Add(requestArg, task);
            try
            {
                await task;
            }
            finally
            {
                this.runningTasksMap.Remove(requestArg);
            }
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
    }

    public class TakesArgReturnsValue<TArg, TSourceValue, TValue>
    {
        public Dictionary<TArg, TValue> ValueMap { get; } = new Dictionary<TArg, TValue>();
        public bool EnableCache { get; set; } = true;

        private readonly Dictionary<TArg, Task<TSourceValue>> runningTasksMap = new Dictionary<TArg, Task<TSourceValue>>();

        public async Task<TValue> Execute(
            Func<TArg, Task<TSourceValue>> taskGetter,
            TArg requestArg,
            Func<TSourceValue, TValue> valueConverter = null,
            bool bypassCache = false)
        {
            TValue value;
            if (this.EnableCache && !bypassCache && this.ValueMap.TryGetValue(requestArg, out value))
                return value;

            Task<TSourceValue> taskInFlightFetchingRelevantArg;
            if (this.runningTasksMap.TryGetValue(requestArg, out taskInFlightFetchingRelevantArg))
                return ConvertValue.Convert(await taskInFlightFetchingRelevantArg, valueConverter);

            var task = taskGetter(requestArg);
            this.runningTasksMap.Add(requestArg, task);
            try
            {
                var result = await task;

                var convertedResult = ConvertValue.Convert(result, valueConverter);
                if (this.EnableCache)
                    this.ValueMap[requestArg] = convertedResult;

                return convertedResult;
            }
            finally
            {
                this.runningTasksMap.Remove(requestArg);
            }
        }

        public void Reset()
        {
            this.runningTasksMap.Clear();
            this.ResetCache();
        }

        public void ResetCache()
        {
            this.ValueMap.Clear();
        }

        public bool IsExecuting(TArg requestArg)
        {
            return this.runningTasksMap.ContainsKey(requestArg);
        }
    }

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

        public async Task Execute(
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
    }

    public class TakesEnumerationArgReturnsValue<TArg, TSourceValue, TValue>
    {
        public Dictionary<TArg, TValue> ValueMap { get; } = new Dictionary<TArg, TValue>();
        public bool EnableCache { get; set; } = true;

        private readonly Dictionary<TArg, Task<IEnumerable<TSourceValue>>> runningTasksMap = new Dictionary<TArg, Task<IEnumerable<TSourceValue>>>();

        public async Task<IEnumerable<TValue>> Execute(
            Func<IEnumerable<TArg>, Task<IEnumerable<TSourceValue>>> taskGetter,
            Func<TArg, IEnumerable<TValue>, TValue> valueForArgGetter,
            IEnumerable<TArg> requestArg,
            Func<IEnumerable<TSourceValue>, IEnumerable<TValue>> valueConverter = null,
            bool bypassCache = false)
        {
            requestArg = requestArg.Distinct();

            var argsNotInCache = !this.EnableCache && !bypassCache ? requestArg : requestArg.Where(t => !this.ValueMap.ContainsKey(t)).ToList();

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

                    if (this.EnableCache)
                    {
                        foreach (var arg in argsRequiringCall)
                        {
                            var resultValue = valueForArgGetter(arg, result);
                            this.ValueMap[arg] = resultValue;
                        }
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
                var fromCache = this.ValueMap.Where(t => argsFromCache.Contains(t.Key)).Select(t => t.Value);
                result = result.Union(fromCache);
            }

            return result;
        }

        public void Reset()
        {
            this.runningTasksMap.Clear();
            this.ResetCache();
        }

        public void ResetCache()
        {
            this.ValueMap.Clear();
        }

        public bool IsExecuting(TArg requestArg)
        {
            return this.runningTasksMap.ContainsKey(requestArg);
        }
    }
#pragma warning restore CS1591
}
