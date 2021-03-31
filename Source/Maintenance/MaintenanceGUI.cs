﻿using System;
using UnityEngine;

namespace RP0
{
    public class MaintenanceGUI : UIBase
    {
        private enum MaintenancePeriod { Day, Month, Year };

        private Vector2 _nautListScroll = new Vector2();
        private MaintenancePeriod _selectedPeriod = MaintenancePeriod.Year;

        private double PeriodFactor
        {
            get
            {
                return _selectedPeriod switch
                {
                    MaintenancePeriod.Day => 1,
                    MaintenancePeriod.Month => 30,
                    MaintenancePeriod.Year => 365,
                    _ => 0,
                };
            }
        }

        private string PeriodDispFormat => _selectedPeriod == MaintenancePeriod.Day ? "N1" : "N0";

        private void RenderPeriodSelector()
        {
            GUILayout.BeginHorizontal();

            if (RenderToggleButton("Day", _selectedPeriod == MaintenancePeriod.Day))
                _selectedPeriod = MaintenancePeriod.Day;
            if (RenderToggleButton("Month", _selectedPeriod == MaintenancePeriod.Month))
                _selectedPeriod = MaintenancePeriod.Month;
            if (RenderToggleButton("Year", _selectedPeriod == MaintenancePeriod.Year))
                _selectedPeriod = MaintenancePeriod.Year;

            GUILayout.EndHorizontal();
        }

        public void RenderSummaryTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Maintenance costs (per ", HighLogic.Skin.label);
            RenderPeriodSelector();
            GUILayout.Label(")", HighLogic.Skin.label);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Facilities", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.FacilityUpkeep * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                if (GUILayout.Button("ⓘ", InfoButton))
                {
                    TopWindow.SwitchTabTo(UITab.Facilities);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Integration", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.IntegrationUpkeep * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                if (GUILayout.Button("ⓘ", InfoButton))
                {
                    TopWindow.SwitchTabTo(UITab.Integration);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Research Teams", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.ResearchUpkeep * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Astronauts", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.NautTotalUpkeep * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                if (GUILayout.Button("ⓘ", InfoButton))
                {
                    TopWindow.SwitchTabTo(UITab.Astronauts);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Total (after subsidy)", BoldLabel, GUILayout.Width(160));
                GUILayout.Label(((MaintenanceHandler.Instance.TotalUpkeep + MaintenanceHandler.Settings.maintenanceOffset) * PeriodFactor).ToString(PeriodDispFormat), BoldRightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();
        }

        public void RenderFacilitiesTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Facilities costs (per ", HighLogic.Skin.label);
            RenderPeriodSelector();
            GUILayout.Label(")", HighLogic.Skin.label);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Launch Pads", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.PadCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            for (int i = 0; i < MaintenanceHandler.PadLevelCount; i++)
            {
                if (MaintenanceHandler.Instance.PadCosts[i] == 0d)
                    continue;
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label(String.Format("  level {0} × {1}", i + 1, MaintenanceHandler.Instance.KCTPadCounts[i]), HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.PadCosts[i] * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Runway", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.RunwayCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Vertical Assembly Building", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.VabCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Spaceplane Hangar", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.SphCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Research & Development", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.RndCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Mission Control", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.McCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Tracking Station", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.TsCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Astronaut Complex", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.AcCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Total", BoldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.FacilityUpkeep * PeriodFactor).ToString(PeriodDispFormat), BoldRightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();
        }

        public void RenderIntegrationTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Integration Team costs (per ", HighLogic.Skin.label);
            RenderPeriodSelector();
            GUILayout.Label(")", HighLogic.Skin.label);
            GUILayout.EndHorizontal();

            foreach (string site in MaintenanceHandler.Instance.KCTBuildRates.Keys)
            {
                double rate = MaintenanceHandler.Instance.KCTBuildRates[site];
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label(site, HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((rate * MaintenanceHandler.Settings.kctBPMult * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Total", BoldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.IntegrationUpkeep * PeriodFactor).ToString(PeriodDispFormat), BoldRightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();
        }

        private void RenderNautList()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("Name", HighLogic.Skin.label, GUILayout.Width(144));
            GUILayout.Label("Retires NET", HighLogic.Skin.label, GUILayout.Width(160));
            GUILayout.EndHorizontal();

            foreach (string name in Crew.CrewHandler.Instance.KerbalRetireTimes.Keys)
            {
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Space(20);
                    double rt = Crew.CrewHandler.Instance.KerbalRetireTimes[name];
                    GUILayout.Label(name, HighLogic.Skin.label, GUILayout.Width(144));
                    GUILayout.Label(Crew.CrewHandler.Instance.RetirementEnabled ? KSPUtil.PrintDate(rt, false) : "(n/a)", HighLogic.Skin.label, GUILayout.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();
            }
        }

        public void RenderAstronautsTab()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Astronaut costs (per ", HighLogic.Skin.label);
                RenderPeriodSelector();
                GUILayout.Label(")", HighLogic.Skin.label);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            try
            {
                int nautCount = HighLogic.CurrentGame.CrewRoster.GetActiveCrewCount();
                GUILayout.Label($"Corps: {nautCount:N0} astronauts", HighLogic.Skin.label, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            _nautListScroll = GUILayout.BeginScrollView(_nautListScroll, GUILayout.Width(360), GUILayout.Height(280));
            RenderNautList();
            GUILayout.EndScrollView();

            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Astronaut base cost", HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.NautBaseUpkeep * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Astronaut operational cost", HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.NautInFlightUpkeep * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Astronaut training cost", HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.TrainingUpkeep * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Total", BoldLabel, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.NautTotalUpkeep * PeriodFactor).ToString(PeriodDispFormat), BoldRightLabel, GUILayout.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
