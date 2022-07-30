using System;
using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public static class KSPUtils
    {
        /// <summary>
        /// Use this method instead of Planetarium.GetUniversalTime().
        /// Fixes the KSP stupidity where wrong UT can be returned when reverting back to the Editor.
        /// </summary>
        /// <returns></returns>
        public static double GetUT()
        {
            return HighLogic.LoadedSceneIsEditor ? HighLogic.CurrentGame.UniversalTime : Planetarium.GetUniversalTime();
        }

        public static void DialogInputLock(this PopupDialog dialog, ControlTypes lockType, string lockName)
        {
            if (dialog == null)
                return;

            InputLockManager.SetControlLock(lockType, lockName);
            dialog.gameObject.AddComponent<LockRemover>().Setup(lockName);
        }

        public class LockRemover : MonoBehaviour
        {
            private string _lockName;
            
            public void Setup(string lockName) { _lockName = lockName; }

            public void OnDestroy() { InputLockManager.RemoveControlLock(_lockName); }
        }
    }
}
