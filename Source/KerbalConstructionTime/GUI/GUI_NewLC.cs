using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static string[] _padLvlOptions = null;
        private static float[] _padTons = null;
        private static Vector3[] _padSizes;

        private static string _tonnageLimit = "60";
        private static string _heightLimit = "33";
        private static string _widthLimit = "8";

        public static void GetPadStats(float tonnageLimit, Vector3 padSize, out float minTonnage, out double curPadCost, out double curVABCost, out float fractionalPadLvl)
        {
            fractionalPadLvl = 0f;
            if (tonnageLimit > 0f)
            {
                minTonnage = Mathf.Floor(tonnageLimit * 0.5f);
                if (minTonnage < 10f)
                    minTonnage = 0f;

                double mass = tonnageLimit;
                curPadCost = Math.Pow(mass, 0.5d) * 2500d + Math.Pow(mass, 1.5d) * 1.5d;

                if (_padLvlOptions == null)
                {
                    LoadPadData();
                }

                if (_padLvlOptions == null)
                {
                    fractionalPadLvl = 0f;
                }
                else
                {
                    float unlimitedTonnageThreshold = 3500f;

                    if (tonnageLimit >= unlimitedTonnageThreshold)
                    {
                        int padLvl = _padLvlOptions.Length - 2;
                        fractionalPadLvl = padLvl;
                    }
                    else
                    {
                        for (int i = 1; i < _padTons.Length; i++)
                        {
                            if (tonnageLimit < _padTons[i])
                            {
                                float lowerBound = _padTons[i - 1];
                                float upperBound = Math.Min(_padTons[i], unlimitedTonnageThreshold);
                                float fractionOverFullLvl = (tonnageLimit - lowerBound) / (upperBound - lowerBound);
                                fractionalPadLvl = (i - 1) + fractionOverFullLvl;

                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                // SPH case
                minTonnage = 0f;
                curPadCost = 0f;
                padSize.y *= 5f;
            }
            curVABCost = Math.Pow(padSize.magnitude, 2.5d) * 2d;
        }

        public static void DrawNewLCWindow(int windowID)
        {
            LCItem activeLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            double oldVABCost = 0, oldPadCost = 0, lpMult = 1;

            GUILayout.BeginVertical();
            if (GUIStates.ShowModifyLC)
            {
                GUILayout.Label(activeLC.Name);
                GUILayout.Label($"Tonnage limits: {activeLC.SupportedMassAsPrettyText}");
                GUILayout.Label($"Size limits: {activeLC.SupportedSizeAsPrettyText}");
                                
                GetPadStats(activeLC.massMax, activeLC.sizeMax, out _, out oldPadCost, out oldVABCost, out _);
                lpMult = activeLC.LaunchPads.Count;
            }
            else
            {
                GUILayout.Label("Name:");
                _newName = GUILayout.TextField(_newName);
            }

            GUILayout.Label(GUIStates.ShowModifyLC ? "New Limits" : "Launch Complex Limits:");

            double curPadCost = 0;
            double curVABCost = 0;
            float fractionalPadLvl = -1;
            float tonnageLimit = 0;
            float heightLimit = 0;
            float widthLimit = 0;
            float minTonnage = 0f;
            Vector3 curPadSize = Vector3.zero;

            bool hasTonnage = !GUIStates.ShowModifyLC || activeLC.isPad;
            if (hasTonnage)
            {
                GUILayout.Label("Maximum tonnage:");
                _tonnageLimit = GUILayout.TextField(_tonnageLimit);
            }
            if ((!hasTonnage || float.TryParse(_tonnageLimit, out tonnageLimit)) && float.TryParse(_heightLimit, out heightLimit) && float.TryParse(_widthLimit, out widthLimit))
            {
                curPadSize.x = curPadSize.z = widthLimit;
                curPadSize.y = heightLimit;
                GetPadStats(tonnageLimit, new Vector3(widthLimit, heightLimit, widthLimit), out minTonnage, out curPadCost, out curVABCost, out fractionalPadLvl);
            }
            if (hasTonnage)
                GUILayout.Label($"Minimum tonnage: {minTonnage:N}");
            else
                tonnageLimit = -1f;

            GUILayout.Label("Size Limits:");
            _widthLimit = GUILayout.TextField(_widthLimit);
            _heightLimit = GUILayout.TextField(_heightLimit);

            double totalCost = Math.Abs(curVABCost - oldVABCost) + Math.Abs(curPadCost - oldPadCost) * lpMult;

            if (totalCost > 0)
            {
                double curPadBuildTime = FacilityUpgrade.CalculateBuildTime(totalCost, SpaceCenterFacility.LaunchPad);
                string sBuildTime = KSPUtil.PrintDateDelta(curPadBuildTime, includeTime: false);
                string costString;
                if (GUIStates.ShowModifyLC)
                {
                    costString = $"It will cost {Math.Round(totalCost):N0} funds to renovate {activeLC.Name}.";
                    if (activeLC.isPad)
                        costString += $" Additional pads there will now cost {Math.Round(curPadCost):N}.";
                }
                else
                {
                    costString = $"It will cost {Math.Round(totalCost):N0} funds to build the new launch complex, and additional pads there will cost {Math.Round(curPadCost):N0}.";
                }
                GUILayout.Label(costString + $" Estimated construction time is {sBuildTime}.");
            }

            GUILayout.Label("Would you like to " + (GUIStates.ShowModifyLC ? "renovate it?" : "build it?"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes") && ValidateLCCreationParameters(_newName, fractionalPadLvl, tonnageLimit, curPadSize, GUIStates.ShowModifyLC))
            {

                string lcName = GUIStates.ShowModifyLC ? activeLC.Name : _newName;
                if (!Utilities.CurrentGameIsCareer())
                {
                    KCTDebug.Log($"Building/Modifying launch complex {lcName}");
                    if (GUIStates.ShowModifyLC)
                        activeLC.Modify(tonnageLimit, curPadSize);
                    else
                        KCTGameStates.ActiveKSC.LaunchComplexes.Add(new LCItem(_newName, tonnageLimit, curPadSize, true, KCTGameStates.ActiveKSC));
                }
                else if (Funding.CanAfford((float)totalCost))
                {
                    KCTDebug.Log($"Building/Modifying launch complex {lcName}");
                    Utilities.SpendFunds(totalCost, TransactionReasons.StructureConstruction);
                    LCItem lc;
                    if (GUIStates.ShowModifyLC)
                    {
                        lc = activeLC;
                        activeLC.Modify(tonnageLimit, curPadSize);
                        KCTGameStates.ActiveKSC.SwitchToPrevLaunchComplex();
                    }
                    else
                    {
                        lc = new LCItem(_newName, tonnageLimit, curPadSize, true, KCTGameStates.ActiveKSC);
                        KCTGameStates.ActiveKSC.LaunchComplexes.Add(lc);
                    }
                    lc.isOperational = false;

                    var lcConstr = new LCConstruction
                    {
                        LaunchComplexIndex = KCTGameStates.ActiveKSC.LaunchComplexes.IndexOf(lc),
                        Cost = totalCost,
                        Name = lcName
                    };
                    lcConstr.SetBP(totalCost);
                    KCTGameStates.ActiveKSC.LCConstructions.Add(lcConstr);

                    try
                    {
                        KCTEvents.OnLCConstructionQueued?.Fire(lcConstr, lc);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
                else
                {
                    ScreenMessages.PostScreenMessage("Not enough funds to " + (GUIStates.ShowModifyLC ? "renovate" : "build") + " this launch complex.");
                }
                GUIStates.ShowNewLC = false;
                GUIStates.ShowModifyLC = false;
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowBuildList = true;

                _padLvlOptions = null;
            }

            if (GUILayout.Button("No"))
            {
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                _padLvlOptions = null;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowBuildList = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            CenterWindow(ref _centralWindowPosition);
        }

        private static void LoadPadData()
        {
            var upgdFacility = KCT_LaunchPad.GetUpgradeableFacilityReference();
            if (upgdFacility == null)
                return;

            var padUpgdLvls = upgdFacility.UpgradeLevels;

            _padLvlOptions = new string[padUpgdLvls.Length + 1];
            _padLvlOptions[padUpgdLvls.Length] = "Custom";
            _padSizes = new Vector3[padUpgdLvls.Length];
            _padTons = new float[padUpgdLvls.Length];

            for (int i = 0; i < padUpgdLvls.Length; i++)
            {
                float normalizedLevel = (float)i / (float)upgdFacility.MaxLevel;
                float limit = GameVariables.Instance.GetCraftMassLimit(normalizedLevel, true);
                _padTons[i] = limit;
                var sLimit = limit == float.MaxValue ? "unlimited" : $"max {limit} tons";
                _padLvlOptions[i] = $"Level {i + 1} ({sLimit})";

                Vector3 sizeLimit = GameVariables.Instance.GetCraftSizeLimit(normalizedLevel, true);
                _padSizes[i] = sizeLimit;
            }
        }

        private static bool ValidateLCCreationParameters(string newName, float fractionalPadLvl, float tonnageLimit, Vector3 curPadSize, bool modify)
        {
            if (curPadSize == Vector3.zero)
            {
                ScreenMessages.PostScreenMessage("Please enter a valid size");
                return false;
            }

            if (modify)
                return true;

            if(fractionalPadLvl == -1 || tonnageLimit == 0)
            {
                ScreenMessages.PostScreenMessage("Please enter a valid tonnage limit");
                return false;
            }

            if (string.IsNullOrEmpty(_newName))
            {
                ScreenMessages.PostScreenMessage("Enter a name for the new launch complex");
                return false;
            }

            for (int i = 0; i < KCTGameStates.ActiveKSC.LaunchComplexes.Count; i++)
            {
                var lp = KCTGameStates.ActiveKSC.LaunchComplexes[i];
                if (string.Equals(lp.Name, _newName, StringComparison.OrdinalIgnoreCase))
                {
                    ScreenMessages.PostScreenMessage("Another launch complex with the same name already exists");
                    return false;
                }
            }

            return true;
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
