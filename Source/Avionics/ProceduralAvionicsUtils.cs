using UnityEngine;

namespace RP0.ProceduralAvionics
{
    static class ProceduralAvionicsUtils
    {
        private static bool _enableLogging = true;

        private const string LogPrefix = "[ProcAvi] ";

        public static void Log(params string[] message)
        {
            if (_enableLogging)
            {
                var builder = StringBuilderCache.Acquire();
                builder.Append(LogPrefix);
                foreach (string part in message)
                {
                    builder.Append(part);
                }
                Debug.Log(builder.ToStringAndRelease());
            }
        }

        public static void Log(params object[] parts)
        {
            if (_enableLogging)
            {
                var builder = StringBuilderCache.Acquire();
                builder.Append(LogPrefix);
                foreach (object part in parts)
                {
                    builder.Append(part.ToString());
                }
                Debug.Log(builder.ToStringAndRelease());
            }
        }
    }
}
