﻿using System;
using UnityEngine;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private static Rect _personnelPosition = new Rect((Screen.width - 450) / 2, Screen.height / 4, 450, 1);
        private static int _personnelWindowHolder = 0;
        private static double _fundsCost = int.MinValue;
        private static double _nodeRate = int.MinValue;
        private static int _buyModifier;
        public static int _LCIndex = 0;
        private static GUIStyle _cannotAffordStyle;
        private static readonly int[] _buyModifierMultsPersonnel = { 1, 10, 100, int.MaxValue };
        private enum PersonnelButtonHover { None, Hire, Fire, Assign, Unassign };
        private static PersonnelButtonHover _currentPersonnelHover = PersonnelButtonHover.None;

        private static void DrawPersonnelWindow(int windowID)
        {
            int oldByModifier = _buyModifier;
            if (Input.GetKey(KeyCode.LeftShift))
            {
                _buyModifier = _buyModifierMultsPersonnel[1];
            }
            else if (Input.GetKey(KeyCode.LeftControl))
            {
                _buyModifier = _buyModifierMultsPersonnel[2];
            }
            else if (GameSettings.MODIFIER_KEY.GetKey())
            {
                _buyModifier = _buyModifierMultsPersonnel[3];
            }
            else
            {
                _buyModifier = _buyModifierMultsPersonnel[0];
            }
            bool isCostCacheInvalid = _buyModifier != oldByModifier;

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Applicants:", GUILayout.Width(120));
            GUILayout.Label(KerbalConstructionTimeData.Instance.Applicants.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            double salaryE = -CurrencyUtils.Funds(TransactionReasonsRP0.SalaryEngineers, -MaintenanceHandler.Instance.IntegrationSalaryPerDay * 365.25d);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Engineers:", GUILayout.Width(120));
            GUILayout.Label(KerbalConstructionTimeData.Instance.TotalEngineers.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.Width(60));
            GUILayout.Label("Salary and Facilities:", GetLabelRightAlignStyle(), GUILayout.Width(150));
            GUILayout.Label($"√{salaryE:N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            double salaryR = -CurrencyUtils.Funds(TransactionReasonsRP0.SalaryResearchers, -MaintenanceHandler.Instance.ResearchSalaryPerDay * 365.25d);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Researchers:", GUILayout.Width(120));
            GUILayout.Label(KerbalConstructionTimeData.Instance.Researchers.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.Width(60));
            GUILayout.Label("Salary and Facilities:", GetLabelRightAlignStyle(), GUILayout.Width(150));
            GUILayout.Label($"√{salaryR:N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Engineers")) { _personnelWindowHolder = 0; _personnelPosition.height = 1; }
            if (KSPUtils.CurrentGameHasScience() && GUILayout.Button("Researchers")) { _personnelWindowHolder = 2; _personnelPosition.height = 1; }
            GUILayout.EndHorizontal();

            if (_personnelWindowHolder == 0)    //VAB
            {
                RenderEngineersSection(isCostCacheInvalid);
            }

            if (_personnelWindowHolder == 2)    //R&D
            {
                RenderResearchersSection(isCostCacheInvalid);
            }

            GUILayout.Label($"Hold LeftShift for x10, LeftCtrl for x100, and {GameSettings.MODIFIER_KEY.primary} for Max Possible", GetLabelCenterAlignStyle());

            if (GUILayout.Button("Close"))
            {
                GUIStates.ShowPersonnelWindow = false;
                _LCIndex = KerbalConstructionTimeData.Instance.ActiveSC.LCIndex; // reset to current active LC
            }
            GUILayout.EndVertical();
            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
        }

        private static void RenderEngineersSection(bool isCostCacheInvalid)
        {
            SpaceCenter KSC = KerbalConstructionTimeData.Instance.ActiveSC;
            LaunchComplex currentLC = KSC.LaunchComplexes[_LCIndex];

            GUILayout.BeginHorizontal();
            GUILayout.Label("Engineers:", GUILayout.Width(100));
            GUILayout.Label(KSC.Engineers.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.Width(100));
            GUILayout.Label($"Unassigned:", GetLabelRightAlignStyle(), GUILayout.Width(100));
            GUILayout.Label($"{KSC.UnassignedEngineers}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            RenderHireFire(false, out int fireAmount, out int hireAmount);

            GUILayout.BeginHorizontal();
            int lcCount = KSC.LaunchComplexCount;
            if (lcCount > 1)
            {
                int idx = KSC.GetLaunchComplexIdxToSwitchTo(forwardDirection: false, padOnly: false, _LCIndex);
                if (GUILayout.Button($"<<{KSC.LaunchComplexes[idx].Name}", GUILayout.ExpandWidth(false))) { _LCIndex = idx; }
            }
            GUILayout.Label(currentLC.IsRushing ? $"{currentLC.Name} (rushing)" : currentLC.Name, GetLabelCenterAlignStyle());
            if (lcCount > 1)
            {
                int idx = KSC.GetLaunchComplexIdxToSwitchTo(forwardDirection: true, padOnly: false, _LCIndex);
                if (GUILayout.Button($"{KSC.LaunchComplexes[idx].Name}>>", GUILayout.ExpandWidth(false))) { _LCIndex = idx; }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Assigned:");
            
            string assignStr = GetAssignText(true, currentLC, out int assignAmt);
            string unassignStr = GetAssignText(false, currentLC, out int unassignAmt);

            bool recalc = false;
            ProjectType type = currentLC.LCType == LaunchComplexType.Pad ? ProjectType.VAB : ProjectType.SPH;
            if (GUILayout.Button(unassignStr, GUILayout.ExpandWidth(false)) && unassignAmt > 0) { KCTUtilities.ChangeEngineers(currentLC, -unassignAmt); recalc = true; }
            if (Event.current.type == EventType.Repaint)
            {
                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    _currentPersonnelHover = PersonnelButtonHover.Unassign;
                else if (_currentPersonnelHover == PersonnelButtonHover.Unassign)
                    _currentPersonnelHover = PersonnelButtonHover.None;
            }

            GUILayout.Label($"  {currentLC.Engineers:N0}  ", GetLabelCenterAlignStyle(), GUILayout.ExpandWidth(false));

            if (GUILayout.Button(assignStr, GUILayout.ExpandWidth(false)) && assignAmt > 0) { KCTUtilities.ChangeEngineers(currentLC, assignAmt); recalc = true; }
            if (Event.current.type == EventType.Repaint)
            {
                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    _currentPersonnelHover = PersonnelButtonHover.Assign;
                else if (_currentPersonnelHover == PersonnelButtonHover.Assign)
                    _currentPersonnelHover = PersonnelButtonHover.None;
            }

            GUILayout.Label($"Max: {currentLC.MaxEngineers:N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            if (recalc)
            {
                currentLC.RecalculateBuildRates();
                currentLC.KSC.RecalculateBuildRates(false);
            }

            int assignDelta = 0;
            if (_currentPersonnelHover == PersonnelButtonHover.Assign)
                assignDelta = assignAmt;
            else if (_currentPersonnelHover == PersonnelButtonHover.Unassign)
                assignDelta = -unassignAmt;
            int freeDelta = 0;
            switch (_currentPersonnelHover)
            {
                case PersonnelButtonHover.Assign: freeDelta = -assignAmt; break;
                case PersonnelButtonHover.Unassign: freeDelta = unassignAmt; break;
                case PersonnelButtonHover.Hire: freeDelta = hireAmount; break;
                case PersonnelButtonHover.Fire: freeDelta = -fireAmount; break;
            }

            double efficiency = currentLC.Efficiency;
            double stratMult = currentLC.StrategyRateMultiplier;
            const string HangarEfficTooltip = "The hangar has no specific efficiency, and instead uses the maximum possible efficiency at your current tech level.";
            const string PadEfficTooltip = "LC efficiency increases as the LC is used, proportional to the number of engineers vs the max, and lowers only if the complex is modified. Efficiency will not increase if the LC is rushing.";
            GUILayout.BeginHorizontal();
            if (currentLC.LCType == LaunchComplexType.Hangar)
            {
                GUILayout.Label(new GUIContent("Efficiency:", HangarEfficTooltip));
                GUILayout.Label(new GUIContent(LCEfficiency.MaxEfficiency.ToString("P1"), HangarEfficTooltip), GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label(new GUIContent($"Efficiency: ({LCEfficiency.MinEfficiency:P0} - {LCEfficiency.MaxEfficiency:P0})", PadEfficTooltip));
                GUILayout.Label(new GUIContent(efficiency.ToString("P1"), PadEfficTooltip), GetLabelRightAlignStyle());
            }
            GUILayout.EndHorizontal();

            double rateFull = KCTUtilities.GetBuildRate(0, type, currentLC, currentLC.IsHumanRated, assignDelta) * stratMult;
            double rate = rateFull * efficiency;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Vessel Rate: {rateFull:N3} => {rate:N3} BP/sec", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (currentLC.CanIntegrate && currentLC.BuildList.Count > 0)
            {
                VesselProject b = currentLC.BuildList[0];
                GUILayout.Label($"Current Vessel: {b.shipName}");

                int engCap = currentLC.MaxEngineersFor(b);
                if (engCap != currentLC.MaxEngineers)
                    GUILayout.Label($"(max of {engCap} eng.)");

                int delta = assignDelta;
                if (engCap < currentLC.Engineers + assignDelta)
                    delta = engCap - currentLC.Engineers;
                double buildRate = KCTUtilities.GetBuildRate(0, b.Type, currentLC, b.humanRated, delta)
                    * efficiency * stratMult;
                double bpLeft = b.buildPoints + b.integrationPoints - b.progress;
                GUILayout.Label(DTUtils.GetColonFormattedTimeWithTooltip(bpLeft / buildRate, "PersonnelVessel"), GetLabelRightAlignStyle());
            }
            else
            {
                LCOpsProject lcp = LCOpsProject.GetFirstCompleting(currentLC);
                if (lcp != null)
                {
                    int engCap = lcp.IsCapped ? currentLC.MaxEngineersFor(lcp.mass, lcp.vesselBP, lcp.isHumanRated) : int.MaxValue;
                    GUILayout.Label($"Current Project: {lcp.Name} {(lcp.AssociatedBLV == null ? string.Empty : lcp.AssociatedBLV.shipName)}");
                    
                    int delta = assignDelta;
                    if (engCap < currentLC.Engineers + assignDelta)
                        delta = engCap - currentLC.Engineers;
                    if (engCap < int.MaxValue && engCap != currentLC.MaxEngineers)
                        GUILayout.Label($"(max of {engCap} eng.)");

                    double buildRate = lcp.GetBuildRate(delta);
                    double bpLeft = (lcp.IsReversed ? 0 : lcp.BP) - lcp.progress;
                    GUILayout.Label(DTUtils.GetColonFormattedTimeWithTooltip(bpLeft / buildRate, "PersonnelVessel"), GetLabelRightAlignStyle());
                }
                else
                {
                    GUILayout.Label($"No current projects at {currentLC.Name}");
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void RenderResearchersSection(bool isCostCacheInvalid)
        {
            if (_currentPersonnelHover == PersonnelButtonHover.Assign
                || _currentPersonnelHover == PersonnelButtonHover.Unassign)
                _currentPersonnelHover = PersonnelButtonHover.None;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Researchers:", GUILayout.Width(90));
            GUILayout.Label(KerbalConstructionTimeData.Instance.Researchers.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Max:", GUILayout.Width(90));
            int resLimit = Database.SettingsSC.GetResearcherCap();
            string resLimitStr = resLimit >= 0 ? resLimit.ToString("N0") : "Unlimited";
            GUILayout.Label(resLimitStr, GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            RenderHireFire(true, out int fireAmount, out int hireAmount);

            int delta = 0;
            if (_currentPersonnelHover == PersonnelButtonHover.Hire)
                delta = hireAmount;
            else if (_currentPersonnelHover == PersonnelButtonHover.Fire)
                delta = -fireAmount;

            double efficiency = Database.SettingsSC.ResearcherEfficiency;
            double days = GameSettings.KERBIN_TIME ? 4 : 1;

            _nodeRate = Formula.GetResearchRate(0, 0, delta);
            double sci = 86400 * _nodeRate;
            double sciPerDay = sci / days;
            double sciPerDayEffic = sciPerDay * efficiency;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rate: ", GetLabelRightAlignStyle());
            //bool usingPerYear = false;
            if (sciPerDay > 0.1 && sciPerDayEffic > 0.1)
            {
                GUILayout.Label($"{sciPerDay:N3} => {sciPerDayEffic:N3} sci/day", GetLabelRightAlignStyle());
            }
            else
            {
                //Well, looks like we need sci/year instead
                int daysPerYear = KSPUtil.dateTimeFormatter.Year / KSPUtil.dateTimeFormatter.Day;
                GUILayout.Label($"{(sciPerDay * daysPerYear):N3} => {(sciPerDayEffic * daysPerYear):N3} sci/yr", GetLabelRightAlignStyle());
                //usingPerYear = true;
            }
            GUILayout.EndHorizontal();

            
            GUILayout.BeginHorizontal();
            const string researcherEfficTooltip = "Researching new Electronics Research nodes and gathering more science will increase this";
            GUILayout.Label(new GUIContent("Efficiency:", researcherEfficTooltip));
            GUILayout.Label(new GUIContent($"{efficiency:P1}", researcherEfficTooltip), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (KerbalConstructionTimeData.Instance.TechList.Count > 0)
            {
                ResearchProject t = KerbalConstructionTimeData.Instance.TechList[0];
                GUILayout.Label($"Current Research: {t.techName}");
                double techRate = Formula.GetResearchRate(t.scienceCost, 0, delta) * efficiency * t.YearBasedRateMult;
                double timeLeft = (t.scienceCost - t.progress) / techRate;
                GUILayout.Label(DTUtils.GetColonFormattedTimeWithTooltip(timeLeft, "PersonnelTech"), GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label("No current research");
            }
            GUILayout.EndHorizontal();
        }

        private static void RenderHireFire(bool research, out int fireAmount, out int hireAmount)
        {
            if (KSPUtils.CurrentGameIsCareer())
            {
                GUILayout.BeginHorizontal();

                string title = research ? "Researchers" : "Engineers";
                GUILayout.Label($"Hire/Fire {title}:");

                fireAmount = research ? KerbalConstructionTimeData.Instance.Researchers : KerbalConstructionTimeData.Instance.ActiveSC.UnassignedEngineers;
                int workers = _buyModifier;
                if (workers == int.MaxValue)
                    workers = fireAmount;

                bool canAfford = workers <= fireAmount;
                GUIStyle style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                if (GUILayout.Button($"Fire {workers:N0}", style, GUILayout.ExpandWidth(false)) && canAfford)
                {
                    if (research)
                    {
                        KCTUtilities.ChangeResearchers(-workers);
                        KerbalConstructionTimeData.Instance.UpdateTechTimes();
                    }
                    else
                    {
                        SpaceCenter ksc = KerbalConstructionTimeData.Instance.ActiveSC;
                        KCTUtilities.ChangeEngineers(ksc, -workers);
                        ksc.RecalculateBuildRates(false);
                    }
                }
                if (Event.current.type == EventType.Repaint)
                {
                    if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                        _currentPersonnelHover = PersonnelButtonHover.Fire;
                    else if (_currentPersonnelHover == PersonnelButtonHover.Fire)
                        _currentPersonnelHover = PersonnelButtonHover.None;
                }
                fireAmount = Math.Min(workers, fireAmount);

                double modifiedHireCost = -CurrencyUtils.Funds(research ? TransactionReasonsRP0.HiringResearchers : TransactionReasonsRP0.HiringEngineers, -Database.SettingsSC.HireCost);
                workers = _buyModifier;
                if (workers == int.MaxValue)
                    workers = Math.Max(_buyModifierMultsPersonnel[0], KerbalConstructionTimeData.Instance.Applicants + (int)(Funding.Instance.Funds / modifiedHireCost));

                if (research)
                {
                    int maxRes = Database.SettingsSC.GetResearcherCap();
                    if (maxRes < 0)
                        maxRes = int.MaxValue;

                    workers = Math.Max(0, Math.Min(workers, maxRes - KerbalConstructionTimeData.Instance.Researchers));
                }

                double workersToHire = Math.Max(0, workers - KerbalConstructionTimeData.Instance.Applicants);
                _fundsCost = modifiedHireCost * workersToHire;
                // Show the result for whatever you're asking for, even if you can't afford it.
                hireAmount = workers; // Math.Min(workers, (int)(Funding.Instance.Funds / Database.SettingsSC.HireCost) + KerbalConstructionTimeData.Instance.UnassignedPersonnel);

                canAfford = Funding.Instance.Funds >= _fundsCost;
                style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                if (GUILayout.Button($"Hire {workers:N0}: √{_fundsCost:N0}", style, GUILayout.ExpandWidth(false)) && canAfford)
                {
                    // Note: have to pass base, not modified, cost here, since the CMQ reruns
                    KCTUtilities.SpendFunds(workersToHire * Database.SettingsSC.HireCost, research ? TransactionReasonsRP0.HiringResearchers : TransactionReasonsRP0.HiringEngineers);
                    if (research)
                    {
                        KCTUtilities.ChangeResearchers(workers);
                        KerbalConstructionTimeData.Instance.UpdateTechTimes();
                    }
                    else
                    {
                        SpaceCenter ksc = KerbalConstructionTimeData.Instance.ActiveSC;
                        KCTUtilities.ChangeEngineers(ksc, workers);
                        ksc.RecalculateBuildRates(false);
                    }
                    KerbalConstructionTimeData.Instance.Applicants = Math.Max(0, KerbalConstructionTimeData.Instance.Applicants - workers);
                    if (KerbalConstructionTimeData.Instance.Applicants == 0)
                        KerbalConstructionTimeData.Instance.HiredStarterApplicants = true;

                    _fundsCost = int.MinValue;
                }
                if (Event.current.type == EventType.Repaint)
                {
                    if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                        _currentPersonnelHover = PersonnelButtonHover.Hire;
                    else if (_currentPersonnelHover == PersonnelButtonHover.Hire)
                        _currentPersonnelHover = PersonnelButtonHover.None;
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                hireAmount = 0;
                fireAmount = 0;
            }
        }

        private static string GetAssignText(bool add, LaunchComplex currentLC, out int mod)
        {
            string signChar;
            int limit;
            mod = _buyModifierMultsPersonnel[0]; // default
            if (add)
            {
                signChar = "+";
                limit = Math.Min(currentLC.KSC.UnassignedEngineers, currentLC.MaxEngineers - currentLC.Engineers);
            }
            else
            {
                signChar = "-";
                limit = currentLC.Engineers;
            }
            for (int i = 0; i < _buyModifierMultsPersonnel.Length; ++i)
            {
                if (_buyModifierMultsPersonnel[i] != _buyModifier)
                    continue;

                mod = Math.Min(limit, _buyModifier);
                break;
            }
            return $"{signChar}{mod:N0}";
        }

        private static GUIStyle GetCannotAffordStyle()
        {
            if (_cannotAffordStyle == null)
            {
                _cannotAffordStyle = new GUIStyle(GUI.skin.button);
                _cannotAffordStyle.normal.textColor = Color.red;
                _cannotAffordStyle.active.textColor = _cannotAffordStyle.normal.textColor;
                _cannotAffordStyle.hover.textColor = _cannotAffordStyle.normal.textColor;
            }
            return _cannotAffordStyle;
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
