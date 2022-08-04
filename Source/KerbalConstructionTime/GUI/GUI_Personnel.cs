using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace KerbalConstructionTime
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
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                _buyModifier = _buyModifierMultsPersonnel[1];
            }
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                _buyModifier = _buyModifierMultsPersonnel[2];
            }
            else if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
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
            GUILayout.Label(KCTGameStates.UnassignedPersonnel.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            int tE = KCTGameStates.TotalEngineers;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Engineers:", GUILayout.Width(120));
            GUILayout.Label(tE.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.Width(60));
            GUILayout.Label("Salary and Facilities:", GetLabelRightAlignStyle(), GUILayout.Width(150));
            GUILayout.Label($"√{KCTGameStates.GetSalaryEngineers():N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Researchers:", GUILayout.Width(120));
            GUILayout.Label(KCTGameStates.Researchers.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.Width(60));
            GUILayout.Label("Salary and Facilities:", GetLabelRightAlignStyle(), GUILayout.Width(150));
            GUILayout.Label($"√{KCTGameStates.GetSalaryResearchers():N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Engineers")) { _personnelWindowHolder = 0; _personnelPosition.height = 1; }
            if (Utilities.CurrentGameHasScience() && GUILayout.Button("Researchers")) { _personnelWindowHolder = 2; _personnelPosition.height = 1; }
            GUILayout.EndHorizontal();

            if (_personnelWindowHolder == 0)    //VAB
            {
                RenderEngineersSection(isCostCacheInvalid);
            }

            if (_personnelWindowHolder == 2)    //R&D
            {
                RenderResearchersSection(isCostCacheInvalid);
            }

            if (GUILayout.Button("Close"))
            {
                GUIStates.ShowPersonnelWindow = false;
                _LCIndex = KCTGameStates.ActiveKSC.ActiveLaunchComplexIndex; // reset to current active LC
            }
            GUILayout.EndVertical();
            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
        }

        private static void RenderEngineersSection(bool isCostCacheInvalid)
        {
            KSCItem KSC = KCTGameStates.ActiveKSC;
            LCItem currentLC = KSC.LaunchComplexes[_LCIndex];

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
                int idx = KSC.SwitchLaunchComplex(false, _LCIndex, false);
                if (GUILayout.Button($"<<{KSC.LaunchComplexes[idx].Name}", GUILayout.ExpandWidth(false))) { _LCIndex = idx; }
            }
            GUILayout.Label(currentLC.IsRushing ? $"{currentLC.Name} (rushing)" : currentLC.Name, GetLabelCenterAlignStyle());
            if (lcCount > 1)
            {
                int idx = KSC.SwitchLaunchComplex(true, _LCIndex, false);
                if (GUILayout.Button($"{KSC.LaunchComplexes[idx].Name}>>", GUILayout.ExpandWidth(false))) { _LCIndex = idx; }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Assigned:");
            
            string assignStr = GetAssignText(true, currentLC, out int assignAmt);
            string unassignStr = GetAssignText(false, currentLC, out int unassignAmt);

            bool recalc = false;
            BuildListVessel.ListType type = currentLC.LCType == LaunchComplexType.Pad ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH;
            if (GUILayout.Button(unassignStr, GUILayout.ExpandWidth(false)) && unassignAmt > 0) { Utilities.ChangeEngineers(currentLC, -unassignAmt); recalc = true; }
            if (Event.current.type == EventType.Repaint)
            {
                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    _currentPersonnelHover = PersonnelButtonHover.Unassign;
                else if (_currentPersonnelHover == PersonnelButtonHover.Unassign)
                    _currentPersonnelHover = PersonnelButtonHover.None;
            }

            GUILayout.Label($"  {currentLC.Engineers:N0}  ", GetLabelCenterAlignStyle(), GUILayout.ExpandWidth(false));

            if (GUILayout.Button(assignStr, GUILayout.ExpandWidth(false)) && assignAmt > 0) { Utilities.ChangeEngineers(currentLC, assignAmt); recalc = true; }
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

            double efficLocal = _currentPersonnelHover == PersonnelButtonHover.Assign ? Utilities.PredictEfficiencyEngineers(currentLC, assignDelta) : currentLC.EfficiencyEngineers;
            double efficGlobal = _currentPersonnelHover == PersonnelButtonHover.Hire ? Utilities.PredictEfficiencyEngineers(freeDelta) : KCTGameStates.EfficiencyEngineers;
            double techMult = PresetManager.Instance.ActivePreset.GeneralSettings.EngineerEfficiencyMultiplier;
            double stratMult = currentLC.StrategyRateMultiplier;
            const string efficTooltip = "Adding new engineers here will temporarily lower local efficiency.\nHiring new engineers will temporarily lower global efficiency.";
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent($"Efficiency: {efficLocal:P1} (at {currentLC.Name}) x {efficGlobal:P1} (global) x {techMult:N2} (tech)", efficTooltip));
            GUILayout.EndHorizontal();

            double rateFull = Utilities.GetBuildRate(0, type, currentLC, currentLC.IsHumanRated, assignDelta) * techMult * stratMult;
            double rate = rateFull * efficLocal * efficGlobal;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Vessel Rate: {rateFull:N3} => {rate:N3} BP/sec", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (currentLC.BuildList.Count > 0)
            {
                BuildListVessel b = currentLC.BuildList[0];
                GUILayout.Label($"Current Vessel: {b.ShipName}");

                int engCap = currentLC.MaxEngineersFor(b);
                if (engCap != currentLC.MaxEngineers)
                    GUILayout.Label($"(max of {engCap} eng.)");

                int delta = assignDelta;
                if (engCap < currentLC.Engineers + assignDelta)
                    delta = engCap - currentLC.Engineers;
                double buildRate = Utilities.GetBuildRate(0, b.Type, currentLC, b.IsHumanRated, delta)
                    * efficLocal * efficGlobal * techMult * stratMult;
                double bpLeft = b.BuildPoints + b.IntegrationPoints - b.Progress;
                GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(bpLeft / buildRate, "PersonnelVessel"), GetLabelRightAlignStyle());
            }
            else
            {
                LCProject lcp = null;
                foreach (var r in currentLC.Recon_Rollout)
                {
                    if (!r.IsComplete() && (lcp == null || lcp.GetTimeLeft() < r.GetTimeLeft()))
                        lcp = r;
                }
                foreach (var a in currentLC.AirlaunchPrep)
                {
                    if (!a.IsComplete() && (lcp == null || lcp.GetTimeLeft() < a.GetTimeLeft()))
                        lcp = a;
                }
                if (lcp != null)
                {
                    int engCap = lcp.IsCapped ? currentLC.MaxEngineersFor(lcp.Mass, lcp.VesselBP, lcp.IsHumanRated) : int.MaxValue;
                    GUILayout.Label($"Current Project: {lcp.Name} {(lcp.AssociatedBLV == null ? string.Empty : lcp.AssociatedBLV.ShipName)}");
                    
                    int delta = assignDelta;
                    if (engCap < currentLC.Engineers + assignDelta)
                        delta = engCap - currentLC.Engineers;
                    if (engCap < int.MaxValue && engCap != currentLC.MaxEngineers)
                        GUILayout.Label($"(max of {engCap} eng.)");

                    double buildRate = lcp.GetBuildRate(delta) 
                        / (currentLC.EfficiencyEngineers * KCTGameStates.EfficiencyEngineers)
                        * efficLocal * efficGlobal;
                    double bpLeft = (lcp.IsReversed ? 0 : lcp.BP) - lcp.Progress;
                    GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(bpLeft / buildRate, "PersonnelVessel"), GetLabelRightAlignStyle());
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
            GUILayout.Label(KCTGameStates.Researchers.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Max:", GUILayout.Width(90));
            GUILayout.Label(PresetManager.Instance.ActivePreset.ResearcherCaps[Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.ResearchAndDevelopment)].ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            RenderHireFire(true, out int fireAmount, out int hireAmount);

            int delta = 0;
            if (_currentPersonnelHover == PersonnelButtonHover.Hire)
                delta = hireAmount;
            else if (_currentPersonnelHover == PersonnelButtonHover.Fire)
                delta = -fireAmount;

            double techMult = PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherEfficiencyMultiplier;
            double days = GameSettings.KERBIN_TIME ? 4 : 1;

            _nodeRate = Formula.GetResearchRate(0, 0, delta);
            double sci = 86400 * _nodeRate;
            double sciPerDay = sci / days;
            double sciPerDayEffic = sciPerDay * techMult;
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
            const string researcherEfficTooltip = "Researching new Electronics Research nodes will increase this";
            GUILayout.Label(new GUIContent("Efficiency:", researcherEfficTooltip));
            GUILayout.Label(new GUIContent($"{techMult:P2}", researcherEfficTooltip), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (KCTGameStates.TechList.Count > 0)
            {
                TechItem t = KCTGameStates.TechList[0];
                GUILayout.Label($"Current Research: {t.TechName}");
                double techRate = Formula.GetResearchRate(t.ScienceCost, 0, delta) * techMult * t.YearBasedRateMult;
                double timeLeft = (t.ScienceCost - t.Progress) / techRate;
                GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(timeLeft, "PersonnelTech"), GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label("No current research");
            }
            GUILayout.EndHorizontal();
        }

        private static void RenderHireFire(bool research, out int fireAmount, out int hireAmount)
        {
            if (Utilities.CurrentGameIsCareer())
            {
                GUILayout.BeginHorizontal();

                string title = research ? "Researchers" : "Engineers";
                GUILayout.Label($"Hire/Fire {title}:");

                fireAmount = research ? KCTGameStates.Researchers : KCTGameStates.ActiveKSC.UnassignedEngineers;
                int workers = _buyModifier;
                if (workers == int.MaxValue)
                    workers = fireAmount;

                bool canAfford = workers <= fireAmount;
                GUIStyle style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                if (GUILayout.Button($"Fire {workers:N0}", style, GUILayout.ExpandWidth(false)) && canAfford)
                {
                    if (research)
                    {
                        Utilities.ChangeResearchers(-workers);
                        KCTGameStates.UpdateTechTimes();
                    }
                    else
                    {
                        KSCItem ksc = KCTGameStates.ActiveKSC;
                        Utilities.ChangeEngineers(ksc, -workers);
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

                double modifiedHireCost = -RP0.CurrencyUtils.Funds(research ? RP0.TransactionReasonsRP0.HiringResearchers : RP0.TransactionReasonsRP0.HiringEngineers, -PresetManager.Instance.ActivePreset.GeneralSettings.HireCost);
                workers = _buyModifier;
                if (workers == int.MaxValue)
                    workers = Math.Max(_buyModifierMultsPersonnel[0], KCTGameStates.UnassignedPersonnel + (int)(Funding.Instance.Funds / modifiedHireCost));

                if (research)
                    workers = Math.Max(0, Math.Min(workers, PresetManager.Instance.ActivePreset.ResearcherCaps[Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.ResearchAndDevelopment)] - KCTGameStates.Researchers));

                _fundsCost = modifiedHireCost * Math.Max(0, workers - KCTGameStates.UnassignedPersonnel);
                // Show the result for whatever you're asking for, even if you can't afford it.
                hireAmount = workers; // Math.Min(workers, (int)(Funding.Instance.Funds / PresetManager.Instance.ActivePreset.GeneralSettings.HireCost) + KCTGameStates.UnassignedPersonnel);

                canAfford = Funding.Instance.Funds >= _fundsCost;
                style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                if (GUILayout.Button($"Hire {workers:N0}: √{_fundsCost:N0}", style, GUILayout.ExpandWidth(false)) && canAfford)
                {
                    Utilities.SpendFunds(_fundsCost, TransactionReasons.None);
                    if (research)
                    {
                        Utilities.ChangeResearchers(workers);
                        KCTGameStates.UpdateTechTimes();
                    }
                    else
                    {
                        KSCItem ksc = KCTGameStates.ActiveKSC;
                        Utilities.ChangeEngineers(ksc, workers);
                        ksc.RecalculateBuildRates(false);
                    }
                    KCTGameStates.UnassignedPersonnel = Math.Max(0, KCTGameStates.UnassignedPersonnel - workers);
                    if (KCTGameStates.UnassignedPersonnel == 0)
                        KCTGameStates.HiredStarterApplicants = true;

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

        private static string GetAssignText(bool add, LCItem currentLC, out int mod)
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
