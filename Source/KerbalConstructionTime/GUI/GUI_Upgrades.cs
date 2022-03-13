using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static Rect _upgradePosition = new Rect((Screen.width - 450) / 2, Screen.height / 4, 450, 1);
        private static int _upgradeWindowHolder = 0;
        private static double _fundsCost = int.MinValue;
        private static double _nodeRate = int.MinValue, _upNodeRate = int.MinValue;
        private static int _spentPoints = int.MinValue;
        private static int _totalPoints = int.MinValue;
        private static int _buyModifier = 1;
        private static int _fundsDelta = 1;
        private static int _nodeDelta = 1;
        private static GUIStyle _cannotAffordStyle;

        private static int SpentPoints
        {
            get
            {
                if (_spentPoints == int.MinValue) 
                    _spentPoints = Utilities.GetTotalSpentUpgrades();
                return _spentPoints;
            }
        }

        private static int TotalPoints
        {
            get
            {
                if (_totalPoints == int.MinValue)
                    _totalPoints = Utilities.GetTotalUpgradePoints();
                return _totalPoints;
            }
        }

        private static int AvailablePoints => TotalPoints - SpentPoints;

        public static void ResetUpgradePointCounts()
        {
            _spentPoints = int.MinValue;
            _totalPoints = int.MinValue;
            _fundsCost = int.MinValue;
        }

        private static void DrawUpgradeWindow(int windowID)
        {
            int oldByModifier = _buyModifier;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                _buyModifier = 5;
            }
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                _buyModifier = 100;
            }
            else if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                _buyModifier = -1;
            }
            else
            {
                _buyModifier = 1;
            }

            bool isCostCacheInvalid = _buyModifier != oldByModifier;

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Points:", GUILayout.Width(90));
            GUILayout.Label(TotalPoints.ToString());
            GUILayout.Label($"Available: {AvailablePoints}");
            GUILayout.EndHorizontal();

            int vabPoints = Utilities.GetSpentUpgradesFor(SpaceCenterFacility.VehicleAssemblyBuilding);
            int sphPoints = Utilities.GetSpentUpgradesFor(SpaceCenterFacility.SpaceplaneHangar);
            vabPoints += sphPoints;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Integration Pts:", GUILayout.Width(90));
            GUILayout.Label(vabPoints.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Points in R&D:", GUILayout.Width(90));
            GUILayout.Label(Utilities.GetSpentUpgradesFor(SpaceCenterFacility.ResearchAndDevelopment).ToString());
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(PresetManager.Instance.ActivePreset.FormulaSettings.UpgradesForScience) &&
                KCTGameStates.SciPointsTotal >= 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Total science:", GUILayout.Width(90));
                GUILayout.Label(((int)KCTGameStates.SciPointsTotal).ToString());
                GUILayout.EndHorizontal();
            }

            if (Utilities.CurrentGameIsCareer())
            {
                if (_fundsCost == int.MinValue || isCostCacheInvalid)
                {
                    _fundsDelta = _buyModifier;
                    _fundsCost = CalcPointCost(ref _fundsDelta, Funding.Instance.Funds, "UpgradeFunds", 1);
                }
                if (_fundsCost >= 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(_fundsDelta > 1 ? $"Buy {_fundsDelta} Points: " : "Buy 1 Point: ");
                    bool canAfford = Funding.Instance.Funds >= _fundsCost;
                    GUIStyle style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                    if (GUILayout.Button($"{Math.Round(_fundsCost, 0)} Funds", style, GUILayout.ExpandWidth(false)) && canAfford)
                    {
                        Utilities.SpendFunds(_fundsCost, TransactionReasons.None);
                        KCTGameStates.PurchasedUpgrades[1] += _fundsDelta;

                        _fundsCost = _spentPoints = _totalPoints = int.MinValue;
                    }
                    GUILayout.EndHorizontal();
                }
            }

            RenderPointResetSection();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Integ.")) { _upgradeWindowHolder = 0; _upgradePosition.height = 1; }
            if (Utilities.CurrentGameHasScience() && GUILayout.Button("R&D")) { _upgradeWindowHolder = 2; _upgradePosition.height = 1; }
            GUILayout.EndHorizontal();

            KSCItem KSC = KCTGameStates.ActiveKSC;

            if (_upgradeWindowHolder == 0)    //VAB
            {
                RenderBuildRateSection(BuildListVessel.ListType.VAB, KSC);
            }

            if (_upgradeWindowHolder == 1)    //SPH
            {
                RenderBuildRateSection(BuildListVessel.ListType.SPH, KSC);
            }

            if (_upgradeWindowHolder == 2)    //R&D
            {
                RenderRnDSection(isCostCacheInvalid, KSC);
            }

            if (GUILayout.Button("Close"))
            {
                GUIStates.ShowUpgradeWindow = false;
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

        private static void RenderPointResetSection()
        {
            //TODO: Calculate the cost of resetting
            int ResetCost = (int)MathParser.GetStandardFormulaValue("UpgradeReset", new Dictionary<string, string> { { "N", KCTGameStates.UpgradesResetCounter.ToString() } });
            if (ResetCost >= 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Reset Upgrades: ");

                bool canAfford = AvailablePoints >= ResetCost;
                var style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                if (GUILayout.Button($"{ResetCost} Points", style, GUILayout.ExpandWidth(false)))
                {
                    if (SpentPoints > 0 && canAfford) //you have to spend some points before resetting does anything
                    {
                        KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.VABUpgrades = new List<int>() { 0 };
                        KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.SPHUpgrades = new List<int>() { 0 };
                        KCTGameStates.ActiveKSC.RDUpgrades = new List<int>() { 0, 0 };
                        KCTGameStates.TechUpgradesTotal = 0;
                        foreach (KSCItem ksc in KCTGameStates.KSCs)
                        {
                            ksc.RDUpgrades[1] = 0;
                        }
                        _nodeRate = int.MinValue;
                        _upNodeRate = int.MinValue;

                        KCTGameStates.ActiveKSC.RecalculateBuildRates();
                        KCTGameStates.ActiveKSC.RecalculateUpgradedBuildRates();

                        foreach (TechItem tech in KCTGameStates.TechList)
                            tech.UpdateBuildRate(KCTGameStates.TechList.IndexOf(tech));

                        KCTGameStates.UpgradesResetCounter++;
                    }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private static void RenderBuildRateSection(BuildListVessel.ListType type, KSCItem KSC)
        {
            List<int> upgrades = type == BuildListVessel.ListType.VAB ? KSC.ActiveLaunchComplexInstance.VABUpgrades : KSC.ActiveLaunchComplexInstance.SPHUpgrades;
            List<double> rates = type == BuildListVessel.ListType.VAB ? KSC.ActiveLaunchComplexInstance.VABRates : KSC.ActiveLaunchComplexInstance.SPHRates;

            GUILayout.BeginHorizontal();
            GUILayout.Label(type.ToString() + " Upgrades");
            GUILayout.Label($"+{(_buyModifier < 0 ? "MAX" : _buyModifier.ToString())} Point{(_buyModifier == 1 ? "" : "s")}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height((upgrades.Count + 1) * 26 + 5), GUILayout.MaxHeight(1 * Screen.height / 4));
            GUILayout.BeginVertical();
            for (int i = 0; i < rates.Count; i++)
            {
                int pointsDelta = _buyModifier;
                if (pointsDelta < 0)
                {
                    if (i == 0) pointsDelta = AvailablePoints;
                    else if (i > rates.Count) pointsDelta = 0;
                    else
                    {
                        pointsDelta = 1;
                        while (pointsDelta < AvailablePoints && Utilities.GetBuildRate(i, type, KSC.ActiveLaunchComplexInstance, pointsDelta + 1) <= rates[i - 1])
                        {
                            pointsDelta++;
                        }
                    }
                }
                double rate = Utilities.GetBuildRate(i, type, KSC.ActiveLaunchComplexInstance);
                double upgraded = Utilities.GetBuildRate(i, type, KSC.ActiveLaunchComplexInstance, true);
                double deltaUpgraded = Utilities.GetBuildRate(i, type, KSC.ActiveLaunchComplexInstance, pointsDelta);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Rate {i + 1}");
                GUILayout.Label($"{rate} BP/s");
                if (AvailablePoints > 0 && (i == 0 || upgraded <= Utilities.GetBuildRate(i - 1, type, KSC.ActiveLaunchComplexInstance)) && upgraded - rate > 0)
                {
                    bool canAfford = AvailablePoints >= pointsDelta && (i == 0 || deltaUpgraded <= Utilities.GetBuildRate(i - 1, type, KSC.ActiveLaunchComplexInstance));
                    GUIStyle style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                    if (GUILayout.Button($"+{Math.Round(deltaUpgraded - rate, 3)}", style, GUILayout.Width(55)) && canAfford)
                    {
                        if (i >= upgrades.Count)
                            upgrades.Add(pointsDelta);
                        else
                            upgrades[i] += pointsDelta;

                        KSC.RecalculateBuildRates();
                        KSC.RecalculateUpgradedBuildRates();
                        _fundsCost = _spentPoints = _totalPoints = int.MinValue;
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private static void RenderRnDSection(bool isCostCacheInvalid, KSCItem KSC)
        {
            int labelDelta = _buyModifier < 0 ? AvailablePoints : _buyModifier;
            GUILayout.BeginHorizontal();
            GUILayout.Label("R&D Upgrades");
            GUILayout.Label($"+{labelDelta} Point{(labelDelta == 1 ? "" : "s")}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            double days = GameSettings.KERBIN_TIME ? 4 : 1;
            if (_nodeRate == int.MinValue || isCostCacheInvalid)
            {
                _nodeDelta = _buyModifier < 0 ? AvailablePoints : _buyModifier;
                _nodeRate = MathParser.ParseNodeRateFormula(0);
                _upNodeRate = MathParser.ParseNodeRateFormula(0, 0, _nodeDelta);
            }

            double sci = 86400 * _nodeRate;
            double sciPerDay = sci / days;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rate");
            bool usingPerYear = false;
            if (sciPerDay > 0.1)
            {
                GUILayout.Label(Math.Round(sciPerDay * 1000) / 1000 + " sci/day");
            }
            else
            {
                //Well, looks like we need sci/year instead
                int daysPerYear = KSPUtil.dateTimeFormatter.Year / KSPUtil.dateTimeFormatter.Day;
                GUILayout.Label(Math.Round(sciPerDay * daysPerYear * 1000) / 1000 + " sci/yr");
                usingPerYear = true;
            }
            if (_upNodeRate != _nodeRate && AvailablePoints > 0)
            {
                bool everyKSCCanUpgrade = true;
                foreach (KSCItem ksc in KCTGameStates.KSCs)
                {
                    if (TotalPoints - Utilities.GetTotalSpentUpgrades(ksc) <= 0)
                    {
                        everyKSCCanUpgrade = false;
                        break;
                    }
                }
                if (everyKSCCanUpgrade)
                {
                    double upSciPerDay = 86400 * _upNodeRate / days;
                    string buttonText = $"{Math.Round(1000 * upSciPerDay) / 1000} sci/day";
                    if (usingPerYear)
                    {
                        int daysPerYear = KSPUtil.dateTimeFormatter.Year / KSPUtil.dateTimeFormatter.Day;
                        buttonText = $"{Math.Round(upSciPerDay * daysPerYear * 1000) / 1000} sci/yr";
                    }
                    bool canAfford = AvailablePoints >= _nodeDelta;
                    GUIStyle style = canAfford ? GUI.skin.button : GetCannotAffordStyle();
                    if (GUILayout.Button(buttonText, style, GUILayout.ExpandWidth(false)) && canAfford)
                    {
                        KCTGameStates.TechUpgradesTotal += _nodeDelta;
                        foreach (KSCItem ksc in KCTGameStates.KSCs)
                            ksc.RDUpgrades[1] = KCTGameStates.TechUpgradesTotal;

                        _nodeRate = _upNodeRate = int.MinValue;
                        _fundsCost = _spentPoints = _totalPoints = int.MinValue;

                        foreach (TechItem tech in KCTGameStates.TechList)
                        {
                            tech.UpdateBuildRate(KCTGameStates.TechList.IndexOf(tech));
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
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

        private static double CalcPointCost(ref int pointDelta, double maxCost, string formulaName, int gameStateIndex)
        {
            int purchasedUpgrades = KCTGameStates.PurchasedUpgrades[gameStateIndex];
            var variables = new Dictionary<string, string>
            {
                { "N", purchasedUpgrades.ToString() }
            };
            double cumulativeCost = 0;
            double nextCost = MathParser.GetStandardFormulaValue(formulaName, variables);

            if (nextCost < 0)
            {
                return -1;
            }

            if (pointDelta < 0)
            {
                pointDelta = 0;
                do
                {
                    pointDelta++;
                    cumulativeCost += nextCost;
                    variables["N"] = (purchasedUpgrades + pointDelta).ToString();
                    nextCost = MathParser.GetStandardFormulaValue(formulaName, variables);
                }
                while (cumulativeCost + nextCost <= maxCost);
            }
            else
            {
                for (int i = 0; i < pointDelta; i++)
                {
                    variables["N"] = (purchasedUpgrades + i).ToString();
                    cumulativeCost += MathParser.GetStandardFormulaValue(formulaName, variables);
                }
            }
            return cumulativeCost;
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
