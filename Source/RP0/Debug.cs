using System;

namespace RP0
{
    public static class RP0Debug
    {
        public static void Log(string str, bool always = false)
        {
#if DEBUG
            bool isBetaVersion = true;
#else
            bool isBetaVersion = always;
#endif
            if (isBetaVersion)
            {
                UnityEngine.Debug.Log("[RP-0] " + str);
            }
        }

        public static void LogWarning(string str)
        {
            UnityEngine.Debug.LogWarning("[RP-0] " + str);
        }

        public static void LogError(string str)
        {
            UnityEngine.Debug.LogError("[RP-0] " + str);
        }

        public static void LogException(Exception ex)
        {
            UnityEngine.Debug.LogError("[RP-0] " + ex);
        }
    }
}
