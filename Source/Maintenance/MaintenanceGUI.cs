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

        private string warpToFundsString;

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
                            if (dName[0] == '#') // not using GetStringByTag in case a user screws this up. :)
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

        private string FormatCost(double cost)
        {
            if (cost < 0)
                return $"({(-cost).ToString(PeriodDispFormat)})";
            else if (cost > 0)
                return $"+{cost.ToString(PeriodDispFormat)}";
            else
                return cost.ToString(PeriodDispFormat);
        }

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
            double totalCost = 0d;
            double cost;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Budget (per ");
            RenderPeriodSelector();
            GUILayout.Label(")");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Facilities", UIHolder.Width(160));
                cost = CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepair, -MaintenanceHandler.Instance.FacilityUpkeepPerDay * PeriodFactor);
                cost += CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepairLC, -MaintenanceHandler.Instance.LCsCostPerDay * PeriodFactor);
                totalCost += cost;
                GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
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
                GUILayout.Label("Integration Teams", UIHolder.Width(160));
                cost = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryEngineers, -MaintenanceHandler.Instance.IntegrationSalaryPerDay * PeriodFactor);
                totalCost += cost;
                GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
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
                GUILayout.Label("Research Teams", UIHolder.Width(160));
                cost = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryResearchers, -MaintenanceHandler.Instance.ResearchSalaryPerDay * PeriodFactor);
                totalCost += cost;
                GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Astronauts", UIHolder.Width(160));
                cost = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryCrew, -MaintenanceHandler.Instance.NautBaseUpkeepPerDay * PeriodFactor)
                    + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryCrew, -MaintenanceHandler.Instance.NautInFlightUpkeepPerDay * PeriodFactor);
                totalCost += cost;
                double cost2 = CurrencyUtils.Funds(TransactionReasonsRP0.CrewTraining, -MaintenanceHandler.Instance.TrainingUpkeepPerDay * PeriodFactor);
                totalCost += cost2;
                cost += cost2;
                GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
                if (GUILayout.Button(_infoBtnContent, InfoButton))
                {
                    TopWindow.SwitchTabTo(UITab.AstronautCosts);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Subsidy", UIHolder.Width(160));
            // NOT formatcost since it is not, strictly speaking, a fund gain.
            double subsidy = MaintenanceHandler.GetAverageSubsidyForPeriod(PeriodFactor * 86400d);
            subsidy = CurrencyUtils.Funds(TransactionReasonsRP0.Subsidy, subsidy) * (PeriodFactor / 365.25d);
            GUILayout.Label(subsidy.ToString(PeriodDispFormat), RightLabel, UIHolder.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            totalCost = Math.Min(0, totalCost + subsidy);
            GUILayout.Label("Net (after subsidy)", BoldLabel, UIHolder.Width(160));
            GUILayout.Label(FormatCost(totalCost), BoldRightLabel, UIHolder.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            double rolloutCost = 0d;
            try
            {
                rolloutCost = KerbalConstructionTime.KCTGameStates.GetRolloutCostOverTime(PeriodFactor * 86400d)
                    + KerbalConstructionTime.KCTGameStates.GetAirlaunchCostOverTime(PeriodFactor * 86400d);
                GUILayout.Label("Rollout/Airlaunch Prep", UIHolder.Width(160));
                GUILayout.Label(FormatCost(rolloutCost), RightLabel, UIHolder.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            double constrMaterials = KerbalConstructionTime.KCTGameStates.GetConstructionCostOverTime(PeriodFactor * 86400d);
            GUILayout.Label("Constructions", UIHolder.Width(160));
            GUILayout.Label(FormatCost(constrMaterials), RightLabel, UIHolder.Width(160));
            if (GUILayout.Button(_infoBtnContent, InfoButton))
            {
                TopWindow.SwitchTabTo(UITab.Construction);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Program Budget",UIHolder.Width(160));
            double programBudget = Programs.ProgramHandler.Instance.GetDisplayProgramFunding(PeriodFactor * 86400d);
            GUILayout.Label(FormatCost(programBudget), RightLabel, UIHolder.Width(160));
            if (GUILayout.Button(_infoBtnContent, InfoButton))
            {
                TopWindow.SwitchTabTo(UITab.Programs);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Balance", BoldLabel, UIHolder.Width(160));
            double delta = programBudget + totalCost + constrMaterials + rolloutCost;
            GUILayout.Label(FormatCost(delta), BoldRightLabel, UIHolder.Width(160));
            GUILayout.EndHorizontal();

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER && GUILayout.Button(new GUIContent("Warp to Fund Target", "Warps to the fund target you\nspecify in the resulting dialog")))
            {
                InputLockManager.SetControlLock(ControlTypes.KSC_ALL, "warptofunds");
                UIHolder.Instance.HideWindow();
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new MultiOptionDialog("warpToFunds", "Fund Target", "Warp To Funds", HighLogic.UISkin,
                    new DialogGUITextInput(warpToFundsString, false, 64, (string n) =>
                    {
                        warpToFundsString = n;
                        return warpToFundsString;
                    }, 24f),
                    new DialogGUIButton("Warp", () => { ConfirmWarpDialog(); }),
                    new DialogGUIButton("Cancel", () => { 
                        UIHolder.Instance.ShowWindow();
                        InputLockManager.RemoveControlLock("warptofunds");
                    })
                    ), false, HighLogic.UISkin);
            }
        }

        private void ConfirmWarpDialog()
        {
            if (!double.TryParse(warpToFundsString, out double fundTarget))
            {
                PopupDialog.SpawnPopupDialog(new MultiOptionDialog("warpToFundsConfirmFail",
                    "Failed to parse funds!",
                    "Error",
                    HighLogic.UISkin,
                    300,
                    new DialogGUIButton("Understood", () => {
                        UIHolder.Instance.ShowWindow();
                        InputLockManager.RemoveControlLock("warptofunds");
                    })), false, HighLogic.UISkin);
            }
            else
            {
                if (fundTarget <= Funding.Instance.Funds)
                {
                    UIHolder.Instance.ShowWindow();
                    return;
                }

                KerbalConstructionTime.FundTarget target = new KerbalConstructionTime.FundTarget(fundTarget);
                double time = target.GetTimeLeft();
                if (time < 0d)
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("warpToFundsConfirmFail",
                        $"Failed to find a time to warp to, with a limit of {KSPUtil.PrintDateDeltaCompact(KerbalConstructionTime.FundTarget.MaxTime, false, false)}",
                        "Error",
                        HighLogic.UISkin,
                        300,
                        new DialogGUIButton("Understood", () => {
                            UIHolder.Instance.ShowWindow();
                            InputLockManager.RemoveControlLock("warptofunds");
                        })), false, HighLogic.UISkin);
                }
                else
                {
                    var options = new DialogGUIBase[] {
                        new DialogGUIButton("Yes", () => 
                        {
                            KerbalConstructionTime.KCTWarpController.Create(target);
                            UIHolder.Instance.ShowWindow();
                            InputLockManager.RemoveControlLock("warptofunds");
                        }),
                        new DialogGUIButton("No", () => 
                        { 
                            UIHolder.Instance.ShowWindow();
                            InputLockManager.RemoveControlLock("warptofunds");
                        })
                    };
                    var dialog = new MultiOptionDialog("warpToFundsConfirm", $"Warp? Estimated to take {KSPUtil.PrintDateDelta(time, false, false)} and finish on {KSPUtil.PrintDate(KSPUtils.GetUT() + time, false)}", "Confirm Warp", HighLogic.UISkin, 300, options);
                    PopupDialog.SpawnPopupDialog(dialog, false, HighLogic.UISkin);
                }
            }
        }

        public void RenderFacilitiesTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Facilities costs (per ");
            RenderPeriodSelector();
            GUILayout.Label(")");
            GUILayout.EndHorizontal();

            double grandTotal = 0d;
            foreach (var ksc in KerbalConstructionTime.KCTGameStates.KSCs)
            {
                string site = LocalizeSiteName(ksc.KSCName);
                GUILayout.BeginHorizontal();
                GUILayout.Label(site, BoldLabel, UIHolder.Width(160));
                GUILayout.EndHorizontal();

                double siteTotal = 0d;
                foreach (var lc in ksc.LaunchComplexes)
                {
                    if (!lc.IsOperational)
                        continue;

                    double cost = MaintenanceHandler.Instance.LCUpkeep(lc) * PeriodFactor;
                    cost = CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepairLC, -cost);
                    siteTotal += cost;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"   {lc.Name}", UIHolder.Width(160));
                    GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label(" Total", BoldLabel, UIHolder.Width(160));
                GUILayout.Label(FormatCost(siteTotal), BoldRightLabel, UIHolder.Width(160));
                GUILayout.EndHorizontal();
                grandTotal += siteTotal;
            }

            foreach (var facility in MaintenanceHandler.Instance.FacilitiesForMaintenance)
            {
                if (!MaintenanceHandler.Instance.FacilityMaintenanceCosts.TryGetValue(facility, out double cost))
                    continue;

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label(ScenarioUpgradeableFacilities.GetFacilityName(facility), UIHolder.Width(200));
                    cost = CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepair, -cost * PeriodFactor);
                    GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(120));
                    grandTotal += cost;
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
                GUILayout.Label("Grand Total", BoldLabel, UIHolder.Width(160));
                GUILayout.Label(FormatCost(grandTotal), BoldRightLabel, UIHolder.Width(160));
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
            GUILayout.Label("Integration Teams Cost (per ");
            RenderPeriodSelector();
            GUILayout.Label(")");
            GUILayout.EndHorizontal();

            double grandTotal = 0d;
            foreach (var kvp in MaintenanceHandler.Instance.IntegrationSalaries)
            {
                string site = LocalizeSiteName(kvp.Key);
                double engineers = kvp.Value;
                if (engineers == 0)
                    continue;

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label(site, UIHolder.Width(160));
                    double cost = -engineers * MaintenanceHandler.Settings.salaryEngineers * PeriodFactor / 365.25d;
                    cost = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryEngineers, cost);
                    grandTotal += cost;
                    GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
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
                GUILayout.Label("Total", BoldLabel, UIHolder.Width(160));
                GUILayout.Label(FormatCost(grandTotal), BoldRightLabel, UIHolder.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();
        }

        public void RenderConstructionTab()
        {
            double totalCost = 0d;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Construction Cost (per ");
            RenderPeriodSelector();
            GUILayout.Label(")");
            GUILayout.EndHorizontal();

            foreach (var ksc in KerbalConstructionTime.KCTGameStates.KSCs)
            {
                string site = LocalizeSiteName(ksc.KSCName);
                if (ksc.Constructions.Count == 0)
                    continue;

                GUILayout.BeginHorizontal();
                try
                {
                    double cost = KerbalConstructionTime.KCTGameStates.GetConstructionCostOverTime(PeriodFactor * 86400d, ksc);
                    totalCost += cost;
                    GUILayout.Label(site, UIHolder.Width(160));
                    GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();

                for (int i = 0; i < ksc.Constructions.Count; ++i)
                {
                    var c = ksc.Constructions[i];
                    GUILayout.BeginHorizontal();
                    try
                    {
                        KerbalConstructionTime.Utilities.GetConstructionTooltip(c, i, out string tooltip, out _);
                        GUILayout.Label(new GUIContent($"  {c.GetItemName()}", tooltip), UIHolder.Width(200));
                        GUILayout.Label(FormatCost(c.GetConstructionCostOverTime(PeriodFactor * 86400d)), RightLabel, UIHolder.Width(120));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Total", BoldLabel, UIHolder.Width(160));
                GUILayout.Label(FormatCost(totalCost), BoldRightLabel, UIHolder.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();
        }

        public void RenderProgramTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Active Programs (per ");
            RenderPeriodSelector();
            GUILayout.Label(")");
            GUILayout.EndHorizontal();

            double total = 0d;
            foreach (Programs.Program p in Programs.ProgramHandler.Instance.ActivePrograms)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(p.title, BoldLabel);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label(" Nominal Deadline:", UIHolder.Width(160));
                const double secsPerYear = 365.25d * 86400d;
                GUILayout.Label(KSPUtil.PrintDate(p.acceptedUT + p.DurationYears * secsPerYear, false), RightLabel, UIHolder.Width(160));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label(" Funding:", UIHolder.Width(160));
                double amt = p.GetFundsForFutureTimestamp(KSPUtils.GetUT() + PeriodFactor * 86400d) - p.GetFundsForFutureTimestamp(KSPUtils.GetUT());
                amt = CurrencyUtils.Funds(TransactionReasonsRP0.ProgramFunding, amt);
                total += amt;
                GUILayout.Label(FormatCost(amt), RightLabel, UIHolder.Width(160));
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Total", BoldLabel, UIHolder.Width(160));
            GUILayout.Label(FormatCost(total), BoldRightLabel, UIHolder.Width(160));
            GUILayout.EndHorizontal();
        }

        private void RenderNautList()
        {
            GUILayout.BeginHorizontal();
            UIHolder.Space(20);
            GUILayout.Label("Name", UIHolder.Width(144));
            GUILayout.Label("Retires NET", UIHolder.Width(120));
            GUILayout.Label("Upkeep", UIHolder.Width(50));
            GUILayout.EndHorizontal();
            for (int i = 0; i < HighLogic.CurrentGame.CrewRoster.Count; ++i)
            {
                var k = HighLogic.CurrentGame.CrewRoster[i];
                if (k.rosterStatus == ProtoCrewMember.RosterStatus.Dead || k.rosterStatus == ProtoCrewMember.RosterStatus.Missing ||
                    k.type != ProtoCrewMember.KerbalType.Crew)
                    continue;

                double rt = Crew.CrewHandler.Instance.GetRetireTime(k.name);
                if (rt == 0d)
                    continue;

                GUILayout.BeginHorizontal();
                try
                {
                    UIHolder.Space(20);
                    GUILayout.Label(k.displayName, UIHolder.Width(144));
                    GUILayout.Label(Crew.CrewHandler.Instance.RetirementEnabled ? KSPUtil.PrintDate(rt, false) : "(n/a)", UIHolder.Width(120));
                    double cost, flightCost;
                    MaintenanceHandler.Instance.GetNautCost(k, out cost, out flightCost);
                    cost += flightCost;
                    cost = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryCrew, -cost * PeriodFactor);
                    GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(50));
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
                GUILayout.Label("Astronaut costs (per ");
                RenderPeriodSelector();
                GUILayout.Label(")");
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            try
            {
                int nautCount = HighLogic.CurrentGame.CrewRoster.GetActiveCrewCount();
                GUILayout.Label($"Corps: {nautCount:N0} astronauts", UIHolder.Width(160));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            _nautListScroll = GUILayout.BeginScrollView(_nautListScroll, UIHolder.Width(360), UIHolder.Height(280));
            RenderNautList();
            GUILayout.EndScrollView();

            double total = 0d;
            double cost;
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Astronaut base cost", UIHolder.Width(160));
                    cost = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryCrew, -MaintenanceHandler.Instance.NautBaseUpkeepPerDay * PeriodFactor);
                    total += cost;
                    GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Astronaut operational cost", UIHolder.Width(160));
                    cost = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryCrew, -MaintenanceHandler.Instance.NautInFlightUpkeepPerDay * PeriodFactor);
                    total += cost;
                    GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Astronaut training cost", UIHolder.Width(160));
                    cost = CurrencyUtils.Funds(TransactionReasonsRP0.CrewTraining, -MaintenanceHandler.Instance.TrainingUpkeepPerDay * PeriodFactor);
                    total += cost;
                    GUILayout.Label(FormatCost(cost), RightLabel, UIHolder.Width(160));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Total", BoldLabel, UIHolder.Width(160));
                    GUILayout.Label(FormatCost(total), BoldRightLabel, UIHolder.Width(160));
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
