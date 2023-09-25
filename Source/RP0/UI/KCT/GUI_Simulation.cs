﻿using System;
using UnityEngine;

namespace RP0
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

            if (FlightDriver.CanRevertToPostInit && GUILayout.Button("Restart Simulation"))
            {
                GUIStates.ShowSimulationGUI = false;
                KCTUtilities.EnableSimulationLocks();
                FlightDriver.RevertToLaunch();
                KerbalConstructionTimeData.Instance.SimulationParams.Reset();
                _centralWindowPosition.height = 1;
            }

            if (FlightDriver.CanRevertToPrelaunch && GUILayout.Button("Revert to Editor"))
            {
                GUIStates.ShowSimulationGUI = false;
                KCTUtilities.DisableSimulationLocks();
                var facility = ShipConstruction.ShipType; // This uses stock behavior because the LaunchedVessel is no longer valid.
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
            SimulationParams simParams = KerbalConstructionTimeData.Instance.SimulationParams;
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Body: ");
            if (simParams == null) simParams = KerbalConstructionTimeData.Instance.SimulationParams = new SimulationParams();
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
            _UTString = GUILayout.TextField(_UTString, GUILayout.Width(110));
            _fromCurrentUT = GUILayout.Toggle(_fromCurrentUT, new GUIContent(" From Now", "If selected the game will warp forwards by the amount of time entered onto the field. Otherwise the date and time will be set to entered value."));
            GUILayout.EndHorizontal();
            GUILayout.Label("Accepts values with format \"1y 2d 3h 4m 5s\" or \"1960-01-01 15:30\"");
            GUILayout.Space(4);

            if (KCTUtilities.IsTestFlightInstalled || KCTUtilities.IsTestLiteInstalled)
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
                    KerbalConstructionTimeData.Instance.SimulationParams.SimulationBody = body;
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
            if (KerbalConstructionTimeData.Instance.IsSimulatedFlight)
            {
                string msg = "Current save already appears to be a simulation. Starting a simulation inside a simulation isn't allowed.";
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "simErrorPopup", "Simulation error", msg, "Understood", false, HighLogic.UISkin).HideGUIsWhilePopup();
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

            double currentUT = Planetarium.GetUniversalTime();
            double ut = 0;
            if (!string.IsNullOrWhiteSpace(_UTString) && !DTUtils.TryParseTimeString(_UTString, isTimespan: !_fromCurrentUT, out ut))
            {
                var message = new ScreenMessage("Please enter a valid time value.", 6f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(message);
                return;
            }

            simParams.DelayMoveSeconds = 0;
            if (_fromCurrentUT)
            {
                simParams.SimulationUT = ut != 0 ? currentUT + ut : 0;
            }
            else
            {
                simParams.SimulationUT = ut;
            }

            if (simParams.SimulationUT < 0)
            {
                var message = new ScreenMessage("Cannot set time further back than the game start", 6f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(message);
                return;
            }

            if (KCTUtilities.IsPrincipiaInstalled && simParams.SimulationUT != 0 && simParams.SimulationUT < currentUT + 0.5)
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
            KCTUtilities.MakeSimulationSave();

            // Create the LaunchedVessel fresh instead of cloning the EditorVessel, since it's possible that the player
            // may have changed the vessel slightly since the last time the coroutine updated the EditorVessell.
            KerbalConstructionTimeData.Instance.LaunchedVessel = new VesselProject(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, EditorLogic.FlagURL, true);
            // Just in case, let's set the LCID
            KerbalConstructionTimeData.Instance.LaunchedVessel.LCID = KerbalConstructionTime.EditorShipEditingMode ? KerbalConstructionTimeData.Instance.EditedVessel.LCID : KerbalConstructionTimeData.Instance.ActiveSC.ActiveLC.ID;

            VesselCrewManifest manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
            if (manifest == null)
            {
                manifest = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(EditorLogic.fetch.ship.SaveShip(), null, true);
            }
            EditorLogic.fetch.ship.SaveShip().Save(tempFile);
            KerbalConstructionTimeData.Instance.IsSimulatedFlight = true;
            string launchSiteName = EditorLogic.fetch.launchSiteName;
            if (launchSiteName == "LaunchPad" && KerbalConstructionTimeData.Instance.ActiveSC.ActiveLC.LCType == LaunchComplexType.Pad)
            {
                launchSiteName = KerbalConstructionTimeData.Instance.ActiveSC.ActiveLC.ActiveLPInstance.launchSiteName;
            }
            FlightDriver.StartWithNewLaunch(tempFile, EditorLogic.FlagURL, launchSiteName, manifest);
        }
    }
}
