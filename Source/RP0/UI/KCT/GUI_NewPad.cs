using System;
using UnityEngine;
using ROUtils;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private static string _newName = "Launch Complex 1";
        private static RenameType _renameType = RenameType.None;

        public static void DrawNewPadWindow(int windowID)
        {
            LaunchComplex curLC = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;

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
            float fractionalPadLvl = curLC.Stats.GetPadFracLevel();
            curLC.Stats.GetCostStats(out curPadCost, out _, out _);
            curPadCost *= Database.SettingsSC.AdditionalPadCostMult;

            if (curPadCost > 0)
            {
                double curPadBuildTime = ConstructionProject.CalculateBuildTime(curPadCost, 0d, SpaceCenterFacility.LaunchPad);
                string sBuildTime = KSPUtil.PrintDateDelta(curPadBuildTime, includeTime: false);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Cost:", GUILayout.ExpandWidth(false));
                GUILayout.Label($"√{-CurrencyUtils.Funds(TransactionReasonsRP0.StructureConstructionLC, -curPadCost):N0}", GetLabelRightAlignStyle());
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
                if (!KSPUtils.CurrentGameIsCareer())
                {
                    RP0Debug.Log("Building new launchpad!");
                    SpaceCenterManagement.Instance.ActiveSC.ActiveLC.LaunchPads.Add(new LCLaunchPad(id, _newName, fractionalPadLvl)
                    {
                        isOperational = true
                    });
                }
                else
                {
                    RP0Debug.Log("Building new launchpad!");
                    var lp = new LCLaunchPad(id, _newName, fractionalPadLvl);
                    SpaceCenterManagement.Instance.ActiveSC.ActiveLC.LaunchPads.Add(lp);

                    var padConstr = new PadConstructionProject
                    {
                        id = id,
                        cost = curPadCost,
                        name = _newName
                    };
                    padConstr.SetBP(curPadCost, 0d);
                    SpaceCenterManagement.Instance.ActiveSC.ActiveLC.PadConstructions.Add(padConstr);

                    try
                    {
                        SCMEvents.OnPadConstructionQueued?.Fire(padConstr, lp);
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
                        VesselProject b = KCTUtilities.FindVPByID(null, _selectedVesselId);
                        b.shipName = _newName; //Change the name from our point of view
                        break;
                    }
                    case RenameType.Pad:
                    {
                        LCLaunchPad lp = SpaceCenterManagement.Instance.ActiveSC.ActiveLC.ActiveLPInstance;
                        lp.Rename(_newName);
                        break;
                    }
                    case RenameType.LaunchComplex:
                    {
                        LaunchComplex lc = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;
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

            for (int i = 0; i < SpaceCenterManagement.Instance.ActiveSC.ActiveLC.LaunchPads.Count; i++)
            {
                var lp = SpaceCenterManagement.Instance.ActiveSC.ActiveLC.LaunchPads[i];
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
