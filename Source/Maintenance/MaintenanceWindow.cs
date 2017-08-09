using System;
using UnityEngine;
using KSP.UI.Screens;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    class MaintenanceWindow : MonoBehaviour
    {
        // GUI
        static Rect windowPos = new Rect(500, 240, 0, 0);
        static bool guiEnabled = true;
        private ApplicationLauncherButton button;
        private GUIStyle rightLabel, boldLabel, boldRightLabel, pressedButton;

        protected void Awake()
        {
            rightLabel = new GUIStyle(HighLogic.Skin.label);
            rightLabel.alignment = TextAnchor.MiddleRight;
            boldLabel = new GUIStyle(HighLogic.Skin.label);
            boldLabel.fontStyle = FontStyle.Bold;
            boldRightLabel = new GUIStyle(rightLabel);
            boldRightLabel.fontStyle = FontStyle.Bold;
            pressedButton = new GUIStyle(HighLogic.Skin.button);
            pressedButton.normal = pressedButton.active;
            try {
                GameEvents.onGUIApplicationLauncherReady.Add(this.OnGuiAppLauncherReady);
            } catch (Exception ex) {
                Debug.LogError("RP0 failed to register MaintenanceWindow.OnGuiAppLauncherReady");
                Debug.LogException(ex);
            }
        }

        private void ShowWindow()
        {
            guiEnabled = true;
        }
        private void HideWindow()
        {
            guiEnabled = false;
        }

        private void OnSceneChange(GameScenes s)
        {
            if (s != GameScenes.SPACECENTER)
                HideWindow();
        }

        private void OnGuiAppLauncherReady()
        {
            if (HighLogic.CurrentGame.Mode != global::Game.Modes.CAREER)
                return;
            try {
                button = ApplicationLauncher.Instance.AddModApplication(
                    ShowWindow,
                    HideWindow,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.SPACECENTER,
                    GameDatabase.Instance.GetTexture("RP-0/maintecost", false));
                GameEvents.onGameSceneLoadRequested.Add(this.OnSceneChange);
            } catch (Exception ex) {
                Debug.LogError("RP0 failed to register MaintenanceWindow");
                Debug.LogException(ex);
            }
        }

        public void OnDestroy()
        {
            try {
                GameEvents.onGUIApplicationLauncherReady.Remove(this.OnGuiAppLauncherReady);
                if (button != null)
                    ApplicationLauncher.Instance.RemoveModApplication(button);
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        public void OnGUI()
        {
            if (guiEnabled)
            {
                windowPos = GUILayout.Window("RP0Maintenance".GetHashCode(), windowPos, DrawWindow, "Maintenance Costs");
                Crew.CrewHandler.Instance.fsGUI.SetGUIPositions(Crew.CrewHandler.Instance.fsGUI.DrawGUIs);
            }
        }

        private enum tabs { SUMMARY, Facilities, Integration, Astronauts };
        private tabs currentTab;
        private enum per { DAY, MONTH, YEAR };
        private per displayPer = per.YEAR;

        private double perFactor { get {
            switch (displayPer) {
            case per.DAY:
                return 1d;
            case per.MONTH:
                return 30d;
            case per.YEAR:
                return 365d;
            default: // can't happen
                return 0d;
            }
        }}
        private string perFormat { get {
            if (displayPer == per.DAY)
                return "N1";
            return "N0";
        }}

        private bool toggleButton(string text, bool selected)
        {
            return GUILayout.Button(text, selected ? pressedButton : HighLogic.Skin.button);
        }

        private void tabSelector()
        {
            GUILayout.BeginHorizontal();
            try {
                if (toggleButton("SUMMARY", currentTab == tabs.SUMMARY))
                    currentTab = tabs.SUMMARY;
                if (toggleButton("Facilities", currentTab == tabs.Facilities))
                    currentTab = tabs.Facilities;
                if (toggleButton("Integration", currentTab == tabs.Integration))
                    currentTab = tabs.Integration;
                if (toggleButton("Astronauts", currentTab == tabs.Astronauts))
                    currentTab = tabs.Astronauts;
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        private void perSelector()
        {
            GUILayout.BeginHorizontal();
            try {
                if (toggleButton("Day", displayPer == per.DAY))
                    displayPer = per.DAY;
                if (toggleButton("Month", displayPer == per.MONTH))
                    displayPer = per.MONTH;
                if (toggleButton("Year", displayPer == per.YEAR))
                    displayPer = per.YEAR;
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        private void summaryTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Maintenance costs (per ", HighLogic.Skin.label);
                perSelector();
                GUILayout.Label(")", HighLogic.Skin.label);
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Facilities", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.facilityUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Integration", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.integrationUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Research Teams", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.researchUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Astronauts", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.nautUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Total", boldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.totalUpkeep * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        private void facilitiesTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Facilities costs (per ", HighLogic.Skin.label);
                perSelector();
                GUILayout.Label(")", HighLogic.Skin.label);
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Launch Pads", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.padCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            for (int i = 0; i < MaintenanceHandler.padLevels; i++) {
                if (MaintenanceHandler.Instance.padCosts[i] == 0d)
                    continue;
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label(String.Format("  level {0} × {1}", i, MaintenanceHandler.Instance.kctPadCounts[i]), HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.padCosts[i] * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Runway", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.runwayCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Vertical Assembly Building", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.vabCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Spaceplane Hangar", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.sphCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Research & Development", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.rndCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Mission Control", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.mcCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Tracking Station", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.tsCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Astronaut Complex", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.acCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Total", boldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.facilityUpkeep * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        private void integrationTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Integration Team costs (per ", HighLogic.Skin.label);
                perSelector();
                GUILayout.Label(")", HighLogic.Skin.label);
            } finally {
                GUILayout.EndHorizontal();
            }
            foreach (string site in MaintenanceHandler.Instance.kctBuildRates.Keys) {
                double rate = MaintenanceHandler.Instance.kctBuildRates[site];
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label(site, HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((rate * MaintenanceHandler.Instance.kctBPMult * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Total", boldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.integrationUpkeep * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        private void astronautsTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Astronaut costs (per ", HighLogic.Skin.label);
                perSelector();
                GUILayout.Label(")", HighLogic.Skin.label);
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                int nautCount = HighLogic.CurrentGame.CrewRoster.GetActiveCrewCount();
                GUILayout.Label(String.Format("Corps: {0:N0} astronauts", nautCount), HighLogic.Skin.label, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Cost per astronaut", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.nautYearlyUpkeep * perFactor / 365d).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Total", boldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.nautUpkeep * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }

            // Training
            GUILayout.BeginHorizontal();
            try
            {
                if (toggleButton("Training", RP0.Crew.CrewHandler.Instance.fsGUI.showMain))
                    RP0.Crew.CrewHandler.Instance.fsGUI.showMain = !RP0.Crew.CrewHandler.Instance.fsGUI.showMain;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        public void DrawWindow(int windowID)
        {
            try {
                GUILayout.BeginVertical();
                try {
                    /* If totalUpkeep is zero, we probably haven't calculated the upkeeps yet, so recalculate now */
                    if (MaintenanceHandler.Instance.totalUpkeep == 0d)
                        MaintenanceHandler.Instance.updateUpkeep();

                    tabSelector();
                    switch (currentTab) {
                    case tabs.SUMMARY:
                        summaryTab();
                        break;
                    case tabs.Facilities:
                        facilitiesTab();
                        break;
                    case tabs.Integration:
                        integrationTab();
                        break;
                    case tabs.Astronauts:
                        astronautsTab();
                        break;
                    default: // can't happen
                        break;
                    }
                } finally {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndVertical();
                }
            } finally {
                GUI.DragWindow();
            }
        }
    }
}

