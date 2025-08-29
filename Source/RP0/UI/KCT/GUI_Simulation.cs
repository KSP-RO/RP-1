using System;
using UnityEngine;
using ROUtils;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private static Rect _simulationWindowPosition = new Rect((Screen.width - 250) / 2, (Screen.height - 250) / 2, 250, 1);
        private static Rect _simulationConfigPosition = new Rect((Screen.width / 2) - 150, (Screen.height / 4), 300, 1);
        private static Vector2 _bodyChooserScrollPos;

        private static string _sOrbitAlt = "", _sOrbitPe = "", _sOrbitAp = "", _sOrbitInc = "", _sOrbitLAN = "", _sOrbitMNA = "", _sOrbitArgPe = "", _UTString = "", _sDelay = "0";
        private static bool _fromCurrentUT = true;
        private static bool _circOrbit = true;

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
                SpaceCenterManagement.Instance.SimulationParams.Reset();
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
            SimulationParams simParams = SpaceCenterManagement.Instance.SimulationParams;
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Body: ");
            if (simParams == null) simParams = SpaceCenterManagement.Instance.SimulationParams = new SimulationParams();
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
                simParams.SimulateInOrbit = GUILayout.Toggle(simParams.SimulateInOrbit, " Start in orbit");
                if (simParams.SimulateInOrbit != changed)
                    _simulationConfigPosition.height = 1;
            }
            if (simParams.SimulationBody != Planetarium.fetch.Home || simParams.SimulateInOrbit)
            {
                _circOrbit = GUILayout.Toggle(_circOrbit, " Circular");
                if (_circOrbit)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Orbit Altitude (km): ");
                    _sOrbitAlt = GUILayout.TextField(_sOrbitAlt, GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Min: " + simParams.SimulationBody.atmosphereDepth / 1000 + "km");
                    GUILayout.Label("Max: " + Math.Floor((simParams.SimulationBody.sphereOfInfluence - simParams.SimulationBody.Radius) / 1000) + "km");
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Orbit Periapsis (km): ");
                    _sOrbitPe = GUILayout.TextField(_sOrbitPe, GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Orbit Apoapsis (km): ");
                    _sOrbitAp = GUILayout.TextField(_sOrbitAp, GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                }

                if (!simParams.SimulateInOrbit) simParams.SimulateInOrbit = true;
            }

            if (simParams.SimulateInOrbit)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Delay (s): ");
                _sDelay = GUILayout.TextField(_sDelay, 3, GUILayout.Width(40));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Inclination (degrees): ");
                _sOrbitInc = GUILayout.TextField(_sOrbitInc, GUILayout.Width(50));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("LAN (degrees): ");
                _sOrbitLAN = GUILayout.TextField(_sOrbitLAN, GUILayout.Width(50));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Mean Anomaly (radians): ");
                _sOrbitMNA = GUILayout.TextField(_sOrbitMNA, GUILayout.Width(50));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Argument of Periapsis (degrees): ");
                _sOrbitArgPe = GUILayout.TextField(_sOrbitArgPe, GUILayout.Width(50));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Time: ");
            _UTString = GUILayout.TextField(_UTString, GUILayout.Width(110));
            _fromCurrentUT = GUILayout.Toggle(_fromCurrentUT, new GUIContent(" From Now", "If selected the game will warp forwards by the entered value. Otherwise the date and time will be set to the entered value."));
            GUILayout.EndHorizontal();
            if (_fromCurrentUT)
            {
                GUILayout.Label("Valid formats: \"1y 2d 3h 4m 5s\" and \"31719845\".");
            }
            else
            {
                GUILayout.Label("Valid formats: \"1y 2d 3h 4m 5s\", \"31719845\", and \"1960-12-31 23:59:59\".");
            }
            GUILayout.Space(4);

            if (ModUtils.IsTestFlightInstalled || ModUtils.IsTestLiteInstalled)
            {
                simParams.DisableFailures = !GUILayout.Toggle(!simParams.DisableFailures, " Enable Part Failures (TestFlight or TestLite)");
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
                    SpaceCenterManagement.Instance.SimulationParams.SimulationBody = body;
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
            if (SpaceCenterManagement.Instance.IsSimulatedFlight)
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

            CelestialBody body = simParams.SimulationBody;

            if (body != Planetarium.fetch.Home)
                simParams.SimulateInOrbit = true;

            if (simParams.SimulateInOrbit)
            {
                if (_circOrbit)
                {
                    if (!double.TryParse(_sOrbitAlt, out simParams.SimOrbitAltitude))
                        simParams.SimOrbitAltitude = GetDefaultAltitudeForBody(body);
                    else
                        simParams.SimOrbitAltitude = EnsureSafeMaxAltitude(1000 * simParams.SimOrbitAltitude, body);

                    simParams.SimOrbitPe = simParams.SimOrbitAp = 0;
                }
                else
                {
                    if (!double.TryParse(_sOrbitPe, out simParams.SimOrbitPe))
                        simParams.SimOrbitPe = GetDefaultAltitudeForBody(body);

                    if (!double.TryParse(_sOrbitAp, out simParams.SimOrbitAp))
                        simParams.SimOrbitAp = GetDefaultAltitudeForBody(body);

                    simParams.SimOrbitAp = EnsureSafeMaxAltitude(1000 * simParams.SimOrbitAp, body);
                    simParams.SimOrbitPe = Math.Min(1000 * simParams.SimOrbitPe, simParams.SimOrbitAp);

                    simParams.SimOrbitAltitude = 0;
                }

                if (!double.TryParse(_sOrbitInc, out simParams.SimInclination))
                    simParams.SimInclination = 0;
                else
                    simParams.SimInclination %= 360;

                if (!double.TryParse(_sOrbitLAN, out simParams.SimLAN))
                    simParams.SimLAN = 0;
                else
                    simParams.SimLAN %= 360;

                if (!double.TryParse(_sOrbitMNA, out simParams.SimMNA))
                    simParams.SimMNA = Math.PI; // this will set it at apoapsis, good for safety
                else
                    simParams.SimMNA %= 2 * Math.PI;

                if (!double.TryParse(_sOrbitArgPe, out simParams.SimArgPe))
                    simParams.SimArgPe = 0;
                else
                    simParams.SimArgPe %= 360;
            }

            double currentUT = Planetarium.GetUniversalTime();
            double ut = 0;
            if (_fromCurrentUT && (_UTString.Contains("-") || _UTString.Contains(":"))) // prevent the user from doing 1960-12-31, accidentally selecting "From Now", and then warping 1960 years forward
            {
                var message = new ScreenMessage("Value must be of format \"1y 2d 3h 4m 5s\" or \"31719845\" when \"From Now\" is selected.", 6f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(message);
                return;
            }
            else if (!string.IsNullOrWhiteSpace(_UTString) && 
                     ((_UTString.Contains(":") && !System.Text.RegularExpressions.Regex.IsMatch(_UTString, @"^\d{4}-\d{2}-\d{2}")) || 
                      !ROUtils.DTUtils.TryParseTimeString(_UTString, isTimespan: !_fromCurrentUT, out ut))) // if string is not empty and ((string has HH:mm but no YYYY-MM-DD) or (string fails TryParseTimeString)), then output failure
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

            if (ModUtils.IsPrincipiaInstalled && simParams.SimulationUT != 0 && simParams.SimulationUT < currentUT + 0.5)
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
            SpaceCenterManagement.Instance.LaunchedVessel = new VesselProject(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, EditorLogic.FlagURL, true);
            // Just in case, let's set the LCID
            SpaceCenterManagement.Instance.LaunchedVessel.LCID = SpaceCenterManagement.EditorShipEditingMode ? SpaceCenterManagement.Instance.EditedVessel.LCID : SpaceCenterManagement.Instance.ActiveSC.ActiveLC.ID;

            VesselCrewManifest manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
            if (manifest == null)
            {
                manifest = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(EditorLogic.fetch.ship.SaveShip(), null, true);
            }
            EditorLogic.fetch.ship.SaveShip().Save(tempFile);
            SpaceCenterManagement.Instance.IsSimulatedFlight = true;
            string launchSiteName = EditorLogic.fetch.launchSiteName;
            if (launchSiteName == "LaunchPad" && SpaceCenterManagement.Instance.ActiveSC.ActiveLC.LCType == LaunchComplexType.Pad)
            {
                launchSiteName = SpaceCenterManagement.Instance.ActiveSC.ActiveLC.ActiveLPInstance.launchSiteName;
            }
            SpaceCenterManagement.Instance.StartCoroutine(CallbackUtil.DelayedCallback(1, delegate
            {
                FlightDriver.StartWithNewLaunch(tempFile, EditorLogic.FlagURL, launchSiteName, manifest);
            }));
        }

        private static double EnsureSafeMaxAltitude(double altitudeMeters, CelestialBody body)
        {
            return Math.Min(Math.Max(altitudeMeters, body.atmosphereDepth), body.sphereOfInfluence - body.Radius - 1000);
        }

        private static double GetDefaultAltitudeForBody(CelestialBody body)
        {
            return body.atmosphere ? body.atmosphereDepth + 30000 : 30000;
        }
    }
}
