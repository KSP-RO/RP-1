using System;
using UnityEngine;
using System.Collections.Generic;

namespace RP0
{
    public static class GUIUtils
    {
        public static PopupDialog HideGUIsWhilePopupNonFlight(this PopupDialog dialog)
        {
            if (HighLogic.LoadedSceneIsFlight)
                return dialog;

            return HideGUIsWhilePopup(dialog);
        }

        public static PopupDialog HideGUIsWhilePopup(this PopupDialog dialog)
        {
            return ROUtils.KSPUtils.PrePostActions(dialog, ControlTypes.KSC_ALL | ControlTypes.UI_MAIN | ControlTypes.EDITOR_SOFT_LOCK, "RP0GenericPopupDialogLock", OnDialogSpawn, OnDialogDismiss);
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
