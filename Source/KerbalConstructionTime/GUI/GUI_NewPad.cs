using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static int _selectedPadIdx = 0;
        private static string[] _padLvlOptions = null;
        private static double[] _padCosts = null;

        private static string _newName = "";
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
            _selectedPadIdx = GUILayout.SelectionGrid(_selectedPadIdx, _padLvlOptions, 1);

            double curPadCost = _padCosts[_selectedPadIdx];
            double curPadBuildTime = FacilityUpgrade.CalculateBuildTime(curPadCost, SpaceCenterFacility.LaunchPad);
            string sBuildTime = KSPUtil.PrintDateDelta(curPadBuildTime, true);

            GUILayout.Label($"It will cost {Math.Round(curPadCost, 2):N} funds to build the new launchpad. " +
                            $"Estimated construction time is {sBuildTime}. Would you like to build it?");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes"))
            {
                if (string.IsNullOrEmpty(_newName))
                {
                    ScreenMessages.PostScreenMessage("Enter a name for the new launchpad");
                    return;
                }

                for (int i = 0; i < KCTGameStates.ActiveKSC.LaunchPads.Count; i++)
                {
                    var lp = KCTGameStates.ActiveKSC.LaunchPads[i];
                    if (string.Equals(lp.name, _newName, StringComparison.OrdinalIgnoreCase))
                    {
                        ScreenMessages.PostScreenMessage("Another launchpad with the same name already exists");
                        return;
                    }
                }

                GUIStates.ShowNewPad = false;
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowBuildList = true;

                if (!Utilities.CurrentGameIsCareer())
                {
                    KCTDebug.Log("Building new launchpad!");
                    KCTGameStates.ActiveKSC.LaunchPads.Add(new KCT_LaunchPad(_newName, _selectedPadIdx));
                }
                else if (Funding.CanAfford((float)curPadCost))
                {
                    KCTDebug.Log("Building new launchpad!");
                    Utilities.SpendFunds(curPadCost, TransactionReasons.StructureConstruction);
                    KCTGameStates.ActiveKSC.LaunchPads.Add(new KCT_LaunchPad(_newName, -1));
                    FacilityUpgrade newPad = new FacilityUpgrade
                    {
                        FacilityType = SpaceCenterFacility.LaunchPad,
                        Id = KCT_LaunchPad.LPID,
                        IsLaunchpad = true,
                        LaunchpadID = KCTGameStates.ActiveKSC.LaunchPads.Count - 1,
                        UpgradeLevel = _selectedPadIdx,
                        CurrentLevel = -1,
                        Cost = curPadCost,
                        CommonName = _newName
                    };
                    newPad.SetBP(curPadCost);
                    try
                    {
                        KCTEvents.OnFacilityUpgradeQueued?.Fire(newPad);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                    KCTGameStates.ActiveKSC.KSCTech.Add(newPad);
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
            KCT_LaunchPad lp = KCTGameStates.ActiveKSC.ActiveLPInstance;
            var list = lp.GetUpgradeableFacilityReferences();
            var upgdFacility = list[0];
            var padUpgdLvls = upgdFacility.UpgradeLevels;

            _padLvlOptions = new string[padUpgdLvls.Length];
            _padCosts = new double[padUpgdLvls.Length];

            for (int i = 0; i < padUpgdLvls.Length; i++)
            {
                float limit = GameVariables.Instance.GetCraftMassLimit((float)i / (float)upgdFacility.MaxLevel, true);
                var sLimit = limit == float.MaxValue ? "unlimited" : $"max {limit} tons";
                _padLvlOptions[i] = $"Level {i + 1} ({sLimit})";

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
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
