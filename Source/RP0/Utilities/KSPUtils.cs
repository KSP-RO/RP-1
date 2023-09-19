using System;
using UnityEngine;

namespace RP0
{
    public static class KSPUtils
    {
        public static bool CurrentGameHasScience()
        {
            return HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX;
        }

        public static bool CurrentGameIsSandbox()
        {
            return HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX;
        }

        public static bool CurrentGameIsCareer()
        {
            return HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
        }

        public static bool CurrentGameIsMission()
        {
            return HighLogic.CurrentGame.Mode == Game.Modes.MISSION || HighLogic.CurrentGame.Mode == Game.Modes.MISSION_BUILDER;
        }

        /// <summary>
        /// Adds a way for a PopupDialog to perform actions on spawn/despawn, like locking input for a true modal.
        /// </summary>
        /// <param name="dialog"></param>
        /// <param name="lockType">optional: the control locks to add</param>
        /// <param name="lockName">optional (will use default if not specified and locking controls)</param>
        /// <param name="onCreateAction">optional: runs on dialog spawn</param>
        /// <param name="onDestroyAction">optional: runs when dialog is destroyed</param>
        public static void PrePostActions(this PopupDialog dialog, ControlTypes lockType = ControlTypes.None, string lockName = null, Action onCreateAction = null, Action onDestroyAction = null)
        {
            if (dialog == null)
                return;

            if (onCreateAction != null)
                onCreateAction();
            if (lockType != ControlTypes.None)
            {
                if (lockName == null)
                    lockName = dialog.GetHashCode().ToString();

                InputLockManager.SetControlLock(lockType, lockName);
            }
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
                if (_lockName != null)
                    InputLockManager.RemoveControlLock(_lockName);

                if (_action != null)
                    _action();
            }
        }

        public static void HideGUIsWhilePopup(this PopupDialog dialog)
        {
            PrePostActions(dialog, ControlTypes.KSC_ALL | ControlTypes.UI_MAIN | ControlTypes.EDITOR_SOFT_LOCK, "RP0GenericPopupDialogLock", OnDialogSpawn, OnDialogDismiss);
        }

        private static void OnDialogSpawn()
        {
            UIHolder.Instance.HideIfShowing();
            KCT_GUI.BackupUIState();
            KCT_GUI.HideAll();
        }

        private static void OnDialogDismiss()
        {
            UIHolder.Instance.ShowIfWasHidden();
            KCT_GUI.RestorePrevUIState();
        }
    }
}
