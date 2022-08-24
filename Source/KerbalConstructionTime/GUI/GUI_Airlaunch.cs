using System;
using UnityEngine;
using RP0;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private const int _airLaunchWindowWidth = 300;
        
        private static Rect _airlaunchWindowPosition = new Rect(Screen.width - 300, 40, _airLaunchWindowWidth*UIHolder.UIScale, 1);
        private static string _sKscDistance = "500";
        private static string _sKscAzimuth = "90";
        private static string _sAltitude = "10000";
        private static string _sAzimuth = "270";
        private static string _sVelocity = "180";
        private static string _errorMsg;
        private static AirlaunchParams _airlaunchParams;

        public static void DrawAirlaunchWindow(int windowID)
        {
            if (_airlaunchParams == null)
            {
                _airlaunchParams = new AirlaunchParams();

                AirlaunchTechLevel lvl = KerbalConstructionTimeData.Instance.IsSimulatedFlight ? AirlaunchTechLevel.GetHighestLevelIncludingUnderResearch() :
                                                                        AirlaunchTechLevel.GetCurrentLevel();
                if (lvl != null)
                {
                    _sKscDistance = (lvl.MaxKscDistance / 1000).ToString();
                    _sAltitude = lvl.MaxAltitude.ToString();
                    _sVelocity = lvl.MaxVelocity.ToString();
                }
            }

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Distance from Space Center: ", GUILayout.ExpandWidth(true));
            _sKscDistance = GUILayout.TextField(_sKscDistance, UIHolder.MaxWidth(70f));
            GUILayout.Label("km", UIHolder.Width(25f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Azimuth from Space Center: ", GUILayout.ExpandWidth(true));
            _sKscAzimuth = GUILayout.TextField(_sKscAzimuth, UIHolder.MaxWidth(70f));
            GUILayout.Label("°", UIHolder.Width(25f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Launch azimuth: ", GUILayout.ExpandWidth(true));
            _sAzimuth = GUILayout.TextField(_sAzimuth, UIHolder.MaxWidth(70f));
            GUILayout.Label("°", UIHolder.Width(25f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Altitude from SL: ", GUILayout.ExpandWidth(true));
            _sAltitude = GUILayout.TextField(_sAltitude, UIHolder.MaxWidth(70f));
            GUILayout.Label("m", UIHolder.Width(25f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Velocity: ", GUILayout.ExpandWidth(true));
            _sVelocity = GUILayout.TextField(_sVelocity, UIHolder.MaxWidth(70f));
            GUILayout.Label("m/s", UIHolder.Width(25f));
            GUILayout.EndHorizontal();

            if (_errorMsg != null)
            {
                if (_orangeText == null)
                {
                    _orangeText = new GUIStyle(GUI.skin.label);
                    _orangeText.normal.textColor = XKCDColors.Orange;
                }

                GUILayout.Label(_errorMsg, _orangeText);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Launch"))
            {
                HandleLaunch();
            }
            if (GUILayout.Button("Cancel"))
            {
                var w = 150 * UIHolder.UIScale;
                _centralWindowPosition.width = w;
                _centralWindowPosition.x = (Screen.width - w) / 2;
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
                bool isSim = KerbalConstructionTimeData.Instance.IsSimulatedFlight;
                _airlaunchParams.KCTVesselId = isSim ? FlightGlobals.ActiveVessel.id : KCTGameStates.LaunchedVessel.shipID;
                _airlaunchParams.Altitude = double.Parse(_sAltitude);
                _airlaunchParams.Velocity = double.Parse(_sVelocity);
                _airlaunchParams.LaunchAzimuth = double.Parse(_sAzimuth);
                _airlaunchParams.KscDistance = double.Parse(_sKscDistance) * 1000;
                _airlaunchParams.KscAzimuth = double.Parse(_sKscAzimuth);

                bool valid = _airlaunchParams.Validate(out _errorMsg);
                if (valid)
                {
                    _errorMsg = null;
                    if (isSim)
                    {
                        var kct = KerbalConstructionTime.Instance;
                        kct.StartCoroutine(kct.AirlaunchRoutine(_airlaunchParams, _airlaunchParams.KCTVesselId, skipCountdown: true));
                        ToggleVisibility(false);
                        return;
                    }

                    KCTGameStates.AirlaunchParams = _airlaunchParams;
                    _airlaunchParams = null;

                    BuildListVessel b = KCTGameStates.LaunchedVessel;
                    if (!IsCrewable(b.ExtractedParts))
                    {
                        b.Launch();
                    }
                    else
                    {
                        GUIStates.ShowAirlaunch = false;
                        KCTGameStates.ToolbarControl?.SetFalse();
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
