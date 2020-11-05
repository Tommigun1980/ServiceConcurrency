using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Extensions.Caching.Memory;

namespace ServiceConcurrency
{
#pragma warning disable CS1591
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class BaseMemoryCache<TArg, TValue> : IEnumerable, IEnumerable<KeyValuePair<TArg, TValue>>, IDisposable
    {
        public delegate void ValueChangedAction(TArg key, TValue value, BaseMemoryCache<TArg, TValue> cacheInstance = null);

        public MemoryCacheEntryOptions CacheEntryOptions { get; set; } = new MemoryCacheEntryOptions() { SlidingExpiration = TimeSpan.FromHours(1) };
        public bool IsCacheShared { get; private set; }

        private IMemoryCache cache;
        private readonly ISet<TArg> possibleKeys = new HashSet<TArg>(); // might contain keys for expired items, only used for clearing cache
        private readonly ValueChangedAction onValueChanged;

        public BaseMemoryCache(IMemoryCache cache, ValueChangedAction onValueChanged = null)
        {
            this.cache = cache;
            this.IsCacheShared = true;
            this.onValueChanged = onValueChanged;
        }
        public BaseMemoryCache(ValueChangedAction onValueChanged = null) : this(new MemoryCache(new MemoryCacheOptions()), onValueChanged)
        {
            this.IsCacheShared = false;
        }

        public void ResetCache()
        {
            foreach (var key in this.possibleKeys)
            {
                this.cache.Remove(key);

                this.onValueChanged?.Invoke(key, default(TValue), this);
            }
            this.possibleKeys.Clear();
        }

        public void Set(TArg key, TValue value)
        {
            this.cache.Set(key, value, this.CacheEntryOptions);
            this.possibleKeys.Add(key);

            this.onValueChanged?.Invoke(key, value, this);
        }

        public bool TryGetValue(TArg key, out TValue value)
        {
            return this.cache.TryGetValue(key, out value);
        }

        public void Remove(TArg key)
        {
            this.cache.Remove(key);
            this.possibleKeys.Remove(key);

            this.onValueChanged?.Invoke(key, default(TValue), this);
        }

        public bool ContainsKey(TArg key)
        {
            TValue temp;
            return this.cache.TryGetValue(key, out temp);
        }

        public TValue this[TArg key]
        {
            get
            {
                TValue value;
                if (this.cache.TryGetValue(key, out value))
                    return value;
                throw new KeyNotFoundException($"They key '{key}' does not exist");
            }
            set
            {
                this.Set(key, value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TArg, TValue>> GetEnumerator()
        {
            foreach (var key in this.possibleKeys)
            {
                TValue value;
                if (this.cache.TryGetValue(key, out value))
                    yield return new KeyValuePair<TArg, TValue>(key, value);
            }
        }

        public void Dispose()
        {
            if (!this.IsCacheShared)
            {
                this.cache.Dispose();

                if (this.onValueChanged != null)
                {
                    foreach (var key in this.possibleKeys)
                        this.onValueChanged.Invoke(key, default(TValue), this);
                }

                this.possibleKeys.Clear();
            }
            else
            {
                this.ResetCache();
            }
        }
    }
#pragma warning restore CS1591
}
