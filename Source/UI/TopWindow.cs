using System;
using UnityEngine;

namespace RP0
{
    public class TopWindow : UIBase
    {
        private static Rect _windowPos = new Rect(500, 240, 0, 0);
        private static readonly int _windowId = "RP0Top".GetHashCode();

        private readonly MaintenanceGUI _maintUI = new MaintenanceGUI();
        private readonly ToolingGUI _toolUI = new ToolingGUI();
        private readonly Crew.FSGUI _fsUI = new Crew.FSGUI();
        private readonly AvionicsGUI _avUI = new AvionicsGUI();
        private readonly CareerLogGUI _logUI = new CareerLogGUI();
        private static UITab _currentTab;

        public TopWindow()
        {
            // Reset the tab on scene changes
            _currentTab = default;
        }

        public void OnGUI()
        {
            _windowPos = GUILayout.Window(_windowId, _windowPos, DrawWindow, "RP-1", HighLogic.Skin.window);
        }

        public static void SwitchTabTo(UITab newTab)
        {
            _currentTab = newTab;
        }

        private void UpdateSelectedTab()
        {
            if (ShouldShowTab(UITab.Maintenance) && RenderToggleButton("Maintenance", _currentTab == UITab.Maintenance))
                _currentTab = UITab.Maintenance;
            if (ShouldShowTab(UITab.Tooling) && RenderToggleButton("Tooling", _currentTab == UITab.Tooling))
                _currentTab = UITab.Tooling;
            if (ShouldShowTab(UITab.Training) && RenderToggleButton("Astronauts", _currentTab == UITab.Training))
                _currentTab = UITab.Training;
            if (ShouldShowTab(UITab.Courses) && RenderToggleButton("Courses", _currentTab == UITab.Courses))
                _currentTab = UITab.Courses;
            if (ShouldShowTab(UITab.Avionics) && RenderToggleButton("Avionics", _currentTab == UITab.Avionics))
                _currentTab = UITab.Avionics;
            if (ShouldShowTab(UITab.CareerLog) && RenderToggleButton("Career Log", _currentTab == UITab.CareerLog))
                _currentTab = UITab.CareerLog;
        }

        public void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            try
            {
                // If TotalUpkeep is zero, we probably haven't calculated the upkeeps yet, so recalculate now
                if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER && MaintenanceHandler.Instance.TotalUpkeep == 0)
                    MaintenanceHandler.Instance?.UpdateUpkeep();

                UpdateSelectedTab();
                if (ShouldShowTab(_currentTab))
                {
                    switch (_currentTab)
                    {
                        case UITab.Maintenance:
                            _maintUI.RenderSummaryTab();
                            break;
                        case UITab.Facilities:
                            _maintUI.RenderFacilitiesTab();
                            break;
                        case UITab.Integration:
                            _maintUI.RenderIntegrationTab();
                            break;
                        case UITab.Astronauts:
                            _maintUI.RenderAstronautsTab();
                            break;
                        case UITab.Tooling:
                            _currentTab = _toolUI.RenderToolingTab();
                            break;
                        case UITab.ToolingType:
                            _toolUI.RenderTypeTab();
                            break;
                        case UITab.Training:
                            _currentTab = _fsUI.RenderSummaryTab();
                            break;
                        case UITab.Courses:
                            _currentTab = _fsUI.RenderCoursesTab();
                            break;
                        case UITab.NewCourse:
                            _currentTab = _fsUI.RenderNewCourseTab();
                            break;
                        case UITab.Naut:
                            _fsUI.RenderNautTab();
                            break;
                        case UITab.Avionics:
                            _avUI.RenderAvionicsTab();
                            break;
                        case UITab.CareerLog:
                            _logUI.RenderTab();
                            break;
                        default:    // can't happen
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }
    }
}
