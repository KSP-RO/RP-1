using System;
using UnityEngine;

namespace RP0
{
    public class MaintenanceGUI : UIBase
    {
        public enum MaintenancePeriod { Day, Month, Year };

        private Vector2 _nautListScroll = new Vector2();
        private readonly GUIContent _infoBtnContent = new GUIContent("ⓘ", "View details");

        private System.Collections.Generic.Dictionary<string, string> siteLocalizer = new System.Collections.Generic.Dictionary<string, string>();

        private string LocalizeSiteName(string siteID)
        {
            if (siteLocalizer.Count == 0)
            {
                foreach (var c in GameDatabase.Instance.GetConfigNodes("KSCSWITCHER"))
                {
                    foreach (var l in c.GetNode("LaunchSites").GetNodes("Site"))
                    {
                        string dName = l.GetValue("displayName");
                        if (!string.IsNullOrEmpty(dName))
                        {
                            if (dName[0] == '#')
                                dName = KSP.Localization.Localizer.Format(dName);

                            siteLocalizer[l.GetValue("name")] = dName;
                        }
                    }
                }
            }

            string val;
            if (siteLocalizer.TryGetValue(siteID, out val))
                return val;

            return siteID;
        }

        private double PeriodFactor
        {
            get
            {
                return MaintenanceHandler.Instance.guiSelectedPeriod switch
                {
                    MaintenancePeriod.Day => 1,
                    MaintenancePeriod.Month => 30,
                    MaintenancePeriod.Year => 365.25,
                    _ => 0,
                };
            }
        }

        private string PeriodDispFormat => MaintenanceHandler.Instance.guiSelectedPeriod == MaintenancePeriod.Day ? "N1" : "N0";

        private void RenderPeriodSelector()
        {
            GUILayout.BeginHorizontal();

            if (RenderToggleButton("Day", MaintenanceHandler.Instance.guiSelectedPeriod == MaintenancePeriod.Day))
                MaintenanceHandler.Instance.guiSelectedPeriod = MaintenancePeriod.Day;
            if (RenderToggleButton("Month", MaintenanceHandler.Instance.guiSelectedPeriod == MaintenancePeriod.Month))
                MaintenanceHandler.Instance.guiSelectedPeriod = MaintenancePeriod.Month;
            if (RenderToggleButton("Year", MaintenanceHandler.Instance.guiSelectedPeriod == MaintenancePeriod.Year))
                MaintenanceHandler.Instance.guiSelectedPeriod = MaintenancePeriod.Year;

            GUILayout.EndHorizontal();
        }

        public void RenderSummaryTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Budget (per ", HighLogic.Skin.label);
            RenderPeriodSelector();
            GUILayout.Label(")", HighLogic.Skin.label);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Facilities", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.FacilityUpkeepPerDay * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                if (GUILayout.Button(_infoBtnContent, InfoButton))
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
                GUILayout.Label((MaintenanceHandler.Instance.IntegrationSalaryPerDay * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                if (GUILayout.Button(_infoBtnContent, InfoButton))
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
                GUILayout.Label("Construction", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.ConstructionSalaryPerDay * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                if (GUILayout.Button(_infoBtnContent, InfoButton))
                {
                    TopWindow.SwitchTabTo(UITab.Construction);
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
                GUILayout.Label((MaintenanceHandler.Instance.ResearchSalaryPerDay * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
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
                GUILayout.Label((MaintenanceHandler.Instance.NautUpkeepPerDay * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                if (GUILayout.Button(_infoBtnContent, InfoButton))
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
            GUILayout.Label("Subsidy", HighLogic.Skin.label, GUILayout.Width(160));
            GUILayout.Label((MaintenanceHandler.Instance.MaintenanceSubsidyPerDay * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            double costPerDay = Math.Max(0, MaintenanceHandler.Instance.TotalUpkeepPerDay - MaintenanceHandler.Instance.MaintenanceSubsidyPerDay);
            GUILayout.Label("Total (after subsidy)", BoldLabel, GUILayout.Width(160));
            GUILayout.Label((costPerDay * PeriodFactor).ToString(PeriodDispFormat), BoldRightLabel, GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            double constrMaterials = KerbalConstructionTime.KCTGameStates.GetConstructionCostOverTime(PeriodFactor * 86400d);
            GUILayout.Label("Building Materials", HighLogic.Skin.label, GUILayout.Width(160));
            GUILayout.Label(constrMaterials.ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Program Budget", HighLogic.Skin.label, GUILayout.Width(160));
            double programBudget = 0d;
            foreach (Programs.Program p in Programs.ProgramHandler.Instance.ActivePrograms)
            {
                programBudget += p.GetFundsForFutureTimestamp(KSPUtils.GetUT() + PeriodFactor * 86400d) - p.GetFundsForFutureTimestamp(KSPUtils.GetUT());
            }
            GUILayout.Label($"+{programBudget.ToString(PeriodDispFormat)}", RightLabel, GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Balance", BoldLabel, GUILayout.Width(160));
            double delta = programBudget - costPerDay * PeriodFactor - constrMaterials;
            GUILayout.Label($"{(delta < 0 ? "-":"+")}{Math.Abs(delta).ToString(PeriodDispFormat)}", BoldRightLabel, GUILayout.Width(160));
            GUILayout.EndHorizontal();
        }

        public void RenderFacilitiesTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Facilities costs (per ", HighLogic.Skin.label);
            RenderPeriodSelector();
            GUILayout.Label(")", HighLogic.Skin.label);
            GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            //try
            //{
            //    GUILayout.Label("Launch Pads", HighLogic.Skin.label, GUILayout.Width(160));
            //    GUILayout.Label((MaintenanceHandler.Instance.PadCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            //}
            //catch (Exception ex)
            //{
            //    Debug.LogException(ex);
            //}
            //GUILayout.EndHorizontal();

            //for (int i = 0; i < MaintenanceHandler.PadLevelCount; i++)
            //{
            //    if (MaintenanceHandler.Instance.PadCosts[i] == 0d)
            //        continue;
            //    GUILayout.BeginHorizontal();
            //    try
            //    {
            //        GUILayout.Label(String.Format("  level {0} × {1}", i + 1, MaintenanceHandler.Instance.KCTPadCounts[i]), HighLogic.Skin.label, GUILayout.Width(160));
            //        GUILayout.Label((MaintenanceHandler.Instance.PadCosts[i] * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            //    }
            //    catch (Exception ex)
            //    {
            //        Debug.LogException(ex);
            //    }
            //    GUILayout.EndHorizontal();
            //}

            //GUILayout.BeginHorizontal();
            //try
            //{
            //    GUILayout.Label("Runway", HighLogic.Skin.label, GUILayout.Width(160));
            //    GUILayout.Label((MaintenanceHandler.Instance.RunwayCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            //}
            //catch (Exception ex)
            //{
            //    Debug.LogException(ex);
            //}
            //GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            //try
            //{
            //    GUILayout.Label("Vertical Assembly Building", HighLogic.Skin.label, GUILayout.Width(160));
            //    GUILayout.Label((MaintenanceHandler.Instance.VabCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            //}
            //catch (Exception ex)
            //{
            //    Debug.LogException(ex);
            //}
            //GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            //try
            //{
            //    GUILayout.Label("Spaceplane Hangar", HighLogic.Skin.label, GUILayout.Width(160));
            //    GUILayout.Label((MaintenanceHandler.Instance.SphCost * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
            //}
            //catch (Exception ex)
            //{
            //    Debug.LogException(ex);
            //}
            //GUILayout.EndHorizontal();

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
                GUILayout.Label((MaintenanceHandler.Instance.FacilityUpkeepPerDay * PeriodFactor).ToString(PeriodDispFormat), BoldRightLabel, GUILayout.Width(160));
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
            GUILayout.Label("Integration Engineer Salaries (per ", HighLogic.Skin.label);
            RenderPeriodSelector();
            GUILayout.Label(")", HighLogic.Skin.label);
            GUILayout.EndHorizontal();

            foreach (var kvp in MaintenanceHandler.Instance.IntegrationSalaries)
            {
                string site = LocalizeSiteName(kvp.Key);
                double engineers = kvp.Value;
                if (engineers == 0)
                    continue;

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label(site, HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((engineers * MaintenanceHandler.Settings.salaryEngineers * PeriodFactor / 365.25d).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
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
                GUILayout.Label((MaintenanceHandler.Instance.IntegrationSalaryPerDay * PeriodFactor).ToString(PeriodDispFormat), BoldRightLabel, GUILayout.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();
        }

        public void RenderConstructionTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Construction Salaries/Materials (per ", HighLogic.Skin.label);
            RenderPeriodSelector();
            GUILayout.Label(")", HighLogic.Skin.label);
            GUILayout.EndHorizontal();

            foreach (var kvp in MaintenanceHandler.Instance.ConstructionSalaries)
            {
                string site = LocalizeSiteName(kvp.Key);
                double engineers = kvp.Value;
                if (engineers == 0)
                    continue;

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label(site, HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((engineers * MaintenanceHandler.Settings.salaryEngineers * PeriodFactor / 365.25d).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("  Materials", HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((KerbalConstructionTime.KCTGameStates.GetConstructionCostOverTime(PeriodFactor * 86400d, kvp.Key)).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
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
                GUILayout.Label((MaintenanceHandler.Instance.ConstructionSalaryPerDay * PeriodFactor).ToString(PeriodDispFormat), BoldRightLabel, GUILayout.Width(160));
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
            GUILayout.Label("Retires NET", HighLogic.Skin.label, GUILayout.Width(120));
            GUILayout.Label("Upkeep", HighLogic.Skin.label, GUILayout.Width(50));
            GUILayout.EndHorizontal();

            for (int i = 0; i < HighLogic.CurrentGame.CrewRoster.Count; ++i)
            {
                var k = HighLogic.CurrentGame.CrewRoster[i];
                double rt;
                if (!Crew.CrewHandler.Instance.KerbalRetireTimes.TryGetValue(k.name, out rt))
                    continue;

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Space(20);
                    GUILayout.Label(k.displayName, HighLogic.Skin.label, GUILayout.Width(144));
                    GUILayout.Label(Crew.CrewHandler.Instance.RetirementEnabled ? KSPUtil.PrintDate(rt, false) : "(n/a)", HighLogic.Skin.label, GUILayout.Width(120));
                    double cost, flightCost;
                    MaintenanceHandler.Instance.GetNautCost(k, out cost, out flightCost);
                    GUILayout.Label(((cost + flightCost) * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(50));
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
                    GUILayout.Label((MaintenanceHandler.Instance.NautBaseUpkeepPerDay * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
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
                    GUILayout.Label((MaintenanceHandler.Instance.NautInFlightUpkeepPerDay * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
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
                    GUILayout.Label((MaintenanceHandler.Instance.TrainingUpkeepPerDay * PeriodFactor).ToString(PeriodDispFormat), RightLabel, GUILayout.Width(160));
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
                    GUILayout.Label((MaintenanceHandler.Instance.NautUpkeepPerDay * PeriodFactor).ToString(PeriodDispFormat), BoldRightLabel, GUILayout.Width(160));
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
