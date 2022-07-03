using UnityEngine;
using System;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class ContinuousLogger : MonoBehaviour
    {
        public static ContinuousLogger Instance { get; private set; }

        public Func<string> LogStr = null;

        public static bool StartLogging(Func<string> loggingFunction, int frames = int.MaxValue)
        {
            if (Instance == null)
                return false;

            Instance.StartLog(loggingFunction, frames);
            return true;
        }

        public static bool StopLogging()
        {
            if (Instance == null)
                return false;

            return Instance.StopLog();
        }

        private int _logFrames = 0;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }

            Instance = this;
            DontDestroyOnLoad(this);
            this.enabled = false;
        }

        private void Update()
        {
            if (_logFrames-- > 0 && LogStr != null)
                Debug.Log(LogStr());
            else
                enabled = false;
        }

        private void StartLog(Func<string> loggingFunction, int frames)
        {
            enabled = true;
            _logFrames = frames;
            LogStr = loggingFunction;
        }

        private bool StopLog()
        {
            if (!enabled)
                return false;

            _logFrames = 0;
            enabled = false;
            return true;
        }
    }
}
