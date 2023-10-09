using System;
using UnityEngine;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private static Rect _airlaunchWindowPosition = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 300, 300, 1);
        private static string _sKscDistance = "500";
        private static string _sKscAzimuth = "90";
        private static string _sAltitude = "10000";
        private static string _sAzimuth = "270";
        private static string _sVelocity = "180";
        private static string _errorMsg;
        private static AirlaunchParams _airlaunchParams;

        private static void ResetToMax()
        {
            AirlaunchTechLevel lvl = SpaceCenterManagement.Instance.IsSimulatedFlight ? AirlaunchTechLevel.GetHighestLevelIncludingUnderResearch() :
                                                                        AirlaunchTechLevel.GetCurrentLevel();
            if (lvl != null)
            {
                _airlaunchParams.KscDistance = lvl.MaxKscDistance;
                _airlaunchParams.Altitude = lvl.MaxAltitude;
                _airlaunchParams.Velocity = lvl.MaxVelocity;
            }
        }

        private static void SetAirlaunchStrings(bool all)
        {
            _sKscDistance = (_airlaunchParams.KscDistance / 1000).ToString();
            _sAltitude = _airlaunchParams.Altitude.ToString();
            _sVelocity = _airlaunchParams.Velocity.ToString();
            if (all)
            {
                _sAzimuth = _airlaunchParams.LaunchAzimuth.ToString();
                _sKscAzimuth = _airlaunchParams.KscAzimuth.ToString();
            }
        }

        public static void DrawAirlaunchWindow(int windowID)
        {
            if (_airlaunchParams == null)
            {
                _airlaunchParams = new AirlaunchParams(SpaceCenterManagement.Instance.AirlaunchParams);

                if (_airlaunchParams.Altitude == 0)
                    ResetToMax();

                SetAirlaunchStrings(true);
            }

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Distance from Space Center: ", GUILayout.ExpandWidth(true));
            _sKscDistance = GUILayout.TextField(_sKscDistance, GUILayout.MaxWidth(70f));
            GUILayout.Label("km", GUILayout.Width(25f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Azimuth from Space Center: ", GUILayout.ExpandWidth(true));
            _sKscAzimuth = GUILayout.TextField(_sKscAzimuth, GUILayout.MaxWidth(70f));
            GUILayout.Label("°", GUILayout.Width(25f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Launch azimuth: ", GUILayout.ExpandWidth(true));
            _sAzimuth = GUILayout.TextField(_sAzimuth, GUILayout.MaxWidth(70f));
            GUILayout.Label("°", GUILayout.Width(25f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Altitude from SL: ", GUILayout.ExpandWidth(true));
            _sAltitude = GUILayout.TextField(_sAltitude, GUILayout.MaxWidth(70f));
            GUILayout.Label("m", GUILayout.Width(25f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Velocity: ", GUILayout.ExpandWidth(true));
            _sVelocity = GUILayout.TextField(_sVelocity, GUILayout.MaxWidth(70f));
            GUILayout.Label("m/s", GUILayout.Width(25f));
            GUILayout.EndHorizontal();

            try
            {
                _airlaunchParams.Altitude = double.Parse(_sAltitude);
                _airlaunchParams.Velocity = double.Parse(_sVelocity);
                _airlaunchParams.LaunchAzimuth = double.Parse(_sAzimuth);
                _airlaunchParams.KscDistance = double.Parse(_sKscDistance) * 1000;
                _airlaunchParams.KscAzimuth = double.Parse(_sKscAzimuth);

                _airlaunchParams.Validate(out _errorMsg);
            }
            catch
            {
                _errorMsg = "Parsing error";
            }

            if (_errorMsg != null)
            {
                if (_orangeText == null)
                {
                    _orangeText = new GUIStyle(GUI.skin.label);
                    _orangeText.normal.textColor = XKCDColors.Orange;
                }

                GUILayout.Label(_errorMsg, _orangeText);
            }
            else
            {
                GUILayout.Label("");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Launch"))
            {
                HandleLaunch();
            }
            if (GUILayout.Button("Max"))
            {
                ResetToMax();
                SetAirlaunchStrings(false);
            }
            if (GUILayout.Button("Cancel"))
            {
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowAirlaunch = false;
                GUIStates.ShowBuildList = true;
                _errorMsg = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
            ClampWindow(ref _airlaunchWindowPosition, strict: true);
        }

        private static void HandleLaunch()
        {
            try
            {
                bool isSim = SpaceCenterManagement.Instance.IsSimulatedFlight;
                _airlaunchParams.KCTVesselId = isSim ? FlightGlobals.ActiveVessel.id : SpaceCenterManagement.Instance.LaunchedVessel.shipID;
                
                bool valid = _airlaunchParams.Validate(out _errorMsg);
                if (valid)
                {
                    _errorMsg = null;
                    if (isSim)
                    {
                        var kct = SpaceCenterManagement.Instance;
                        kct.StartCoroutine(kct.AirlaunchRoutine(_airlaunchParams, _airlaunchParams.KCTVesselId, skipCountdown: true));
                        ToggleVisibility(false);
                        return;
                    }

                    SpaceCenterManagement.Instance.AirlaunchParams = _airlaunchParams;
                    _airlaunchParams = null;

                    VesselProject b = SpaceCenterManagement.Instance.LaunchedVessel;
                    if (!b.IsCrewable())
                    {
                        b.Launch();
                    }
                    else
                    {
                        GUIStates.ShowAirlaunch = false;
                        SpaceCenterManagement.ToolbarControl?.SetFalse();
                        _centralWindowPosition.height = 1;
                        AssignInitialCrew();
                        GUIStates.ShowShipRoster = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMsg = ex.Message;
                Debug.LogException(ex);
            }
        }
    }
}
