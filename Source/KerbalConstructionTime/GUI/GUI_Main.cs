using ClickThroughFix;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        public static GUIStates GUIStates = new GUIStates();
        public static GUIStates PrevGUIStates = null;
        public static GUIDataSaver GuiDataSaver = new GUIDataSaver();

        private static Rect _centralWindowPosition = new Rect((Screen.width - 150) / 2, (Screen.height - 50) / 2, 150, 50);
        private static Rect _blPlusPosition = new Rect(Screen.width - 500, 40, 100, 1);
        private static Vector2 _scrollPos;
        private static GUISkin _windowSkin;
        private static GUIStyle _orangeText;

        private static bool _unlockEditor;
        private static bool _isKSCLocked = false;
        private static readonly List<GameScenes> _validScenes = new List<GameScenes> { GameScenes.FLIGHT, GameScenes.EDITOR, GameScenes.SPACECENTER, GameScenes.TRACKSTATION };

        public static bool IsPrimarilyDisabled => PresetManager.PresetLoaded() && (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled ||
                                                                                   !PresetManager.Instance.ActivePreset.GeneralSettings.BuildTimes);

        public static void SetGUIPositions()
        {
            GUISkin oldSkin = GUI.skin;
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER && _windowSkin == null)
                _windowSkin = GUI.skin;
            GUI.skin = _windowSkin;

            if (_validScenes.Contains(HighLogic.LoadedScene))
            {
                if (GUIStates.ShowSettings)
                    _presetPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawPresetWindow"), _presetPosition, DrawPresetWindow, "KCT Settings", HighLogic.Skin.window);
                if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                    return;

                if (GUIStates.ShowEditorGUI)
                    EditorWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawEditorGUI"), EditorWindowPosition, DrawEditorGUI, "Kerbal Construction Time", HighLogic.Skin.window);
                if (GUIStates.ShowBuildList)
                {
                    ref Rect pos = ref (HighLogic.LoadedSceneIsEditor ? ref EditorBuildListWindowPosition : ref BuildListWindowPosition);
                    pos = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawBuildListWindow"), pos, DrawBuildListWindow, "Build List", HighLogic.Skin.window);
                }
                if (GUIStates.ShowClearLaunch)
                    _centralWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawClearLaunch"), _centralWindowPosition, DrawClearLaunch, "Launch site not clear!", HighLogic.Skin.window);
                if (GUIStates.ShowShipRoster)
                    _crewListWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawShipRoster"), _crewListWindowPosition, DrawShipRoster, "Select Crew", HighLogic.Skin.window);
                if (GUIStates.ShowCrewSelect)
                    _crewListWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawCrewSelect"), _crewListWindowPosition, DrawCrewSelect, "Select Crew & Launch", HighLogic.Skin.window);
                if (GUIStates.ShowUpgradeWindow)
                    _upgradePosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawUpgradeWindow"), _upgradePosition, DrawUpgradeWindow, "Upgrades", HighLogic.Skin.window);
                if (GUIStates.ShowBLPlus)
                    _blPlusPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawBLPlusWindow"), _blPlusPosition, DrawBLPlusWindow, "Options", HighLogic.Skin.window);
                if (GUIStates.ShowDismantlePad)
                    _centralWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawDismantlePadWindow"), _centralWindowPosition, DrawDismantlePadWindow, "Dismantle pad", HighLogic.Skin.window);
                if (GUIStates.ShowRename)
                    _centralWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawRenameWindow"), _centralWindowPosition, DrawRenameWindow, "Rename", HighLogic.Skin.window);
                if (GUIStates.ShowNewPad)
                    _centralWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawNewPadWindow"), _centralWindowPosition, DrawNewPadWindow, "New launch pad", HighLogic.Skin.window);
                if (GUIStates.ShowFirstRun)
                    _firstRunWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawFirstRun"), _firstRunWindowPosition, DrawFirstRun, "Kerbal Construction Time", HighLogic.Skin.window);
                if (GUIStates.ShowPresetSaver)
                    _presetNamingWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawPresetSaveWindow"), _presetNamingWindowPosition, DrawPresetSaveWindow, "Save as New Preset", HighLogic.Skin.window);
                if (GUIStates.ShowLaunchSiteSelector)
                    _centralWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawLaunchSiteChooser"), _centralWindowPosition, DrawLaunchSiteChooser, "Select Site", HighLogic.Skin.window);

                if (GUIStates.ShowBuildPlansWindow)
                    _buildPlansWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawBuildPlansWindow"), _buildPlansWindowPosition, DrawBuildPlansWindow, "Building Plans & Construction", HighLogic.Skin.window);

                // both flags can be true when it's necessary to first show ClearLaunch and then Airlaunch right after that
                if (GUIStates.ShowAirlaunch && !GUIStates.ShowClearLaunch)
                    _airlaunchWindowPosition = ClickThruBlocker.GUILayoutWindow(WindowHelper.NextWindowId("DrawAirlaunchWindow"), _airlaunchWindowPosition, DrawAirlaunchWindow, "Airlaunch", HighLogic.Skin.window);

                if (_unlockEditor)
                {
                    EditorLogic.fetch.Unlock("KCTGUILock");
                    _unlockEditor = false;
                }

                if (HighLogic.LoadedSceneIsEditor)
                {
                    DoBuildPlansList();
                }

                //Disable KSC things when certain windows are shown.
                if (GUIStates.ShowFirstRun || GUIStates.ShowRename || GUIStates.ShowNewPad || GUIStates.ShowDismantlePad || GUIStates.ShowUpgradeWindow || GUIStates.ShowSettings || GUIStates.ShowCrewSelect || GUIStates.ShowShipRoster || GUIStates.ShowClearLaunch || GUIStates.ShowAirlaunch)
                {
                    if (!_isKSCLocked)
                    {
                        InputLockManager.SetControlLock(ControlTypes.KSC_FACILITIES, "KCTKSCLock");
                        _isKSCLocked = true;
                    }
                }
                else if (_isKSCLocked)
                {
                    InputLockManager.RemoveControlLock("KCTKSCLock");
                    _isKSCLocked = false;
                }
                GUI.skin = oldSkin;
            }
        }

        public static void ClickToggle()
        {
            ToggleVisibility(!GUIStates.IsMainGuiVisible);
        }

        public static void ToggleVisibility(bool isVisible)
        {
            if (KCTEvents.Instance.KCTButtonStockImportant)
                KCTEvents.Instance.KCTButtonStockImportant = false;

            if (HighLogic.LoadedScene == GameScenes.FLIGHT && !IsPrimarilyDisabled)
            {
                BuildListWindowPosition.height = 1;
                GUIStates.ShowBuildList = isVisible;
                GUIStates.ShowBLPlus = false;
                ResetBLWindow();

                if (Utilities.IsKRASHSimActive && AirlaunchTechLevel.AnyUnlocked())
                {
                    GUIStates.ShowAirlaunch = isVisible;
                }
            }
            else if ((HighLogic.LoadedScene == GameScenes.EDITOR) && !IsPrimarilyDisabled)
            {
                EditorWindowPosition.height = 1;
                GUIStates.ShowEditorGUI = isVisible;
                KCTGameStates.ShowWindows[1] = isVisible;
            }
            else if ((HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION) && !IsPrimarilyDisabled)
            {
                BuildListWindowPosition.height = 1;
                GUIStates.ShowBuildList = isVisible;
                GUIStates.ShowBuildPlansWindow = false;
                GUIStates.ShowBLPlus = false;
                ResetBLWindow();
                KCTGameStates.ShowWindows[0] = isVisible;
            }

            RefreshToolbarState();
        }

        private static void RefreshToolbarState()
        {
            if (GUIStates.IsMainGuiVisible)
            {
                KCTGameStates.ToolbarControl.SetTrue(false);
            }
            else
            {
                KCTGameStates.ToolbarControl.SetFalse(false);
            }
        }

        public static void RestorePrevUIState()
        {
            if (PrevGUIStates == null) return;

            GUIStates = PrevGUIStates;
            PrevGUIStates = null;
            RefreshToolbarState();
        }

        public static void BackupUIState()
        {
            PrevGUIStates = GUIStates.Clone();
        }

        public static void OnRightClick()
        {
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER && PresetManager.PresetLoaded() && !GUIStates.ShowFirstRun)
            {
                if (!GUIStates.ShowSettings)
                {
                    ShowSettings();
                }
                else
                {
                    GUIStates.ShowSettings = false;
                }
            }
        }

        public static void HideAll()
        {
            GUIStates.ShowEditorGUI = false;
            GUIStates.ShowBuildList = false;
            GUIStates.ShowClearLaunch = false;
            GUIStates.ShowShipRoster = false;
            GUIStates.ShowCrewSelect = false;
            GUIStates.ShowSettings = false;
            GUIStates.ShowUpgradeWindow = false;
            GUIStates.ShowBLPlus = false;
            GUIStates.ShowRename = false;
            GUIStates.ShowDismantlePad = false;
            GUIStates.ShowFirstRun = false;
            GUIStates.ShowPresetSaver = false;
            GUIStates.ShowLaunchSiteSelector = false;
            GUIStates.ShowAirlaunch = false;

            ResetBLWindow();
        }

        public static void RemoveInputLocks() => InputLockManager.RemoveControlLock("KCTPopupLock");

        public static void ResetFormulaRateHolders()
        {
            _fundsCost = -13;
            _nodeRate = -13;
            _upNodeRate = -13;
            _researchRate = -13;
            _upResearchRate = -13;
            _costOfNewLP = -13;
        }

        public static void CenterWindow(ref Rect window)
        {
            window.x = (float)((Screen.width - window.width) / 2.0);
            window.y = (float)((Screen.height - window.height) / 2.0);
        }

        /// <summary>
        /// Clamps a window to the screen
        /// </summary>
        /// <param name="window">The window Rect</param>
        /// <param name="strict">If true, none of the window can go past the edge.
        /// If false, half the window can. Defaults to false.</param>
        public static void ClampWindow(ref Rect window, bool strict = false)
        {
            if (strict)
            {
                if (window.x < 0)
                    window.x = 0;
                if (window.x + window.width > Screen.width)
                    window.x = Screen.width - window.width;

                if (window.y < 0)
                    window.y = 0;
                if (window.y + window.height > Screen.height)
                    window.y = Screen.height - window.height;
            }
            else
            {
                float halfW = window.width / 2;
                float halfH = window.height / 2;
                if (window.x + halfW < 0)
                    window.x = -halfW;
                if (window.x + halfW > Screen.width)
                    window.x = Screen.width - halfW;

                if (window.y + halfH < 0)
                    window.y = -halfH;
                if (window.y + halfH > Screen.height)
                    window.y = Screen.height - halfH;
            }
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
