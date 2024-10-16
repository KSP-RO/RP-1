using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public static partial class KCT_GUI
    {
        public static GUIStates GUIStates = new GUIStates();
        public static Stack<GUIStates> PrevGUIStates = new Stack<GUIStates>();
        public static GUIDataSaver GuiDataSaver = new GUIDataSaver();

        private static Rect _centralWindowPosition = new Rect((Screen.width - 150) / 2, (Screen.height - 50) / 2, 150, 50);
        private static Rect _blPlusPosition = new Rect(Screen.width - 500, 40, 100, 1);
        private static Rect _lcResourcesPosition = new Rect(_centralWindowPosition.xMin - 150, _centralWindowPosition.yMin, 250, 200);
        private static Vector2 _scrollPos;
        private static Vector2 _scrollPos2;
        private static GUISkin _windowSkin;
        private static GUIStyle _orangeText;

        private static bool _unlockEditor;
        private static bool _isKSCLocked = false;
        private static bool _inSCSubscene = false;
        public static bool InSCSubscene => _inSCSubscene;
        private static readonly List<GameScenes> _validScenes = new List<GameScenes> { GameScenes.FLIGHT, GameScenes.EDITOR, GameScenes.SPACECENTER, GameScenes.TRACKSTATION };
        private static GUIStyle _styleLabelRightAlign;
        private static GUIStyle _styleLabelRightAlignYellow;
        private static GUIStyle _styleLabelYellow;
        private static GUIStyle _styleLabelCenterAlign;
        private static GUIStyle _styleTextFieldRightAlign;

        public static bool IsPrimarilyDisabled => PresetManager.PresetLoaded() && (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled ||
                                                                                   !PresetManager.Instance.ActivePreset.GeneralSettings.BuildTimes);

        public static void SetGUIPositions()
        {
            GUISkin oldSkin = GUI.skin;
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER && _windowSkin == null)
                _windowSkin = GUI.skin;
            GUI.skin = _windowSkin;

            if (_validScenes.Contains(HighLogic.LoadedScene) && SpaceCenterManagement.Instance != null)
            {
                if (GUIStates.ShowSettings)
                    _presetPosition = DrawWindowWithTooltipSupport(_presetPosition, "DrawPresetWindow", "Settings", DrawPresetWindow);
                if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                    return;

                if (Milestones.NewspaperUI.IsOpen)
                    return;

                if (GUIStates.ShowEditorGUI)
                    EditorWindowPosition = DrawWindowWithTooltipSupport(EditorWindowPosition, "DrawEditorGUI", "Integration Info", DrawEditorGUI);
                if (GUIStates.ShowSimulationGUI)
                    _simulationWindowPosition = DrawWindowWithTooltipSupport(_simulationWindowPosition, "DrawSimGUI", "Simulation", DrawSimulationWindow);
                if (GUIStates.ShowSimConfig)
                    _simulationConfigPosition = DrawWindowWithTooltipSupport(_simulationConfigPosition, "DrawSimConfGUI", "Simulation Configuration", DrawSimulationConfigure);
                if (GUIStates.ShowSimBodyChooser)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawSimBodyGUI", "Choose Body", DrawBodyChooser);
                if (GUIStates.ShowBuildList)
                {
                    ref Rect pos = ref (HighLogic.LoadedSceneIsEditor ? ref EditorBuildListWindowPosition : ref BuildListWindowPosition);
                    pos = DrawWindowWithTooltipSupport(pos, "DrawBuildListWindow", "Space Center Management", DrawBuildListWindow);
                }
                if (GUIStates.ShowClearLaunch)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawClearLaunch", "Launch site not clear!", DrawClearLaunch);
                if (GUIStates.ShowShipRoster)
                    _crewListWindowPosition = DrawWindowWithTooltipSupport(_crewListWindowPosition, "DrawShipRoster", "Select Crew", DrawShipRoster);
                if (GUIStates.ShowCrewSelect)
                    _crewListWindowPosition = DrawWindowWithTooltipSupport(_crewListWindowPosition, "DrawCrewSelect", "Select Crew & Launch", DrawCrewSelect);
                //if (GUIStates.ShowUpgradeWindow)
                //    _upgradePosition = DrawWindowWithTooltipSupport(_upgradePosition, "DrawUpgradeWindow", "Upgrades", DrawUpgradeWindow);
                if (GUIStates.ShowPersonnelWindow)
                    _personnelPosition = DrawWindowWithTooltipSupport(_personnelPosition, "DrawPersonnelWindow", "Staffing", DrawPersonnelWindow);
                if (GUIStates.ShowBLPlus)
                    _blPlusPosition = DrawWindowWithTooltipSupport(_blPlusPosition, "DrawBLPlusWindow", "Options", DrawBLPlusWindow);
                if (GUIStates.ShowDismantlePad)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawDismantlePadWindow", "Dismantle Pad", DrawDismantlePadWindow);
                if (GUIStates.ShowDismantleLC)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawDismantlePadWindow", "Dismantle Launch Complex", DrawDismantlePadWindow);
                if (GUIStates.ShowRename)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawRenameWindow", "Rename", DrawRenameWindow);
                if (GUIStates.ShowNewPad)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawNewPadWindow", "New Launch Pad", DrawNewPadWindow);
                if (GUIStates.ShowNewLC)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawNewLCWindow", "New Launch Complex", DrawNewLCWindow);
                if (GUIStates.ShowModifyLC)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawModifyLCWindow", "Modify Launch Complex", DrawNewLCWindow);
                if (GUIStates.ShowLCResources)
                    _lcResourcesPosition = DrawWindowWithTooltipSupport(_lcResourcesPosition, "DrawLCResourcesWindow", "Resources", DrawLCResourcesWindow);
                if (GUIStates.ShowFirstRun)
                    _firstRunWindowPosition = DrawWindowWithTooltipSupport(_firstRunWindowPosition, "DrawFirstRun", "Space Center Setup", DrawFirstRun);
                if (GUIStates.ShowPresetSaver)
                    _presetNamingWindowPosition = DrawWindowWithTooltipSupport(_presetNamingWindowPosition, "DrawPresetSaveWindow", "Save as New Preset", DrawPresetSaveWindow);
                if (GUIStates.ShowLaunchSiteSelector)
                    _centralWindowPosition = DrawWindowWithTooltipSupport(_centralWindowPosition, "DrawLaunchSiteChooser", "Select Site", DrawLaunchSiteChooser);

                // Only show plans if we don't have a popup
                // or allow overriding if New/ModifyLC is up
                if (_overrideShowBuildPlans || (GUIStates.ShowBuildPlansWindow && !_isKSCLocked))
                    _buildPlansWindowPosition = DrawWindowWithTooltipSupport(_buildPlansWindowPosition, "DrawBuildPlansWindow", "Plans", DrawBuildPlansWindow);

                // both flags can be true when it's necessary to first show ClearLaunch and then Airlaunch right after that
                if (GUIStates.ShowAirlaunch && !GUIStates.ShowClearLaunch)
                    _airlaunchWindowPosition = DrawWindowWithTooltipSupport(_airlaunchWindowPosition, "DrawAirlaunchWindow", "Airlaunch", DrawAirlaunchWindow);

                if (_unlockEditor)
                {
                    EditorLogic.fetch.Unlock("KCTGUILock");
                    _unlockEditor = false;
                }

                if (_inSCSubscene && HighLogic.LoadedScene != GameScenes.SPACECENTER)
                {
                    _inSCSubscene = false;
                }
            }

            //Disable KSC things when certain windows are shown.
            if (GUIStates.ShowFirstRun || GUIStates.ShowRename || GUIStates.ShowNewPad || GUIStates.ShowNewLC || GUIStates.ShowModifyLC || GUIStates.ShowLCResources || GUIStates.ShowDismantleLC || GUIStates.ShowDismantlePad || GUIStates.ShowUpgradeWindow || GUIStates.ShowSettings || GUIStates.ShowCrewSelect || GUIStates.ShowShipRoster || GUIStates.ShowClearLaunch || GUIStates.ShowAirlaunch || GUIStates.ShowLaunchSiteSelector)
            {
                if (!_isKSCLocked)
                {
                    InputLockManager.SetControlLock(ControlTypes.KSC_FACILITIES, SpaceCenterManagement.KCTKSCLock);
                    _isKSCLocked = true;
                }
            }
            else if (_isKSCLocked)
            {
                InputLockManager.RemoveControlLock(SpaceCenterManagement.KCTKSCLock);
                _isKSCLocked = false;
            }

            GUI.skin = oldSkin;
        }

        public static void ClickToggle()
        {
            ToggleVisibility(!GUIStates.IsMainGuiVisible);
        }

        public static void ToggleVisibility(bool isVisible)
        {
            if (SCMEvents.Instance.KCTButtonStockImportant)
                SCMEvents.Instance.KCTButtonStockImportant = false;

            if (HighLogic.LoadedScene == GameScenes.FLIGHT && !IsPrimarilyDisabled)
            {
                BuildListWindowPosition.height = 1;
                GUIStates.ShowBuildList = isVisible;
                GUIStates.ShowBLPlus = false;
                ResetBLWindow();

                if (SpaceCenterManagement.Instance.IsSimulatedFlight && (AirlaunchTechLevel.AnyUnlocked() || AirlaunchTechLevel.AnyUnderResearch()))
                {
                    GUIStates.ShowAirlaunch = isVisible;
                }
                if (SpaceCenterManagement.Instance.IsSimulatedFlight)
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
                SpaceCenterManagement.ShowWindows[1] = isVisible;
            }
            else if ((HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION) && !IsPrimarilyDisabled)
            {
                BuildListWindowPosition.height = 1;
                GUIStates.ShowBuildList = isVisible;
                GUIStates.ShowBuildPlansWindow = false;
                GUIStates.ShowBLPlus = false;
                ResetBLWindow();
                SpaceCenterManagement.ShowWindows[0] = isVisible;
            }

            RefreshToolbarState();
        }

        private static void RefreshToolbarState()
        {
            if (GUIStates.IsMainGuiVisible)
            {
                SpaceCenterManagement.ToolbarControl.SetTrue(false);
            }
            else
            {
                SpaceCenterManagement.ToolbarControl.SetFalse(false);
            }
        }

        public static void RestorePrevUIState()
        {
            if (PrevGUIStates.Count == 0) return;

            GUIStates = PrevGUIStates.Pop();

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                int idx = HighLogic.LoadedScene == GameScenes.SPACECENTER ? 0 : 1;
                SpaceCenterManagement.ShowWindows[idx] = GUIStates.IsMainGuiVisible;
            }

            RefreshToolbarState();
        }

        public static void BackupUIState()
        {
            PrevGUIStates.Push(GUIStates.Clone());
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
            GUIStates.ShowPersonnelWindow = false;
            GUIStates.ShowBLPlus = false;
            GUIStates.ShowRename = false;
            GUIStates.ShowDismantlePad = false;
            GUIStates.ShowDismantleLC = false;
            GUIStates.ShowNewLC = false;
            GUIStates.ShowLCResources = false;
            GUIStates.ShowNewPad = false;
            GUIStates.ShowModifyLC = false;
            GUIStates.ShowFirstRun = false;
            GUIStates.ShowPresetSaver = false;
            GUIStates.ShowLaunchSiteSelector = false;
            GUIStates.ShowAirlaunch = false;
            GUIStates.ShowSimulationGUI = false;
            GUIStates.ShowSimConfig = false;
            GUIStates.ShowSimBodyChooser = false;

            ResetBLWindow();
        }

        public static void ResetFormulaRateHolders()
        {
            _fundsCost = int.MinValue;
            _nodeRate = int.MinValue;
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

        private static GUIStyle GetLabelRightAlignStyle()
        {
            if (_styleLabelRightAlign == null)
            {
                _styleLabelRightAlign = new GUIStyle(GUI.skin.label);
                _styleLabelRightAlign.alignment = TextAnchor.LowerRight;
            }
            return _styleLabelRightAlign;
        }

        private static GUIStyle GetLabelRightAlignStyleYellow()
        {
            if (_styleLabelRightAlignYellow == null)
            {
                _styleLabelRightAlignYellow = new GUIStyle(GUI.skin.label);
                _styleLabelRightAlignYellow.normal.textColor = XKCDColors.KSPMellowYellow;
                _styleLabelRightAlignYellow.hover.textColor = XKCDColors.KSPMellowYellow;
                _styleLabelRightAlignYellow.active.textColor = XKCDColors.KSPMellowYellow;
                _styleLabelRightAlignYellow.focused.textColor = XKCDColors.KSPMellowYellow;
                _styleLabelRightAlignYellow.alignment = TextAnchor.LowerRight;
            }
            return _styleLabelRightAlignYellow;
        }

        private static GUIStyle GetLabelStyleYellow()
        {
            if (_styleLabelYellow == null)
            {
                _styleLabelYellow = new GUIStyle(GUI.skin.label);
                _styleLabelYellow.normal.textColor = XKCDColors.KSPMellowYellow;
                _styleLabelYellow.hover.textColor = XKCDColors.KSPMellowYellow;
                _styleLabelYellow.active.textColor = XKCDColors.KSPMellowYellow;
                _styleLabelYellow.focused.textColor = XKCDColors.KSPMellowYellow;
            }
            return _styleLabelYellow;
        }

        private static GUIStyle GetLabelCenterAlignStyle()
        {
            if (_styleLabelCenterAlign == null)
            {
                _styleLabelCenterAlign = new GUIStyle(GUI.skin.label);
                _styleLabelCenterAlign.alignment = TextAnchor.LowerCenter;
            }
            return _styleLabelCenterAlign;
        }

        private static GUIStyle GetTextFieldRightAlignStyle()
        {
            if (_styleTextFieldRightAlign == null)
            {
                _styleTextFieldRightAlign = new GUIStyle(GUI.skin.textField);
                _styleTextFieldRightAlign.alignment = TextAnchor.LowerRight;
            }
            return _styleTextFieldRightAlign;
        }

        public static void EnterSCSubcene()
        {
            _inSCSubscene = true;
        }
        public static void ExitSCSubcene()
        {
            _inSCSubscene = false;
        }

        public static void OnDestroy()
        {
            _wasShowBuildList = false;
            _overrideShowBuildPlans = false;
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
