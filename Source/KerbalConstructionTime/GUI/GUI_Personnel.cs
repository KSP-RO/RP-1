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

        private static int TotalEngineers => KCTGameStates.KSCs.Sum(k => k.Personnel);

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
            GUILayout.Label("Total Engineers:", GUILayout.Width(120));
            GUILayout.Label(TotalEngineers.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Researchers:", GUILayout.Width(120));
            GUILayout.Label(KCTGameStates.RDPersonnel.ToString("N0"), GetLabelRightAlignStyle());
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
            if (Utilities.CurrentGameHasScience() && GUILayout.Button("Reserachers")) { _personnelWindowHolder = 2; _personnelPosition.height = 1; }
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
                _LCIndex = KCTGameStates.ActiveKSC.ActiveLaunchComplexID; // reset to current active LC

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
            GUILayout.Label("Engineers:", GUILayout.Width(90));
            GUILayout.Label(KSC.Personnel.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.Label($"Free for Construction: {KSC.FreePersonnel} ({Utilities.GetConstructionRate(KSC):N2} BP/sec)", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            RenderHireFire(false);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<<", GUILayout.ExpandWidth(false))) { _LCIndex = KSC.SwitchLaunchComplex(false, _LCIndex, false); }
            GUILayout.Label(currentLC.Name);
            if (GUILayout.Button(">>", GUILayout.ExpandWidth(false))) { _LCIndex = KSC.SwitchLaunchComplex(true, _LCIndex, false); }
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            GUILayout.Label("Assigned:");
            int delta;
            bool recalc = false;
            BuildListVessel.ListType type = currentLC.isPad ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH;
            if (GUILayout.Button(GetAssignText(false, currentLC, out delta), GUILayout.ExpandWidth(false))) { ChangeEngineers(currentLC, -delta); recalc = true; }
            GUILayout.Label($"{currentLC.Personnel:N0}");
            if (GUILayout.Button(GetAssignText(true, currentLC, out delta), GUILayout.ExpandWidth(false))) { ChangeEngineers(currentLC, delta); recalc = true; }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Efficiency: {(currentLC.EfficiencyPersonnel * 100d):N0}%", GetLabelCenterAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Utilities.GetBuildRate(0, type, currentLC):N2}BP/sec");
            GUILayout.EndHorizontal();

            if (recalc)
            {
                currentLC.RecalculateBuildRates();
                currentLC.KSC.RecalculateBuildRates(false);
            }
        }

        private static void RenderResearchersSection(bool isCostCacheInvalid)
        {
            KSCItem KSC = KCTGameStates.ActiveKSC;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Researchers:", GUILayout.Width(90));
            GUILayout.Label(KCTGameStates.RDPersonnel.ToString("N0"), GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            RenderHireFire(true);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Efficiency: {(KCTGameStates.EfficiencyRDPersonnel * 100d):N0}%", GetLabelCenterAlignStyle());
            GUILayout.EndHorizontal();

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
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rate");
            //bool usingPerYear = false;
            if (sciPerDay > 0.1)
            {
                GUILayout.Label($"{sciPerDay:N3} sci/day", GetLabelRightAlignStyle());
            }
            else
            {
                //Well, looks like we need sci/year instead
                int daysPerYear = KSPUtil.dateTimeFormatter.Year / KSPUtil.dateTimeFormatter.Day;
                GUILayout.Label($"{(sciPerDay * daysPerYear):N3} sci/yr", GetLabelRightAlignStyle());
                //usingPerYear = true;
            }
            GUILayout.EndHorizontal();
        }

        private static void RenderHireFire(bool research)
        {
            if (Utilities.CurrentGameIsCareer())
            {
                GUILayout.BeginHorizontal();

                string title = research ? "Researchers" : "Engineers";

                int limit = research ? KCTGameStates.RDPersonnel : KCTGameStates.ActiveKSC.Personnel;
                int workers = _buyModifier;
                if (workers == int.MaxValue)
                    workers = limit;

                bool canAfford = workers <= limit;
                GUIStyle style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                if (GUILayout.Button($"Fire {workers:N0} {title}", style, GUILayout.ExpandWidth(false)) && canAfford)
                {
                    if (research)
                    {
                        ChangeResearchers(-workers);
                        KCTGameStates.UpdateTechTimes();
                    }
                    else
                    {
                        KSCItem ksc = KCTGameStates.ActiveKSC;
                        ksc.Personnel -= workers;
                        ksc.RecalculateBuildRates(false);
                    }
                }

                GUILayout.FlexibleSpace();

                workers = _buyModifier;
                if (workers == int.MaxValue)
                    workers = Math.Max(_buyModifierMultsPersonnel[0], (int)(Funding.Instance.Funds / PresetManager.Instance.ActivePreset.GeneralSettings.HireCost));

                _fundsCost = PresetManager.Instance.ActivePreset.GeneralSettings.HireCost * workers;

                
                canAfford = Funding.Instance.Funds >= _fundsCost;
                style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                if (GUILayout.Button($"Hire {workers:N0} {title}: {_fundsCost:N0} Funds", style, GUILayout.ExpandWidth(false)) && canAfford)
                {
                    Utilities.SpendFunds(_fundsCost, TransactionReasons.None);
                    if (research)
                    {
                        ChangeResearchers(workers);
                        KCTGameStates.UpdateTechTimes();
                    }
                    else
                    {
                        KSCItem ksc = KCTGameStates.ActiveKSC;
                        ksc.Personnel += workers;
                        ksc.RecalculateBuildRates(false);
                    }

                    _fundsCost = int.MinValue;
                }
                GUILayout.EndHorizontal();
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

        private static void ChangeEngineers(LCItem currentLC, int delta)
        {
            int oldNum = currentLC.Personnel;
            double newNum = oldNum + delta;
            if (delta > 0)
                currentLC.EfficiencyPersonnel = ((currentLC.EfficiencyPersonnel * oldNum) + (delta * PresetManager.Instance.ActivePreset.GeneralSettings.EngineerStartEfficiency)) / newNum;
            currentLC.Personnel += delta;
        }

        private static void ChangeResearchers(int delta)
        {
            int oldNum = KCTGameStates.RDPersonnel;
            double newNum = oldNum + delta;
            if (delta > 0)
                KCTGameStates.EfficiencyRDPersonnel = ((KCTGameStates.EfficiencyRDPersonnel * oldNum) + (delta * PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherStartEfficiency)) / newNum;

            KCTGameStates.EfficiencyRDPersonnel += delta;
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
