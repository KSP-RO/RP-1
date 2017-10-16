﻿using System;
using UnityEngine;
using KSP.UI.Screens;

namespace RP0
{
    class TopWindow : UIBase
    {
        // GUI
        static Rect windowPos = new Rect(500, 240, 0, 0);
        private MaintenanceGUI maintUI = new MaintenanceGUI();
        private ToolingGUI toolUI = new ToolingGUI();
        private Crew.FSGUI fsUI = new RP0.Crew.FSGUI();
        private AvionicsGUI avUI = new AvionicsGUI();

        public void OnGUI()
        {
            windowPos = GUILayout.Window("RP0Top".GetHashCode(), windowPos, DrawWindow, "RP-0");
        }

        private tabs currentTab;
        private void tabSelector()
        {
            GUILayout.BeginHorizontal();
            try {
                if (showTab(tabs.Maintenance) && toggleButton("Maintenance", currentTab == tabs.Maintenance))
                    currentTab = tabs.Maintenance;
                if (showTab(tabs.Facilities) && toggleButton("Facilities", currentTab == tabs.Facilities))
                    currentTab = tabs.Facilities;
                if (showTab(tabs.Integration) && toggleButton("Integration", currentTab == tabs.Integration))
                    currentTab = tabs.Integration;
                if (showTab(tabs.Astronauts) && toggleButton("Astronauts", currentTab == tabs.Astronauts))
                    currentTab = tabs.Astronauts;
                if (showTab(tabs.Tooling) && toggleButton("Tooling", currentTab == tabs.Tooling))
                    currentTab = tabs.Tooling;
                if (showTab(tabs.Training) && toggleButton("Training", currentTab == tabs.Training))
                    currentTab = tabs.Training;
                if (showTab(tabs.Courses) && toggleButton("Courses", currentTab == tabs.Courses))
                    currentTab = tabs.Courses;
                if (showTab(tabs.Avionics) && toggleButton("Avionics", currentTab == tabs.Avionics))
                    currentTab = tabs.Avionics;
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
                    if (showTab(currentTab)) {
                        switch (currentTab) {
                        case tabs.Maintenance:
                            maintUI.summaryTab();
                            break;
                        case tabs.Facilities:
                            maintUI.facilitiesTab();
                            break;
                        case tabs.Integration:
                            maintUI.integrationTab();
                            break;
                        case tabs.Astronauts:
                            maintUI.astronautsTab();
                            break;
                        case tabs.Tooling:
                            currentTab = toolUI.toolingTab();
                            break;
                        case tabs.ToolingType:
                            toolUI.toolingTypeTab();
                            break;
                        case tabs.Training:
                            currentTab = fsUI.summaryTab();
                            break;
                        case tabs.Courses:
                            currentTab = fsUI.coursesTab();
                            break;
                        case tabs.NewCourse:
                            currentTab = fsUI.newCourseTab();
                            break;
                        case tabs.Naut:
                            fsUI.nautTab();
                            break;
                        case tabs.Avionics:
                            avUI.avionicsTab();
                            break;
                        default: // can't happen
                            break;
                        }
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

