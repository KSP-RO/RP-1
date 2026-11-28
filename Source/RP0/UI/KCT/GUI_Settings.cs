using System;
using UnityEngine;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private const int _settingsWidth = 420, _settingsHeight = 100;
        private static Rect _settingsPosition = new Rect((Screen.width - _settingsWidth) / 2, (Screen.height - _settingsHeight) / 2, _settingsWidth, _settingsHeight);
        private static SCMSaveSettings _workingSaveSettings;

        private static bool _disableAllMsgs, _showSimWatermark, _debug, _overrideLaunchBtn, _autoAlarms, _cleanUpKSCDebris, _useDates, _inPlaceEdit;
        private static int _newTimewarp;

        public static void DrawSettingsWindow(int windowID)
        {
            if (_workingSaveSettings == null)
                _workingSaveSettings = new SCMSaveSettings(SCMSaveSettings.Instance);

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            //Save settings
            GUILayout.BeginVertical(GUILayout.Width(_settingsWidth / 2));
            GUILayout.Label("Save Settings", _yellowText);
            GUILayout.Label("NOTE: Affects this save only", _yellowText);
            GUILayout.BeginVertical(HighLogic.Skin.textArea);
            _workingSaveSettings.Enabled = GUILayout.Toggle(_workingSaveSettings.Enabled, "Mod Enabled", HighLogic.Skin.button);
            _workingSaveSettings.BuildTimes = GUILayout.Toggle(_workingSaveSettings.BuildTimes, "Build Times", HighLogic.Skin.button);
            _workingSaveSettings.TechUnlockTimes = GUILayout.Toggle(_workingSaveSettings.TechUnlockTimes, "Tech Unlock Times", HighLogic.Skin.button);
            _workingSaveSettings.KSCUpgradeTimes = GUILayout.Toggle(_workingSaveSettings.KSCUpgradeTimes, "KSC Upgrade Times", HighLogic.Skin.button);
            GUILayout.EndVertical();
            GUILayout.EndVertical();

            //General settings
            GUILayout.BeginVertical(GUILayout.Width(_settingsWidth / 2));
            GUILayout.Label("General Settings", _yellowText);
            GUILayout.Label("NOTE: Affects all saves!", _yellowText);
            GUILayout.BeginVertical(HighLogic.Skin.textArea);
            GUILayout.Label("Max Timewarp");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.ExpandWidth(false)))
            {
                _newTimewarp = Math.Max(_newTimewarp - 1, 0);
            }
            //current warp setting
            GUILayout.Label(TimeWarp.fetch.warpRates[_newTimewarp] + "x");
            if (GUILayout.Button("+", GUILayout.ExpandWidth(false)))
            {
                _newTimewarp = Math.Min(_newTimewarp + 1, TimeWarp.fetch.warpRates.Length - 1);
            }
            GUILayout.EndHorizontal();

            _autoAlarms = GUILayout.Toggle(_autoAlarms, "Auto Alarms", HighLogic.Skin.button);
            _overrideLaunchBtn = GUILayout.Toggle(_overrideLaunchBtn, "Override Launch Button", HighLogic.Skin.button);
            _disableAllMsgs = !GUILayout.Toggle(!_disableAllMsgs, "Use Message System", HighLogic.Skin.button);
            _showSimWatermark = GUILayout.Toggle(_showSimWatermark, "Show sim watermark", HighLogic.Skin.button);
            _debug = GUILayout.Toggle(_debug, "Debug Logging", HighLogic.Skin.button);
            _cleanUpKSCDebris = GUILayout.Toggle(_cleanUpKSCDebris, "Autoclean KSC Debris", HighLogic.Skin.button);
            _useDates = GUILayout.Toggle(_useDates, "Use Dates Not +Days", HighLogic.Skin.button);
            _inPlaceEdit = GUILayout.Toggle(_inPlaceEdit, "Edit Keeps Buildorder", HighLogic.Skin.button);
            GUILayout.EndVertical();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save", GUILayout.ExpandWidth(false)))
            {
                SCMSaveSettings.SetActive(_workingSaveSettings);
                SCMSaveSettings.Instance.Save();
                _workingSaveSettings = null;

                if (!SCMSaveSettings.Instance.Enabled)
                    KCTUtilities.DisableModFunctionality();
                KCTSettings.Instance.MaxTimeWarp = _newTimewarp;
                KCTSettings.Instance.DisableAllMessages = _disableAllMsgs;
                KCTSettings.Instance.ShowSimWatermark = _showSimWatermark;
                KCTSettings.Instance.OverrideLaunchButton = _overrideLaunchBtn;
                KCTSettings.Instance.AutoAlarms = _autoAlarms;
                KCTSettings.Instance.CleanUpKSCDebris = _cleanUpKSCDebris;
                KCTSettings.Instance.UseDates = _useDates;
                KCTSettings.Instance.InPlaceEdit = _inPlaceEdit;
                KCTSettings.Instance.DebugLogging = _debug;

                KCTSettings.Instance.Save();
                GUIStates.ShowSettings = false;
                if (!IsPrimarilyDisabled && !GUIStates.ShowFirstRun)
                {
                    ResetBLWindow();
                    GUIStates.ShowBuildList = true;
                    RefreshToolbarState();
                }
                if (!SCMSaveSettings.Instance.Enabled) InputLockManager.RemoveControlLock(SpaceCenterManagement.SCMKSCLock);

                SpaceCenterManagement.Instance.RecalculateBuildRates();

                ResetFormulaRateHolders();
                Harmony.PatchKSCFacilityContextMenu.AreTextsUpdated = false;
            }
            if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(false)))
            {
                _workingSaveSettings = null;
                GUIStates.ShowSettings = false;
                if (!IsPrimarilyDisabled && !GUIStates.ShowFirstRun)
                {
                    ResetBLWindow();
                    GUIStates.ShowBuildList = true;
                    RefreshToolbarState();
                }

                SpaceCenterManagement.Instance.RecalculateBuildRates();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
        }

        private static void ShowSettings()
        {
            _workingSaveSettings = new SCMSaveSettings(SCMSaveSettings.Instance);
            _newTimewarp = KCTSettings.Instance.MaxTimeWarp;
            _disableAllMsgs = KCTSettings.Instance.DisableAllMessages;
            _showSimWatermark = KCTSettings.Instance.ShowSimWatermark;
            _overrideLaunchBtn = KCTSettings.Instance.OverrideLaunchButton;
            _autoAlarms = KCTSettings.Instance.AutoAlarms;
            _cleanUpKSCDebris = KCTSettings.Instance.CleanUpKSCDebris;
            _useDates = KCTSettings.Instance.UseDates;
            _inPlaceEdit = KCTSettings.Instance.InPlaceEdit;
            _debug = KCTSettings.Instance.DebugLogging;

            GUIStates.ShowSettings = !GUIStates.ShowSettings;
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
