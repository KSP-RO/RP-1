using RealFuels;
using UnityEngine;
using KerbalConstructionTime;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class GameplayTips : MonoBehaviour
    {
        private static bool _airlaunchTipShown;
        private static bool _isInterplanetaryWarningShown;

        public static GameplayTips Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
        }

        private void Start()
        {
            var rp0Settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();

            // Nothing gets saved in simulations. Use static fields to pass the information over to the editor scene where it gets correctly persisted.
            if (_isInterplanetaryWarningShown)
            {
                rp0Settings.Avionics_InterplanetaryWarningShown = true;
            }
            _isInterplanetaryWarningShown |= rp0Settings.Avionics_InterplanetaryWarningShown;

            if (_airlaunchTipShown)
            {
                rp0Settings.AirlaunchTipShown = true;
            }
            _airlaunchTipShown |= rp0Settings.AirlaunchTipShown;

            var vessel = FlightGlobals.ActiveVessel;
            if (!_airlaunchTipShown && vessel &&
                KerbalConstructionTime.Utilities.IsSimulationActive &&
                vessel.GetVesselBuiltAt() == EditorFacility.SPH &&
                vessel.FindPartModuleImplementing<ModuleEngineConfigs>() != null)    // Does the vessel have a rocket engine?
            {
                ShowAirlaunchTip();
            }
        }

        public void ShowInterplanetaryAvionicsReminder()
        {
            if (_isInterplanetaryWarningShown) return;

            _isInterplanetaryWarningShown = true;
            HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().Avionics_InterplanetaryWarningShown = true;

            string altitudeThreshold = $"{ModuleAvionics.InterplanetaryAltitudeThreshold / 1000:N0} km";
            string msg = $"Near-Earth Avionics only provide control closer than {altitudeThreshold} from {Planetarium.fetch.Home.name}. " +
                         $"Only fore/aft translation is available at this point." +
                         $"\nConfigure the avionics as Deep Space for full control.";
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowInterplanetaryAvionicsReminder",
                                         "Deep Space Avionics",
                                         msg,
                                         "OK",
                                         false,
                                         HighLogic.UISkin);
        }

        private void ShowAirlaunchTip()
        {
            _airlaunchTipShown = true;
            HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().AirlaunchTipShown = true;

            string msg = $"Did you know that you can simulate airlaunches by clicking on the KCT button?\n" +
                         $"Tech levels that are in the research queue will be available for simulation purposes.";
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowAirlaunchTip",
                                         "Simulate airlaunch",
                                         msg,
                                         "OK",
                                         false,
                                         HighLogic.UISkin);
        }
    }
}
