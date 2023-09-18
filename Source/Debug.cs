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
                UnityEngine.Debug.Log(str);
            }
        }

        public static void LogWarning(string str)
        {
            UnityEngine.Debug.LogWarning(str);
        }

        public static void LogError(string str)
        {
            UnityEngine.Debug.LogError(str);
        }
    }
}
