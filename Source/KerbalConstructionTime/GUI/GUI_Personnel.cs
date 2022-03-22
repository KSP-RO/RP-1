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
        private static int _hireFireDelta = 0;
        private static int _assignDelta = 0;

        public static int TotalEngineers => KCTGameStates.KSCs.Sum(k => k.Personnel);

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

            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Engineers:", GUILayout.Width(120));
            GUILayout.Label(TotalEngineers.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Global Engineer Efficiency:");
            GUILayout.Label($"{(KCTGameStates.EfficiecnyEngineers * 100d):N0}%", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Researchers:", GUILayout.Width(120));
            GUILayout.Label(KCTGameStates.RDPersonnel.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Global Researcher Efficiency:");
            GUILayout.Label($"{(KCTGameStates.EfficiencyRDPersonnel * 100d)}%", GetLabelRightAlignStyle());
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
            GUILayout.Label(KSC.Personnel.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.Width(30));
            double cRate = Utilities.GetConstructionRate(KSC);
            GUILayout.Label($"Free for Construction: {KSC.FreePersonnel} ({cRate:N2} => {(cRate * KCTGameStates.EfficiecnyEngineers):N2} BP/sec)", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            int constructionDelta = RenderHireFire(false);

            if (Event.current.type == EventType.Repaint)
                _assignDelta = 0;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<<", GUILayout.ExpandWidth(false))) { _LCIndex = KSC.SwitchLaunchComplex(false, _LCIndex, false); }
            GUILayout.Label(currentLC.Name);
            if (GUILayout.Button(">>", GUILayout.ExpandWidth(false))) { _LCIndex = KSC.SwitchLaunchComplex(true, _LCIndex, false); }
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            GUILayout.Label("Assigned:");
            int delta;
            bool recalc = false;
            BuildListVessel.ListType type = currentLC.IsPad ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH;
            if (GUILayout.Button(GetAssignText(false, currentLC, out delta), GUILayout.ExpandWidth(false))) { Utilities.ChangeEngineers(currentLC, -delta); recalc = true; }
            if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                _assignDelta = delta;
            GUILayout.Label($"  {currentLC.Personnel:N0}  ", GetLabelCenterAlignStyle(), GUILayout.ExpandWidth(false));
            if (GUILayout.Button(GetAssignText(true, currentLC, out delta), GUILayout.ExpandWidth(false))) { Utilities.ChangeEngineers(currentLC, delta); recalc = true; }
            if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                _assignDelta = delta;
            GUILayout.Label($"Max: {currentLC.MaxPersonnel:N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Efficiency: {(currentLC.EfficiencyPersonnel * 100d):N0}% (LC) x {(KCTGameStates.EfficiecnyEngineers*100d):N0}% (global)");
            double rateFull = Utilities.GetBuildRate(0, type, currentLC, currentLC.IsHumanRated);
            double rate = rateFull * currentLC.EfficiencyPersonnel * KCTGameStates.EfficiecnyEngineers;
            GUILayout.Label($"Rate: {rateFull:N3} => {rate:N3} BP/sec", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            if (recalc)
            {
                currentLC.RecalculateBuildRates();
                currentLC.KSC.RecalculateBuildRates(false);
            }

            GUILayout.BeginHorizontal();
            if (currentLC.BuildList.Count > 0)
            {
                BuildListVessel b = currentLC.BuildList[0];
                GUILayout.Label($"Current Vessel: {b.ShipName}");
                double buildRate = Math.Min(Utilities.GetBuildRate(0, b.Type, currentLC, b.IsHumanRated, _assignDelta), Utilities.GetBuildRateCap(b.BuildPoints + b.IntegrationPoints, b.GetTotalMass(), currentLC))
                    * currentLC.EfficiencyPersonnel * KCTGameStates.EfficiecnyEngineers;
                double bpLeft = b.BuildPoints + b.IntegrationPoints - b.Progress;
                GUILayout.Label($"Est: {MagiCore.Utilities.GetColonFormattedTime(bpLeft / buildRate)}", GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label($"No vessels under construction at {currentLC.Name}");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (KSC.Constructions.Count > 0)
            {
                IConstructionBuildItem b = KSC.Constructions[0];
                GUILayout.Label($"Current Construction: {b.GetItemName()}");

                double buildRate = Utilities.GetConstructionRate(0, KSC, _hireFireDelta) * KCTGameStates.EfficiecnyEngineers;
                GUILayout.Label($"Est: {MagiCore.Utilities.GetColonFormattedTime((b.BuildPoints() - b.CurrentProgress()) / buildRate)}", GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label($"No construction projects");
            }
            GUILayout.EndHorizontal();
        }

        private static void RenderResearchersSection(bool isCostCacheInvalid)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Researchers:", GUILayout.Width(90));
            GUILayout.Label(KCTGameStates.RDPersonnel.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            int delta = RenderHireFire(true);

            double days = GameSettings.KERBIN_TIME ? 4 : 1;
            //if (_nodeRate == int.MinValue || isCostCacheInvalid)
            //{
            //    _nodeDelta = _buyModifier == int.MaxValue ? AvailablePoints : _buyModifier;
            //    _nodeRate = MathParser.ParseNodeRateFormula(0);
            //    _upNodeRate = MathParser.ParseNodeRateFormula(0, 0, _nodeDelta);
            //}
            _nodeRate = MathParser.ParseNodeRateFormula(0, 0, 0);
            double sci = 86400 * _nodeRate;
            double sciPerDay = sci / days;
            double sciPerDayEffic = sciPerDay * KCTGameStates.EfficiencyRDPersonnel;
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
            if (KCTGameStates.TechList.Count > 0)
            {
                TechItem t = KCTGameStates.TechList[0];
                GUILayout.Label($"Current Research: {t.TechName}");
                double techRate = MathParser.ParseNodeRateFormula(t.ScienceCost, 0, delta) * KCTGameStates.EfficiencyRDPersonnel * t.YearBasedRateMult;
                double timeLeft = (t.ScienceCost - t.Progress) / techRate;
                GUILayout.Label($"Est: {MagiCore.Utilities.GetColonFormattedTime(timeLeft)}", GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label("No current research");
            }
            GUILayout.EndHorizontal();
        }

        private static int RenderHireFire(bool research)
        {
            if (Event.current.type == EventType.Repaint)
                _hireFireDelta = 0;

            if (Utilities.CurrentGameIsCareer())
            {
                GUILayout.BeginHorizontal();

                string title = research ? "Researchers" : "Engineers";
                GUILayout.Label($"Hire/Fire {title}:");

                int limit = research ? KCTGameStates.RDPersonnel : KCTGameStates.ActiveKSC.FreePersonnel;
                int workers = _buyModifier;
                if (workers == int.MaxValue)
                    workers = limit;

                bool canAfford = workers <= limit;
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
                if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    _hireFireDelta = workers;

                workers = _buyModifier;
                if (workers == int.MaxValue)
                    workers = Math.Max(_buyModifierMultsPersonnel[0], KCTGameStates.UnassignedPersonnel + (int)(Funding.Instance.Funds / PresetManager.Instance.ActivePreset.GeneralSettings.HireCost));

                _fundsCost = PresetManager.Instance.ActivePreset.GeneralSettings.HireCost * Math.Max(0, workers - KCTGameStates.UnassignedPersonnel);

                
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
                if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    _hireFireDelta = workers;

                GUILayout.EndHorizontal();
            }
            return _hireFireDelta;
        }

        private static string GetAssignText(bool add, LCItem currentLC, out int mod)
        {
            string signChar;
            int limit;
            mod = _buyModifierMultsPersonnel[0]; // default
            if (add)
            {
                signChar = "+";
                limit = currentLC.KSC.FreePersonnel;
            }
            else
            {
                signChar = "-";
                limit = currentLC.Personnel;
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
