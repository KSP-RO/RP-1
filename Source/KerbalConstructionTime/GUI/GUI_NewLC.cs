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
            if (tonnageLimit > 0f)
            {
                minTonnage = Mathf.Floor(tonnageLimit * 0.5f);
                if (minTonnage < 10f)
                    minTonnage = 0f;
            }
            else
            {
                minTonnage = 0f;
            }

            double mass = tonnageLimit;
            curPadCost = Math.Pow(mass, 0.5d) * 2500d + Math.Pow(mass, 1.5d) * 1.5d;
            curVABCost = padSize.sqrMagnitude * 2d;
            fractionalPadLvl = 0f;

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

        public static void DrawNewLCWindow(int windowID)
        {
            if (_padLvlOptions == null)
            {
                LoadPadData();
            }

            GUILayout.BeginVertical();
            GUILayout.Label("Name:");
            _newName = GUILayout.TextField(_newName);

            GUILayout.Label("Launch Complex Info:");

            double curPadCost = 0;
            double curVABCost = 0;
            float fractionalPadLvl = -1;
            float tonnageLimit = 0;
            float heightLimit = 0;
            float widthLimit = 0;
            float minTonnage = 0f;
            Vector3 curPadSize = Vector3.zero;

            GUILayout.Label("Maximum tonnage:");
            _tonnageLimit = GUILayout.TextField(_tonnageLimit);
            if (float.TryParse(_tonnageLimit, out tonnageLimit) && float.TryParse(_heightLimit, out heightLimit) && float.TryParse(_widthLimit, out widthLimit))
            {
                GetPadStats(tonnageLimit, new Vector3(widthLimit, heightLimit, widthLimit), out minTonnage, out curPadCost, out curVABCost, out fractionalPadLvl);
            }
            GUILayout.Label($"Minimum tonnage: {minTonnage:N}");
            GUILayout.Label("Size Limits:");
            _heightLimit = GUILayout.TextField(_heightLimit);
            _widthLimit = GUILayout.TextField(_widthLimit);

            double curLCCost = curPadCost + curVABCost;

            if (curLCCost > 0)
            {
                double curPadBuildTime = FacilityUpgrade.CalculateBuildTime(curLCCost, SpaceCenterFacility.LaunchPad);
                string sBuildTime = KSPUtil.PrintDateDelta(curPadBuildTime, includeTime: false);
                GUILayout.Label($"It will cost {Math.Round(curLCCost):N} funds to build the new launch complex, and additional pads there will cost {Math.Round(curPadCost):N}. " +
                                $"Estimated construction time is {sBuildTime}.");
            }

            GUILayout.Label("Would you like to build it?");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes") && ValidateLCCreationParameters(_newName, fractionalPadLvl, tonnageLimit, curPadSize))
            {

                GUIStates.ShowNewLC = false;
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowBuildList = true;

                if (!Utilities.CurrentGameIsCareer())
                {
                    KCTDebug.Log("Building new launch complex!");
                    KCTGameStates.ActiveKSC.LaunchComplexes.Add(new LCItem(_newName, tonnageLimit, curPadSize, true, KCTGameStates.ActiveKSC));
                }
                else if (Funding.CanAfford((float)curPadCost))
                {
                    KCTDebug.Log("Building new launch complex!");
                    Utilities.SpendFunds(curLCCost, TransactionReasons.StructureConstruction);
                    var lc = new LCItem(_newName, tonnageLimit, curPadSize, true, KCTGameStates.ActiveKSC);
                    KCTGameStates.ActiveKSC.LaunchComplexes.Add(lc);

                    var lcConstr = new LCConstruction
                    {
                        LaunchComplexIndex = KCTGameStates.ActiveKSC.LaunchComplexes.Count - 1,
                        Cost = curLCCost,
                        Name = _newName
                    };
                    lcConstr.SetBP(curPadCost);
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
                    ScreenMessages.PostScreenMessage("Not enough funds to build this launch complex.");
                }

                _padLvlOptions = null;
            }

            if (GUILayout.Button("No"))
            {
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                _padLvlOptions = null;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowBuildList = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            CenterWindow(ref _centralWindowPosition);
        }

        private static void LoadPadData()
        {
            var upgdFacility = KCT_LaunchPad.GetUpgradeableFacilityReference();
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

        private static bool ValidateLCCreationParameters(string newName, float fractionalPadLvl, float tonnageLimit, Vector3 curPadSize)
        {
            if (fractionalPadLvl == -1 || tonnageLimit == 0 || curPadSize == Vector3.zero)
            {
                ScreenMessages.PostScreenMessage("Please enter a valid pad size");
                return false;
            }

            if (string.IsNullOrEmpty(_newName))
            {
                ScreenMessages.PostScreenMessage("Enter a name for the new launchpad");
                return false;
            }

            for (int i = 0; i < KCTGameStates.ActiveKSC.LaunchComplexes.Count; i++)
            {
                var lp = KCTGameStates.ActiveKSC.LaunchComplexes[i];
                if (string.Equals(lp.LCName, _newName, StringComparison.OrdinalIgnoreCase))
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
