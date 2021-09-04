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
                    _presetPosition = DrawWindowWithTooltipSupport(_presetPosition, "DrawPresetWindow", "KCT Settings", DrawPresetWindow);
                if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                    return;

                if (GUIStates.ShowEditorGUI)
                    EditorWindowPosition = DrawWindowWithTooltipSupport(EditorWindowPosition, "DrawEditorGUI", "Kerbal Construction Time", DrawEditorGUI);
                if (GUIStates.ShowSimulationGUI)
                    _simulationWindowPosition = DrawWindowWithTooltipSupport(_simulationWindowPosition, "DrawSimGUI", "KCT Simulation", DrawSimulationWindow);
                if (GUIStates.ShowSimConfig)
                    _simulationConfigPosition = DrawWindowWithTooltipSupport(_simulationConfigPosition, "DrawSimConfGUI", "Simulation Configuration", DrawSimulationConfigure);
                if (GUIStates.ShowSimBodyChooser)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawSimBodyGUI", "Choose Body", DrawBodyChooser);
                if (GUIStates.ShowBuildList)
                {
                    ref Rect pos = ref (HighLogic.LoadedSceneIsEditor ? ref EditorBuildListWindowPosition : ref BuildListWindowPosition);
                    pos = DrawWindowWithTooltipSupport(pos, "DrawBuildListWindow", "Build List", DrawBuildListWindow);
                }
                if (GUIStates.ShowClearLaunch)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawClearLaunch", "Launch site not clear!", DrawClearLaunch);
                if (GUIStates.ShowShipRoster)
                    _crewListWindowPosition = DrawWindowWithTooltipSupport(_crewListWindowPosition, "DrawShipRoster", "Select Crew", DrawShipRoster);
                if (GUIStates.ShowCrewSelect)
                    _crewListWindowPosition = DrawWindowWithTooltipSupport(_crewListWindowPosition, "DrawCrewSelect", "Select Crew & Launch", DrawCrewSelect);
                if (GUIStates.ShowUpgradeWindow)
                    _upgradePosition = DrawWindowWithTooltipSupport(_upgradePosition, "DrawUpgradeWindow", "Upgrades", DrawUpgradeWindow);
                if (GUIStates.ShowBLPlus)
                    _blPlusPosition = DrawWindowWithTooltipSupport(_blPlusPosition, "DrawBLPlusWindow", "Options", DrawBLPlusWindow);
                if (GUIStates.ShowDismantlePad)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawDismantlePadWindow", "Dismantle pad", DrawDismantlePadWindow);
                if (GUIStates.ShowRename)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawRenameWindow", "Rename", DrawRenameWindow);
                if (GUIStates.ShowNewPad)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawNewPadWindow", "New launch pad", DrawNewPadWindow);
                if (GUIStates.ShowFirstRun)
                    _firstRunWindowPosition = DrawWindowWithTooltipSupport(_firstRunWindowPosition, "DrawFirstRun", "Kerbal Construction Time", DrawFirstRun);
                if (GUIStates.ShowPresetSaver)
                    _presetNamingWindowPosition = DrawWindowWithTooltipSupport(_presetNamingWindowPosition, "DrawPresetSaveWindow", "Save as New Preset", DrawPresetSaveWindow);
                if (GUIStates.ShowLaunchSiteSelector)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawLaunchSiteChooser", "Select Site", DrawLaunchSiteChooser);

                if (GUIStates.ShowBuildPlansWindow)
                    _buildPlansWindowPosition = DrawWindowWithTooltipSupport(_buildPlansWindowPosition, "DrawBuildPlansWindow", "Building Plans & Construction", DrawBuildPlansWindow);

                // both flags can be true when it's necessary to first show ClearLaunch and then Airlaunch right after that
                if (GUIStates.ShowAirlaunch && !GUIStates.ShowClearLaunch)
                    _airlaunchWindowPosition = DrawWindowWithTooltipSupport(_airlaunchWindowPosition, "DrawAirlaunchWindow", "Airlaunch", DrawAirlaunchWindow);

                if (_unlockEditor)
                {
                    EditorLogic.fetch.Unlock("KCTGUILock");
                    _unlockEditor = false;
                }

                if (HighLogic.LoadedSceneIsEditor)
                {
                    DoBuildPlansList();
                    CreateDevPartsToggle();
                }

                //Disable KSC things when certain windows are shown.
                if (GUIStates.ShowFirstRun || GUIStates.ShowRename || GUIStates.ShowNewPad || GUIStates.ShowDismantlePad || GUIStates.ShowUpgradeWindow || GUIStates.ShowSettings || GUIStates.ShowCrewSelect || GUIStates.ShowShipRoster || GUIStates.ShowClearLaunch || GUIStates.ShowAirlaunch)
                {
                    if (!_isKSCLocked)
                    {
                        InputLockManager.SetControlLock(ControlTypes.KSC_FACILITIES, KerbalConstructionTime.KCTKSCLock);
                        _isKSCLocked = true;
                    }
                }
                else if (_isKSCLocked)
                {
                    InputLockManager.RemoveControlLock(KerbalConstructionTime.KCTKSCLock);
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

                if (Utilities.IsSimulationActive && AirlaunchTechLevel.AnyUnlocked())
                {
                    GUIStates.ShowAirlaunch = isVisible;
                }
                if (KCTGameStates.IsSimulatedFlight)
                {
                    GUIStates.ShowSimulationGUI = isVisible;
                    _simulationWindowPosition.height = 1;
                }
            }
            else if ((HighLogic.LoadedScene == GameScenes.EDITOR) && !IsPrimarilyDisabled)
            {
                EditorWindowPosition.height = 1;
                GUIStates.ShowEditorGUI = isVisible;
                if (!isVisible)
                    GUIStates.ShowBuildList = false;
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

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                int idx = HighLogic.LoadedScene == GameScenes.SPACECENTER ? 0 : 1;
                KCTGameStates.ShowWindows[idx] = GUIStates.IsMainGuiVisible;
            }

            RefreshToolbarState();
        }

        public static void BackupUIState()
        {
            PrevGUIStates = GUIStates.Clone();
        }

        public static void EnsureEditModeIsVisible()
        {
            GUIStates.ShowEditorGUI = true;
            RefreshToolbarState();
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
            _fundsCost = int.MinValue;
            _nodeRate = int.MinValue;
            _upNodeRate = int.MinValue;
            _researchRate = int.MinValue;
            _upResearchRate = int.MinValue;
            _costOfNewLP = int.MinValue;
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
