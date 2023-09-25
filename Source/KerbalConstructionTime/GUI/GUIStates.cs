﻿namespace RP0
{
    public class GUIStates
    {
        public bool ShowEditorGUI;
        public bool ShowBuildList;
        public bool ShowSimulationGUI;
        public bool ShowSimConfig;
        public bool ShowSimBodyChooser;
        public bool ShowClearLaunch;
        public bool ShowShipRoster;
        public bool ShowCrewSelect;
        public bool ShowSettings;
        public bool ShowUpgradeWindow;
        public bool ShowPersonnelWindow;
        public bool ShowBLPlus;
        public bool ShowNewPad;
        public bool ShowNewLC;
        public bool ShowModifyLC;
        public bool ShowLCResources;
        public bool ShowDismantleLC;
        public bool ShowRename;
        public bool ShowDismantlePad;
        public bool ShowFirstRun;
        public bool ShowLaunchSiteSelector;
        public bool ShowBuildPlansWindow;
        public bool ShowAirlaunch;
        public bool ShowPresetSaver;

        public bool IsMainGuiVisible => ShowBuildList || ShowEditorGUI;

        public GUIStates Clone()
        {
            return (GUIStates)MemberwiseClone();
        }

        public void HideAllNonMainWindows()
        {
            ShowSimConfig = ShowSimBodyChooser = ShowSimulationGUI = 
            ShowClearLaunch = ShowShipRoster = ShowCrewSelect = ShowSettings = ShowDismantleLC =
            ShowUpgradeWindow = ShowPersonnelWindow = ShowBLPlus = ShowNewPad = ShowModifyLC =
            ShowNewLC = ShowLCResources = ShowRename = ShowDismantlePad = ShowFirstRun = ShowLaunchSiteSelector = 
            ShowBuildPlansWindow = ShowAirlaunch = ShowPresetSaver = false;
        }
    }
}
