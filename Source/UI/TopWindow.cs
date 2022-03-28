using ClickThroughFix;
using System;
using UnityEngine;

namespace RP0
{
    public class TopWindow : UIBase
    {
        private static Rect _windowPos = new Rect(500, 240, 0, 0);
        private static readonly int _mainWindowId = "RP0Top".GetHashCode();
        private static UITab _currentTab;
        private static bool _shouldResetUISize;

        private readonly MaintenanceGUI _maintUI = new MaintenanceGUI();
        private readonly ToolingGUI _toolUI = new ToolingGUI();
        private readonly Crew.FSGUI _fsUI = new Crew.FSGUI();
        private readonly AvionicsGUI _avUI = new AvionicsGUI();
        private readonly ContractGUI _contractUI = new ContractGUI();
        private readonly CareerLogGUI _logUI = new CareerLogGUI();

        public TopWindow()
        {
            // Reset the tab on scene changes
            _currentTab = HighLogic.LoadedSceneIsEditor ? UITab.Tooling : default;
            _shouldResetUISize = true;
        }

        public void OnGUI()
        {
            if (_shouldResetUISize && Event.current.type == EventType.Layout)
            {
                _windowPos.width = 0;
                _windowPos.height = 0;
                _shouldResetUISize = false;
            }
            _windowPos = ClickThruBlocker.GUILayoutWindow(_mainWindowId, _windowPos, DrawWindow, "RP-1", HighLogic.Skin.window);
            Tooltip.Instance.ShowTooltip(_mainWindowId);
        }

        protected override void OnStart()
        {
            _maintUI.Start();
            _toolUI.Start();
            _fsUI.Start();
            _avUI.Start();
            _contractUI.Start();
            _logUI.Start();
        }

        public static void SwitchTabTo(UITab newTab)
        {
            if (newTab == _currentTab)
                return;
            _currentTab = newTab;
            _shouldResetUISize = true;
        }

        private void UpdateSelectedTab()
        {
            GUILayout.BeginHorizontal();
            if (ShouldShowTab(UITab.Maintenance) && RenderToggleButton("Maintenance", _currentTab == UITab.Maintenance))
                SwitchTabTo(UITab.Maintenance);
            if (ShouldShowTab(UITab.Tooling) && RenderToggleButton("Tooling", _currentTab == UITab.Tooling))
                SwitchTabTo(UITab.Tooling);
            if (ShouldShowTab(UITab.Training) && RenderToggleButton("Astronauts", _currentTab == UITab.Training))
                SwitchTabTo(UITab.Training);
            if (ShouldShowTab(UITab.Courses) && RenderToggleButton("Training", _currentTab == UITab.Courses))
                SwitchTabTo(UITab.Courses);
            if (ShouldShowTab(UITab.Avionics) && RenderToggleButton("Avionics", _currentTab == UITab.Avionics))
                SwitchTabTo(UITab.Avionics);
            if (ShouldShowTab(UITab.Contracts) && RenderToggleButton("Contracts", _currentTab == UITab.Contracts))
                SwitchTabTo(UITab.Contracts);
            if (ShouldShowTab(UITab.CareerLog) && RenderToggleButton("Career Log", _currentTab == UITab.CareerLog))
                SwitchTabTo(UITab.CareerLog);
            GUILayout.EndHorizontal();
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
                        case UITab.Construction:
                            _maintUI.RenderConstructionTab();
                            break;
                        case UITab.Astronauts:
                            _maintUI.RenderAstronautsTab();
                            break;
                        case UITab.Tooling:
                            SwitchTabTo(_toolUI.RenderToolingTab());
                            break;
                        case UITab.ToolingType:
                            _toolUI.RenderTypeTab();
                            break;
                        case UITab.Training:
                            SwitchTabTo(_fsUI.RenderSummaryTab());
                            break;
                        case UITab.Courses:
                            SwitchTabTo(_fsUI.RenderCoursesTab());
                            break;
                        case UITab.NewCourse:
                            SwitchTabTo(_fsUI.RenderNewCourseTab());
                            break;
                        case UITab.Naut:
                            _fsUI.RenderNautTab();
                            break;
                        case UITab.Avionics:
                            _avUI.RenderAvionicsTab();
                            break;
                        case UITab.Contracts:
                            _contractUI.RenderContractsTab();
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

            Tooltip.Instance.RecordTooltip(_mainWindowId);
        }
    }
}
