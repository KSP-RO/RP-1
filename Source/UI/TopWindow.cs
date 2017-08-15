using System;
using UnityEngine;
using KSP.UI.Screens;

namespace RP0
{
    class TopWindow : UIBase
    {
        // GUI
        static Rect windowPos = new Rect(500, 240, 0, 0);

        private MaintenanceGUI maintUI;

        public TopWindow() : base()
        {
            maintUI = new MaintenanceGUI(this);
        }

        public void OnGUI()
        {
            windowPos = GUILayout.Window("RP0Top".GetHashCode(), windowPos, DrawWindow, "RP-0");
            Crew.CrewHandler.Instance.fsGUI.SetGUIPositions(Crew.CrewHandler.Instance.fsGUI.DrawGUIs);
        }
        public tabs currentTab;

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
                if (toggleButton("Tooling", currentTab == tabs.Tooling)) {
                    currentTab = tabs.Tooling;
                    maintUI.currentToolingType = null;
                }
            } finally {
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
                    case UIBase.tabs.SUMMARY:
                        maintUI.summaryTab();
                        break;
                    case UIBase.tabs.Facilities:
                        maintUI.facilitiesTab();
                        break;
                    case UIBase.tabs.Integration:
                        maintUI.integrationTab();
                        break;
                    case UIBase.tabs.Astronauts:
                        maintUI.astronautsTab();
                        break;
                    case UIBase.tabs.Tooling:
                        maintUI.toolingTab();
                        break;
                    case UIBase.tabs.ToolingType:
                        maintUI.toolingTypeTab();
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

