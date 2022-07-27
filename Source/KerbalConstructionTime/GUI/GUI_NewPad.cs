using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static string _newName = "Launch Complex 1";
        private static RenameType _renameType = RenameType.None;

        public static void DrawNewPadWindow(int windowID)
        {
            LCItem curLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.ExpandWidth(false));
            _newName = GUILayout.TextField(_newName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Maximum tonnage:", GUILayout.ExpandWidth(false));
            GUILayout.Label($"{curLC.MassMax:N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Minimum tonnage:", GUILayout.ExpandWidth(false));
            GUILayout.Label($"{curLC.MassMin:N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Size Limits:", GUILayout.ExpandWidth(false));
            GUILayout.Label(curLC.SupportedSizeAsPrettyText, GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Human Rated:");
            GUILayout.Label(curLC.IsHumanRated ? "Yes" : "No", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            double curPadCost;
            float fractionalPadLvl;
            Utilities.GetPadStats(curLC.MassMax, curLC.SizeMax, curLC.IsHumanRated, out curPadCost, out _, out fractionalPadLvl);
            curPadCost *= PresetManager.Instance.ActivePreset.GeneralSettings.AdditionalPadCostMult;

            if (curPadCost > 0)
            {
                double curPadBuildTime = ConstructionBuildItem.CalculateBuildTime(curPadCost, SpaceCenterFacility.LaunchPad);
                string sBuildTime = KSPUtil.PrintDateDelta(curPadBuildTime, includeTime: false);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Cost:", GUILayout.ExpandWidth(false));
                GUILayout.Label($"√{curPadCost:N0}", GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Est. construction time:", GUILayout.ExpandWidth(false));
                GUILayout.Label(sBuildTime, GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Build") && ValidatePadCreationParameters())
            {

                GUIStates.ShowNewPad = false;
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowBuildList = true;

                Guid id = Guid.NewGuid();
                if (!Utilities.CurrentGameIsCareer())
                {
                    KCTDebug.Log("Building new launchpad!");
                    KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LaunchPads.Add(new KCT_LaunchPad(id, _newName, fractionalPadLvl)
                    {
                        isOperational = true
                    });
                }
                else
                {
                    KCTDebug.Log("Building new launchpad!");
                    var lp = new KCT_LaunchPad(id, _newName, fractionalPadLvl);
                    KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LaunchPads.Add(lp);

                    var padConstr = new PadConstruction
                    {
                        ID = id,
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
            }

            if (GUILayout.Button("Cancel"))
            {
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
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
                        BuildListVessel b = Utilities.FindBLVesselByID(null, _selectedVesselId);
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
