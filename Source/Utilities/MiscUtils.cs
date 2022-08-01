using System.Collections.Generic;

namespace RP0
{
    public static class MiscUtils
    {
        public static TValue ValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        {
            dict.TryGetValue(key, out TValue value);
            return value;
        }
    }
}
