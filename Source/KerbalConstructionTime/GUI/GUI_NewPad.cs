using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static string _newName = "";
        private static RenameType _renameType = RenameType.None;

        public static void DrawNewPadWindow(int windowID)
        {
            if (_padLvlOptions == null)
            {
                LoadPadData();
            }

            GUILayout.BeginVertical();
            GUILayout.Label("Name:");
            _newName = GUILayout.TextField(_newName);
            LCItem curLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            
            GUILayout.Label($"Maximum tonnage: {curLC.massMax:N}");
            GUILayout.Label($"Minimum tonnage: {curLC.massMin:N}");
            GUILayout.Label($"Size limit: {curLC.sizeMax.x:#.#}x{curLC.sizeMax.y:#.#}m");

            double curPadCost;
            float fractionalPadLvl;
            GetPadStats(curLC.massMax, curLC.sizeMax, out _, out curPadCost, out _, out fractionalPadLvl);

            if (curPadCost > 0)
            {
                double curPadBuildTime = FacilityUpgrade.CalculateBuildTime(curPadCost, SpaceCenterFacility.LaunchPad);
                string sBuildTime = KSPUtil.PrintDateDelta(curPadBuildTime, includeTime: false);
                GUILayout.Label($"It will cost {Math.Round(curPadCost):N} funds to build the new launchpad. " +
                                $"Estimated construction time is {sBuildTime}.");
            }

            GUILayout.Label("Would you like to build it?");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes") && ValidatePadCreationParameters())
            {

                GUIStates.ShowNewPad = false;
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowBuildList = true;

                if (!Utilities.CurrentGameIsCareer())
                {
                    KCTDebug.Log("Building new launchpad!");
                    KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LaunchPads.Add(new KCT_LaunchPad(_newName, fractionalPadLvl, curLC.massMax, curLC.sizeMax));
                }
                else if (Funding.CanAfford((float)curPadCost))
                {
                    KCTDebug.Log("Building new launchpad!");
                    Utilities.SpendFunds(curPadCost, TransactionReasons.StructureConstruction);
                    var lp = new KCT_LaunchPad(_newName, fractionalPadLvl, curLC.massMax, curLC.sizeMax);
                    KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LaunchPads.Add(lp);

                    var padConstr = new PadConstruction
                    {
                        LaunchpadIndex = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LaunchPads.Count - 1,
                        Cost = curPadCost,
                        Name = _newName
                    };
                    padConstr.SetBP(curPadCost);
                    KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.PadConstructions.Add(padConstr);

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

                _padLvlOptions = null;
            }

            if (GUILayout.Button("No"))
            {
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
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
                switch (_renameType)
                {
                    case RenameType.Vessel:
                    {
                        BuildListVessel b = Utilities.FindBLVesselByID(_selectedVesselId);
                        b.ShipName = _newName; //Change the name from our point of view
                        b.ShipNode.SetValue("ship", _newName);
                        break;
                    }
                    case RenameType.Pad:
                    {
                        KCT_LaunchPad lp = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.ActiveLPInstance;
                        lp.Rename(_newName);
                        break;
                    }
                    case RenameType.LaunchComplex:
                    {
                        LCItem lc = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
                        lc.Rename(_newName);
                        break;
                    }
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
        private static bool ValidatePadCreationParameters()
        {
            if (string.IsNullOrEmpty(_newName))
            {
                ScreenMessages.PostScreenMessage("Enter a name for the new launchpad");
                return false;
            }

            for (int i = 0; i < KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LaunchPads.Count; i++)
            {
                var lp = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LaunchPads[i];
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
