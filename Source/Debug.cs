using System;
using System.Collections.Generic;

namespace RP0
{
    public static class RP0Debug
    {
        public static void Log(string str)
        {
#if DEBUG
            UnityEngine.Debug.Log(str);
#endif
        }

        public static void LogWarning(string str)
        {
#if DEBUG
            UnityEngine.Debug.LogWarning(str);
#endif
        }

        public static void LogError(string str)
        {
#if DEBUG
            UnityEngine.Debug.LogError(str);
#endif
        }
    }
}
