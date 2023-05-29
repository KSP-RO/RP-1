using System.Collections.Generic;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        public static void DrawClearLaunch(int windowID)
        {
            GUILayout.BeginVertical();
            if (GUILayout.Button("Recover Flight and Proceed"))
            {
                GUIStates.ShowClearLaunch = false;

                List<ProtoVessel> list = ShipConstruction.FindVesselsLandedAt(HighLogic.CurrentGame.flightState, KerbalConstructionTimeData.Instance.LaunchedVessel.launchSite);
                foreach (ProtoVessel pv in list)
                    ShipConstruction.RecoverVesselFromFlight(pv, HighLogic.CurrentGame.flightState);

                if (GUIStates.ShowAirlaunch)
                {
                    // Will be shown automatically as soon as GUIStates.showClearLaunch is set to false
                }
                else
                {
                    if (!KerbalConstructionTimeData.Instance.LaunchedVessel.IsCrewable())
                    {
                        KerbalConstructionTimeData.Instance.LaunchedVessel.Launch();
                    }
                    else
                    {
                        AssignInitialCrew();
                        GUIStates.ShowShipRoster = true;
                    }
                }
                _centralWindowPosition.height = 1;
            }

            if (GUILayout.Button("Cancel"))
            {
                KerbalConstructionTimeData.Instance.LaunchedVessel = new BuildListVessel();
                GUIStates.ShowClearLaunch = false;
                GUIStates.ShowAirlaunch = false;
                GUIStates.ShowBuildList = true;
                _centralWindowPosition.height = 1;
            }
            GUILayout.EndVertical();
            CenterWindow(ref _centralWindowPosition);
        }
    }
}
