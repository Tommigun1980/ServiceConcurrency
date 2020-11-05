using System;

namespace ServiceConcurrency
{
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
}
