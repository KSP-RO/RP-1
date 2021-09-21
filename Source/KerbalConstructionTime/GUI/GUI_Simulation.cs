using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static Rect _simulationWindowPosition = new Rect((Screen.width - 250) / 2, (Screen.height - 250) / 2, 250, 1);
        private static Rect _simulationConfigPosition = new Rect((Screen.width / 2) - 150, (Screen.height / 4), 300, 1);
        private static Vector2 _bodyChooserScrollPos;

        private static string _sOrbitAlt = "", _sOrbitInc = "", _UTString = "", _sDelay = "0";
        private static bool _fromCurrentUT = true;

        public static void DrawSimulationWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("This is a simulation.");
            GUILayout.Label("All progress will be lost after leaving the flight scene.");

            // TODO: Disabled for now. Would require some changes to not allow building vessels with locked parts.
            //if (!IsPrimarilyDisabled && !KCTGameStates.EditorShipEditingMode && GUILayout.Button("Build It!"))
            //{
            //    KCTGameStates.SimulationParams.BuildSimulatedVessel = true;
            //    KCTDebug.Log("Ship added from simulation.");
            //    var message = new ScreenMessage("Vessel will be added to the build queue after returning to the editor or space center", 6f, ScreenMessageStyle.UPPER_CENTER);
            //    ScreenMessages.PostScreenMessage(message);
            //}

            if (FlightDriver.CanRevertToPostInit && GUILayout.Button("Restart Simulation"))
            {
                GUIStates.ShowSimulationGUI = false;
                Utilities.EnableSimulationLocks();
                FlightDriver.RevertToLaunch();
                KCTGameStates.SimulationParams.Reset();
                _centralWindowPosition.height = 1;
            }

            if (FlightDriver.CanRevertToPrelaunch && GUILayout.Button("Revert to Editor"))
            {
                GUIStates.ShowSimulationGUI = false;
                Utilities.DisableSimulationLocks();
                var facility = KCTGameStates.LaunchedVessel.Type == BuildListVessel.ListType.VAB ? EditorFacility.VAB : EditorFacility.SPH;
                FlightDriver.RevertToPrelaunch(facility);
                _centralWindowPosition.height = 1;
            }

            if (GUILayout.Button("Close"))
            {
                GUIStates.ShowSimulationGUI = !GUIStates.ShowSimulationGUI;
            }
            GUILayout.EndVertical();

            if (_simulationWindowPosition.width > 250)
                _simulationWindowPosition.width = 250;

            CenterWindow(ref _simulationWindowPosition);
        }

        public static void DrawSimulationConfigure(int windowID)
        {
            SimulationParams simParams = KCTGameStates.SimulationParams;
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Body: ");
            if (simParams == null) simParams = KCTGameStates.SimulationParams = new SimulationParams();
            if (simParams.SimulationBody == null)
            {
                simParams.SimulationBody = Planetarium.fetch.Home;
            }
            GUILayout.Label(simParams.SimulationBody.bodyName);
            if (GUILayout.Button("Select", GUILayout.ExpandWidth(false)))
            {
                GUIStates.ShowSimConfig = false;
                GUIStates.ShowSimBodyChooser = true;
                _centralWindowPosition.height = 1;
                _simulationConfigPosition.height = 1;
            }
            GUILayout.EndHorizontal();
            if (simParams.SimulationBody == Planetarium.fetch.Home)
            {
                bool changed = simParams.SimulateInOrbit;
                simParams.SimulateInOrbit = GUILayout.Toggle(simParams.SimulateInOrbit, " Start in orbit?");
                if (simParams.SimulateInOrbit != changed)
                    _simulationConfigPosition.height = 1;
            }
            if (simParams.SimulationBody != Planetarium.fetch.Home || simParams.SimulateInOrbit)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Orbit Altitude (km): ");
                _sOrbitAlt = GUILayout.TextField(_sOrbitAlt, GUILayout.Width(100));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Min: " + simParams.SimulationBody.atmosphereDepth / 1000);
                GUILayout.Label("Max: " + Math.Floor(simParams.SimulationBody.sphereOfInfluence) / 1000);
                GUILayout.EndHorizontal();

                if (!simParams.SimulateInOrbit) simParams.SimulateInOrbit = true;
            }

            if (simParams.SimulateInOrbit)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Delay: (s)");
                _sDelay = GUILayout.TextField(_sDelay, 3, GUILayout.Width(40));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Inclination: ");
                _sOrbitInc = GUILayout.TextField(_sOrbitInc, GUILayout.Width(50));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Time: ");
            _UTString = GUILayout.TextField(_UTString, GUILayout.Width(100));
            _fromCurrentUT = GUILayout.Toggle(_fromCurrentUT, new GUIContent(" From Now", "If selected the game will warp forwards by the amount of time entered onto the field. Otherwise the date and time will be set to entered value."));
            GUILayout.EndHorizontal();
            GUILayout.Label("Accepts values with format \"1y 2d 3h 4m 5s\"");
            GUILayout.Space(4);

            if (Utilities.IsTestFlightInstalled || Utilities.IsTestLiteInstalled)
            {
                simParams.DisableFailures = !GUILayout.Toggle(!simParams.DisableFailures, " Enable Part Failures");
                GUILayout.Space(4);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Simulate"))
            {
                StartSim(simParams);
            }

            if (GUILayout.Button("Cancel"))
            {
                GUIStates.ShowSimConfig = false;
                _centralWindowPosition.height = 1;
                _unlockEditor = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            CheckEditorLock();
            CenterWindow(ref _simulationConfigPosition);
        }

        public static void DrawBodyChooser(int windowID)
        {
            _bodyChooserScrollPos = GUILayout.BeginScrollView(_bodyChooserScrollPos, GUILayout.Height(500));
            GUILayout.BeginVertical();
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (GUILayout.Button(body.bodyName))
                {
                    KCTGameStates.SimulationParams.SimulationBody = body;
                    GUIStates.ShowSimBodyChooser = false;
                    GUIStates.ShowSimConfig = true;
                    _centralWindowPosition.height = 1;
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            CheckEditorLock();
            CenterWindow(ref _centralWindowPosition);
        }

        private static void StartSim(SimulationParams simParams)
        {
            if (KCTGameStates.IsSimulatedFlight)
            {
                string msg = "Current save already appears to be a simulation. Starting a simulation inside a simulation isn't allowed.";
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "simErrorPopup", "KCT Simulation error", msg, "Understood", false, HighLogic.UISkin);
                return;
            }

            if (EditorLogic.fetch.ship.Count == 0)
            {
                var message = new ScreenMessage("Can't simulate without a vessel", 6f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(message);
                return;
            }

            if (simParams.SimulationBody != Planetarium.fetch.Home)
                simParams.SimulateInOrbit = true;

            if (simParams.SimulateInOrbit)
            {
                if (!double.TryParse(_sOrbitAlt, out simParams.SimOrbitAltitude))
                    simParams.SimOrbitAltitude = simParams.SimulationBody.atmosphere ? simParams.SimulationBody.atmosphereDepth + 20000 : 20000;
                else
                    simParams.SimOrbitAltitude = Math.Min(Math.Max(1000 * simParams.SimOrbitAltitude, simParams.SimulationBody.atmosphereDepth), simParams.SimulationBody.sphereOfInfluence);

                if (!double.TryParse(_sOrbitInc, out simParams.SimInclination))
                    simParams.SimInclination = 0;
                else
                    simParams.SimInclination %= 360;
            }

            double currentUT = Utilities.GetUT();
            simParams.DelayMoveSeconds = 0;
            if (_fromCurrentUT)
            {
                double utOffset = MagiCore.Utilities.ParseTimeString(_UTString, false);
                simParams.SimulationUT = utOffset != 0 ? currentUT + utOffset : 0;
            }
            else
            {
                simParams.SimulationUT = MagiCore.Utilities.ParseTimeString(_UTString, true);
            }

            if (simParams.SimulationUT < 0)
            {
                var message = new ScreenMessage("Cannot set time further back than the game start", 6f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(message);
                return;
            }

            if (Utilities.IsPrincipiaInstalled && simParams.SimulationUT != 0 && simParams.SimulationUT < currentUT + 0.5)
            {
                var message = new ScreenMessage("Going backwards in time isn't allowed with Principia", 6f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(message);
                return;
            }

            int.TryParse(_sDelay, out simParams.DelayMoveSeconds);
            if (simParams.SimulationUT < 0)
                simParams.SimulationUT = currentUT;

            //_unlockEditor = true;
            GUIStates.ShowSimConfig = false;
            _centralWindowPosition.height = 1;
            string tempFile = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/Ships/temp.craft";
            Utilities.MakeSimulationSave();

            Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
            double effCost = Utilities.GetEffectiveCost(EditorLogic.fetch.ship.Parts);
            double bp = Utilities.GetBuildTime(effCost);
            KCTGameStates.LaunchedVessel = new BuildListVessel(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, effCost, bp, EditorLogic.FlagURL);

            VesselCrewManifest manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
            if (manifest == null)
            {
                manifest = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(EditorLogic.fetch.ship.SaveShip(), null, true);
            }
            EditorLogic.fetch.ship.SaveShip().Save(tempFile);
            KCTGameStates.IsSimulatedFlight = true;
            FlightDriver.StartWithNewLaunch(tempFile, EditorLogic.FlagURL, EditorLogic.fetch.launchSiteName, manifest);
        }
    }
}
