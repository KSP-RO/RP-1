using System;
using UnityEngine;
using System.Collections.Generic;

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
        /// Returns a list containing all types that can be casted to T,
        /// by default returning only instantiable types
        /// </summary>
        /// <typeparam name="T">T can be a regular type or an interface</typeparam>
        /// <param name="instantiableOnly">by default, ignore abstract types</param>
        /// <returns></returns>
        public static List<Type> GetAllLoadedTypes<T>(bool instantiableOnly = true)
        {
            var list = new List<Type>();
            var type = typeof(T);
            AssemblyLoader.loadedAssemblies.TypeOperation(a =>
            {
                if (type.IsAssignableFrom(a) && (!instantiableOnly || (!a.IsAbstract && !a.IsInterface)))
                    list.Add(a);
            });

            return list;
        }

        /// <summary>
        /// Tests to see if two ConfigNodes have the same information. Currently requires same ordering of values and subnodes
        /// </summary>
        /// <param name="node1"></param>
        /// <param name="node2"></param>
        /// <returns></returns>
        public static bool ConfigNodesAreEquivalent(ConfigNode node1, ConfigNode node2)
        {
            if (node1.nodes.Count != node2.nodes.Count)
                return false;

            if (node1.values.Count != node2.values.Count)
                return false;

            for (int i = node1.values.Count; i-- > 0;)
            {
                var v1 = node1.values[i];
                var v2 = node2.values[i];
                if (v1.name != v2.name || v1.value != v2.value)
                    return false;
            }

            for (int i = node1.nodes.Count; i-- > 0;)
                if (!ConfigNodesAreEquivalent(node1.nodes[i], node2.nodes[i]))
                    return false;

            return true;
        }

        /// <summary>
        /// Like PrePostActions, but does nothing in the Flight scene
        /// </summary>
        /// <param name="dialog"></param>
        /// <param name="lockType"></param>
        /// <param name="lockName"></param>
        /// <param name="onCreateAction"></param>
        /// <param name="onDestroyAction"></param>
        public static PopupDialog PrePostActionsNonFlight(this PopupDialog dialog, ControlTypes lockType = ControlTypes.None, string lockName = null, Callback onCreateAction = null, Callback onDestroyAction = null)
        {
            if (HighLogic.LoadedSceneIsFlight)
                return dialog;

            return PrePostActions(dialog, lockType, lockName, onCreateAction, onDestroyAction);
        }

        /// <summary>
        /// Adds a way for a PopupDialog to perform actions on spawn/despawn, like locking input for a true modal.
        /// </summary>
        /// <param name="dialog"></param>
        /// <param name="lockType">optional: the control locks to add</param>
        /// <param name="lockName">optional (will use default if not specified and locking controls)</param>
        /// <param name="onCreateAction">optional: runs on dialog spawn</param>
        /// <param name="onDestroyAction">optional: runs when dialog is destroyed</param>
        public static PopupDialog PrePostActions(this PopupDialog dialog, ControlTypes lockType = ControlTypes.None, string lockName = null, Callback onCreateAction = null, Callback onDestroyAction = null)
        {
            if (dialog == null)
                return null;

            if (onCreateAction != null)
                onCreateAction();
            if (lockType != ControlTypes.None)
            {
                if (lockName == null)
                    lockName = dialog.GetHashCode().ToString();

                InputLockManager.SetControlLock(lockType, lockName);
            }
            dialog.gameObject.AddComponent<LockRemover>().Setup(lockName, onDestroyAction);

            return dialog;
        }

        public class LockRemover : MonoBehaviour
        {
            private string _lockName;
            private Callback _dismissAction;
            private bool _dismissActionRun = false;

            public void Setup(string lockName, Callback dismissAction)
            {
                _lockName = lockName;
                _dismissAction = dismissAction;

                // If the dialog is dismissed by a button, we need to run the
                // postaction _before_ the button's own callback. This is because
                // usually we are restoring UI elements, and the button's callback
                // *also* probably tocuhes UI elements. So we need to restore state
                // prior to the callback running. To do this, we combine the callbacks
                // on all child buttons that dismiss on select.
                if (_dismissAction != null && gameObject.GetComponent<PopupDialog>() is PopupDialog dlg && dlg.dialogToDisplay != null)
                {
                    Callback cb = () =>
                    {
                        if (!_dismissActionRun)
                        {
                            _dismissActionRun = true;
                            _dismissAction();
                        }
                    };

                    foreach (var d in dlg.dialogToDisplay.Options)
                        CombineCallbackAndRecurse(d, cb);
                }
            }

            private void CombineCallbackAndRecurse(DialogGUIBase dlg, Callback cb)
            {
                if (dlg is DialogGUIButton b && b.DismissOnSelect)
                {
                    if (b.onOptionSelected == null)
                        b.onOptionSelected = cb;
                    else
                        b.onOptionSelected = (Callback)Callback.Combine(cb, b.onOptionSelected);
                }
                foreach (var d in dlg.children)
                    CombineCallbackAndRecurse(d, cb);
            }

            public void OnDestroy()
            {
                if (_lockName != null)
                    InputLockManager.RemoveControlLock(_lockName);

                if (_dismissAction != null && !_dismissActionRun)
                {
                    _dismissActionRun = true;
                    _dismissAction();
                }
            }
        }

        public static PopupDialog HideGUIsWhilePopupNonFlight(this PopupDialog dialog)
        {
            if (HighLogic.LoadedSceneIsFlight)
                return dialog;

            return HideGUIsWhilePopup(dialog);
        }

        public static PopupDialog HideGUIsWhilePopup(this PopupDialog dialog)
        {
            return PrePostActions(dialog, ControlTypes.KSC_ALL | ControlTypes.UI_MAIN | ControlTypes.EDITOR_SOFT_LOCK, "RP0GenericPopupDialogLock", OnDialogSpawn, OnDialogDismiss);
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
