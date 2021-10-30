using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static int _selectedPadIdx = 0;
        private static string[] _padLvlOptions = null;
        private static double[] _padCosts = null;
        private static float[] _padTons = null;
        private static Vector3[] _padSizes;

        private static string _newName = "";
        private static string _tonnageLimit = "60";
        private static bool _isRenamingLaunchPad = false;

        public static void DrawNewPadWindow(int windowID)
        {
            if (_padCosts == null || _padLvlOptions == null)
            {
                LoadPadNamesAndCosts();
            }

            GUILayout.BeginVertical();
            GUILayout.Label("Name:");
            _newName = GUILayout.TextField(_newName);

            GUILayout.Label("Pad level:");
            _selectedPadIdx = GUILayout.SelectionGrid(_selectedPadIdx, _padLvlOptions, 2);

            const float unlimitedTonnageThreshold = 3500;
            Vector3 unlimitedSizeThreshold = new Vector3(70, 130, 70);

            double curPadCost;
            float fractionalPadLvl = -1;
            float tonnageLimit = 0;
            Vector3 curPadSize = Vector3.zero;

            int customPadIdx = _padLvlOptions.Length - 1;
            if (_selectedPadIdx == customPadIdx)
            {
                curPadCost = 0;

                GUILayout.Label("Tonnage limit:");
                _tonnageLimit = GUILayout.TextField(_tonnageLimit);
                if (float.TryParse(_tonnageLimit, out tonnageLimit) && tonnageLimit >= _padTons[0])
                {
                    if (tonnageLimit >= unlimitedTonnageThreshold)
                    {
                        int padLvl = _padLvlOptions.Length - 2;
                        tonnageLimit = unlimitedTonnageThreshold;
                        curPadSize = _padSizes[padLvl];
                        curPadCost = _padCosts[padLvl];
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

                                var s1 = _padSizes[i - 1];
                                var s2 = Vector3.Min(_padSizes[i], unlimitedSizeThreshold);
                                curPadSize = s1 + (s2 - s1) * fractionOverFullLvl;

                                var c1 = _padCosts[i - 1];
                                var c2 = _padCosts[i];
                                curPadCost = c1 + (c2 - c1) * fractionOverFullLvl;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                curPadSize = _padSizes[_selectedPadIdx];
                curPadCost = _padCosts[_selectedPadIdx];
            }

            if (curPadSize != Vector3.zero)
            {
                if (curPadSize.y == float.MaxValue)
                {
                    GUILayout.Label($"Size limit: unlimited");
                }
                else
                {
                    GUILayout.Label($"Size limit: {curPadSize.x:#.#}x{curPadSize.y:#.#}m");
                }
            }

            if (curPadCost > 0)
            {
                double curPadBuildTime = FacilityUpgrade.CalculateBuildTime(curPadCost, SpaceCenterFacility.LaunchPad);
                string sBuildTime = KSPUtil.PrintDateDelta(curPadBuildTime, includeTime: false);
                GUILayout.Label($"It will cost {Math.Round(curPadCost):N} funds to build the new launchpad. " +
                                $"Estimated construction time is {sBuildTime}.");
            }

            GUILayout.Label("Would you like to build it?");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes") && ValidatePadCreationParameters(_newName, fractionalPadLvl, tonnageLimit, curPadSize))
            {

                GUIStates.ShowNewPad = false;
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowBuildList = true;

                if (!Utilities.CurrentGameIsCareer())
                {
                    KCTDebug.Log("Building new launchpad!");
                    KCTGameStates.ActiveKSC.LaunchPads.Add(new KCT_LaunchPad(_newName, fractionalPadLvl, tonnageLimit, curPadSize));
                }
                else if (Funding.CanAfford((float)curPadCost))
                {
                    KCTDebug.Log("Building new launchpad!");
                    Utilities.SpendFunds(curPadCost, TransactionReasons.StructureConstruction);
                    var lp = new KCT_LaunchPad(_newName, fractionalPadLvl, tonnageLimit, curPadSize);
                    KCTGameStates.ActiveKSC.LaunchPads.Add(lp);

                    var padConstr = new PadConstruction
                    {
                        LaunchpadIndex = KCTGameStates.ActiveKSC.LaunchPads.Count - 1,
                        Cost = curPadCost,
                        Name = _newName
                    };
                    padConstr.SetBP(curPadCost);
                    KCTGameStates.ActiveKSC.PadConstructions.Add(padConstr);

                    try
                    {
                        KCTEvents.OnPadConstructionQueued?.Fire(padConstr, lp);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
                else
                {
                    ScreenMessages.PostScreenMessage("Not enough funds to build this launchpad.");
                }

                _padCosts = null;
                _padLvlOptions = null;
                _costOfNewLP = int.MinValue;
            }

            if (GUILayout.Button("No"))
            {
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                _padCosts = null;
                _padLvlOptions = null;
                GUIStates.ShowNewPad = false;
                GUIStates.ShowBuildList = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            CenterWindow(ref _centralWindowPosition);
        }

        public static void DrawRenameWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Name:");
            _newName = GUILayout.TextField(_newName);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                if (!_isRenamingLaunchPad)
                {
                    BuildListVessel b = Utilities.FindBLVesselByID(_selectedVesselId);
                    b.ShipName = _newName; //Change the name from our point of view
                    b.ShipNode.SetValue("ship", _newName);
                }
                else
                {
                    KCT_LaunchPad lp = KCTGameStates.ActiveKSC.ActiveLPInstance;
                    lp.Rename(_newName);
                }
                GUIStates.ShowRename = false;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowBuildList = true;
            }
            if (GUILayout.Button("Cancel"))
            {
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowRename = false;
                GUIStates.ShowBuildList = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            CenterWindow(ref _centralWindowPosition);
        }

        private static void LoadPadNamesAndCosts()
        {
            var upgdFacility = KCT_LaunchPad.GetUpgradeableFacilityReference();
            var padUpgdLvls = upgdFacility.UpgradeLevels;

            _padLvlOptions = new string[padUpgdLvls.Length + 1];
            _padLvlOptions[padUpgdLvls.Length] = "Custom";
            _padCosts = new double[padUpgdLvls.Length];
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

                if (i > 0)
                {
                    var lvl = padUpgdLvls[i];
                    _padCosts[i] = _padCosts[i - 1] + lvl.levelCost;
                }
                else
                {
                    // Use the KCT formula for determining the cost of first level
                    _padCosts[0] = _costOfNewLP;
                }
            }
        }

        private static bool ValidatePadCreationParameters(string newName, float fractionalPadLvl, float tonnageLimit, Vector3 curPadSize)
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

            for (int i = 0; i < KCTGameStates.ActiveKSC.LaunchPads.Count; i++)
            {
                var lp = KCTGameStates.ActiveKSC.LaunchPads[i];
                if (string.Equals(lp.name, _newName, StringComparison.OrdinalIgnoreCase))
                {
                    ScreenMessages.PostScreenMessage("Another launchpad with the same name already exists");
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
