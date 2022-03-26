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
        private static readonly int[] _buyModifierMultsPersonnel = { 5, 50, 500, int.MaxValue };
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
            GUILayout.Label(tE.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.Width(40));
            GUILayout.Label("Salary and Facilities:", GetLabelRightAlignStyle(), GUILayout.Width(150));
            GUILayout.Label($"√{KCTGameStates.GetSalaryEngineers():N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Researchers:", GUILayout.Width(120));
            GUILayout.Label(KCTGameStates.Researchers.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.Width(40));
            GUILayout.Label("Salary and Facilities:", GetLabelRightAlignStyle(), GUILayout.Width(150));
            GUILayout.Label($"√{KCTGameStates.GetSalaryResearchers():N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            //if (!string.IsNullOrEmpty(PresetManager.Instance.ActivePreset.FormulaSettings.UpgradesForScience) &&
            //    KCTGameStates.SciPointsTotal >= 0)
            //{
            //    GUILayout.BeginHorizontal();
            //    GUILayout.Label("Total science:", GUILayout.Width(90));
            //    GUILayout.Label(((int)KCTGameStates.SciPointsTotal).ToString("N0"));
            //    GUILayout.EndHorizontal();
            //}


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

                if (!IsPrimarilyDisabled)
                {
                    KCTGameStates.ToolbarControl?.SetTrue();
                    GUIStates.ShowBuildList = true;
                }
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
            GUILayout.Label("Engineers:", GUILayout.ExpandWidth(false));
            GUILayout.Label(KSC.Engineers.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.Width(30));
            GUILayout.Label($"Free for Construction:", GetLabelRightAlignStyle());
            GUILayout.Label($"{KSC.ConstructionWorkers}", GetLabelRightAlignStyle(), GUILayout.Width(30));
            GUILayout.EndHorizontal();

            RenderHireFire(false, out int fireAmount, out int hireAmount);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<<", GUILayout.ExpandWidth(false))) { _LCIndex = KSC.SwitchLaunchComplex(false, _LCIndex, false); }
            GUILayout.Label(currentLC.IsRushing ? $"{currentLC.Name} (rushing)" : currentLC.Name, GetLabelCenterAlignStyle());
            if (GUILayout.Button(">>", GUILayout.ExpandWidth(false))) { _LCIndex = KSC.SwitchLaunchComplex(true, _LCIndex, false); }
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            GUILayout.Label("Assigned:");
            
            string assignStr = GetAssignText(true, currentLC, out int assignAmt);
            string unassignStr = GetAssignText(false, currentLC, out int unassignAmt);

            bool recalc = false;
            BuildListVessel.ListType type = currentLC.IsPad ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH;
            if (GUILayout.Button(unassignStr, GUILayout.ExpandWidth(false))) { Utilities.ChangeEngineers(currentLC, -unassignAmt); recalc = true; }
            if (Event.current.type == EventType.Repaint)
            {
                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    _currentPersonnelHover = PersonnelButtonHover.Unassign;
                else if (_currentPersonnelHover == PersonnelButtonHover.Unassign)
                    _currentPersonnelHover = PersonnelButtonHover.None;
            }

            GUILayout.Label($"  {currentLC.Engineers:N0}  ", GetLabelCenterAlignStyle(), GUILayout.ExpandWidth(false));

            if (GUILayout.Button(assignStr, GUILayout.ExpandWidth(false))) { Utilities.ChangeEngineers(currentLC, assignAmt); recalc = true; }
            if (Event.current.type == EventType.Repaint)
            {
                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    _currentPersonnelHover = PersonnelButtonHover.Assign;
                else if (_currentPersonnelHover == PersonnelButtonHover.Assign)
                    _currentPersonnelHover = PersonnelButtonHover.None;
            }

            GUILayout.Label($"Max: {currentLC.MaxPersonnel:N0}", GetLabelRightAlignStyle());
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
            int constructionDelta = 0;
            switch (_currentPersonnelHover)
            {
                case PersonnelButtonHover.Assign: constructionDelta = -assignAmt; break;
                case PersonnelButtonHover.Unassign: constructionDelta = unassignAmt; break;
                case PersonnelButtonHover.Hire: constructionDelta = hireAmount; break;
                case PersonnelButtonHover.Fire: constructionDelta = -fireAmount; break;
            }

            double efficLocal = _currentPersonnelHover == PersonnelButtonHover.Assign ? Utilities.PredictEfficiencyEngineers(currentLC, assignDelta) : currentLC.EfficiencyEngineers;
            double efficGlobal = _currentPersonnelHover == PersonnelButtonHover.Hire ? Utilities.PredictEfficiencyEngineers(constructionDelta) : KCTGameStates.EfficiecnyEngineers;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Efficiency: {efficLocal:P1} (at {currentLC.Name}) x {efficGlobal:P1} (global)");
            GUILayout.EndHorizontal();

            double cRateFull = Utilities.GetConstructionRate(0, KSC, constructionDelta);
            double cRate = cRateFull * efficGlobal;

            double rateFull = Utilities.GetBuildRate(0, type, currentLC, currentLC.IsHumanRated, assignDelta);
            double rate = rateFull * efficLocal * efficGlobal;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Vessel Rate: {rateFull:N3} => {rate:N3} BP/sec", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (currentLC.BuildList.Count > 0)
            {
                BuildListVessel b = currentLC.BuildList[0];
                GUILayout.Label($"Current Vessel: {b.ShipName}");
                double buildRate = Math.Min(Utilities.GetBuildRate(0, b.Type, currentLC, b.IsHumanRated, assignDelta), Utilities.GetBuildRateCap(b.BuildPoints + b.IntegrationPoints, b.GetTotalMass(), currentLC))
                    * efficLocal * efficGlobal;
                double bpLeft = b.BuildPoints + b.IntegrationPoints - b.Progress;
                GUILayout.Label($"Est: {MagiCore.Utilities.GetColonFormattedTime(bpLeft / buildRate)}", GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label($"No vessels under construction at {currentLC.Name}");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Construction Rate: {cRateFull:N2} => {cRate:N2} BP/sec)", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (KSC.Constructions.Count > 0)
            {
                IConstructionBuildItem b = KSC.Constructions[0];
                GUILayout.Label($"Current Construction: {b.GetItemName()}");
                GUILayout.Label($"Est: {MagiCore.Utilities.GetColonFormattedTime((b.BuildPoints() - b.CurrentProgress()) / cRate)}", GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label($"No construction projects");
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

            double effic = Utilities.PredictEfficiencyResearchers(delta);

            double days = GameSettings.KERBIN_TIME ? 4 : 1;
            //if (_nodeRate == int.MinValue || isCostCacheInvalid)
            //{
            //    _nodeDelta = _buyModifier == int.MaxValue ? AvailablePoints : _buyModifier;
            //    _nodeRate = MathParser.ParseNodeRateFormula(0);
            //    _upNodeRate = MathParser.ParseNodeRateFormula(0, 0, _nodeDelta);
            //}
            _nodeRate = MathParser.ParseNodeRateFormula(0, 0, delta);
            double sci = 86400 * _nodeRate;
            double sciPerDay = sci / days;
            double sciPerDayEffic = sciPerDay * effic;
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
            GUILayout.Label("Global Researcher Efficiency:");
            GUILayout.Label($"{effic:P1}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (KCTGameStates.TechList.Count > 0)
            {
                TechItem t = KCTGameStates.TechList[0];
                GUILayout.Label($"Current Research: {t.TechName}");
                double techRate = MathParser.ParseNodeRateFormula(t.ScienceCost, 0, delta) * effic * t.YearBasedRateMult;
                double timeLeft = (t.ScienceCost - t.Progress) / techRate;
                GUILayout.Label($"Est: {MagiCore.Utilities.GetColonFormattedTime(timeLeft)}", GetLabelRightAlignStyle());
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

                fireAmount = research ? KCTGameStates.Researchers : KCTGameStates.ActiveKSC.ConstructionWorkers;
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

                workers = _buyModifier;
                if (workers == int.MaxValue)
                    workers = Math.Max(_buyModifierMultsPersonnel[0], KCTGameStates.UnassignedPersonnel + (int)(Funding.Instance.Funds / PresetManager.Instance.ActivePreset.GeneralSettings.HireCost));

                if (research)
                    workers = Math.Min(workers, PresetManager.Instance.ActivePreset.ResearcherCaps[Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.ResearchAndDevelopment)]);

                _fundsCost = PresetManager.Instance.ActivePreset.GeneralSettings.HireCost * Math.Max(0, workers - KCTGameStates.UnassignedPersonnel);
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
                limit = currentLC.KSC.ConstructionWorkers;
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
