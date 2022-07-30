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

        public static void DialogInputLock(this PopupDialog dialog, ControlTypes lockType, string lockName, Action onCreateAction = null, Action onDestroyAction = null)
        {
            if (dialog == null)
                return;

            if (onCreateAction != null)
                onCreateAction();

            InputLockManager.SetControlLock(lockType, lockName);
            dialog.gameObject.AddComponent<LockRemover>().Setup(lockName, onDestroyAction);
        }

        public class LockRemover : MonoBehaviour
        {
            private string _lockName;
            private Action _action;
            
            public void Setup(string lockName, Action action)
            { 
                _lockName = lockName;
                _action = action;
            }

            public void OnDestroy()
            {
                InputLockManager.RemoveControlLock(_lockName);
                if (_action != null)
                    _action();
            }
        }
    }
}
