using KerbalConstructionTime;
using RealFuels;
using UniLinq;
using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class GameplayTips : MonoBehaviour
    {
        private static bool _airlaunchTipShown;
        private static bool _isInterplanetaryWarningShown;

        private bool _subcribedToPAWEvent;
        private EventData<BuildListVessel> _onKctVesselAddedToBuildQueueEvent;

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

            _onKctVesselAddedToBuildQueueEvent = GameEvents.FindEvent<EventData<BuildListVessel>>("OnKctVesselAddedToBuildQueue");
            if (_onKctVesselAddedToBuildQueueEvent != null)
            {
                _onKctVesselAddedToBuildQueueEvent.Add(OnKctVesselAddedToBuildQueue);
            }

            var vessel = FlightGlobals.ActiveVessel;
            if (!_airlaunchTipShown && vessel &&
                KerbalConstructionTime.Utilities.IsSimulationActive &&
                vessel.GetVesselBuiltAt() == EditorFacility.SPH &&
                vessel.FindPartModuleImplementing<ModuleEngineConfigs>() != null)    // Does the vessel have a rocket engine?
            {
                ShowAirlaunchTip();
            }

            if (HighLogic.LoadedSceneIsEditor && !rp0Settings.RealChuteTipShown)
            {
                GameEvents.onPartActionUIShown.Add(OnPartActionUIShown);
                _subcribedToPAWEvent = true;
            }
        }

        public void OnDestroy()
        {
            if (_onKctVesselAddedToBuildQueueEvent != null) _onKctVesselAddedToBuildQueueEvent.Remove(OnKctVesselAddedToBuildQueue);
            if (_subcribedToPAWEvent) GameEvents.onPartActionUIShown.Remove(OnPartActionUIShown);
        }

        private void OnPartActionUIShown(UIPartActionWindow paw, Part part)
        {
            if (part.Modules.Contains("RealChuteModule"))
            {
                ShowRealChuteTip();
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

            string msg = $"Did you know that you can simulate airlaunches by clicking on the Space Center Management button?\n" +
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

        private void OnKctVesselAddedToBuildQueue(BuildListVessel data)
        {
            if (HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().NeverShowToolingReminders) return;

            bool hasUntooledParts = EditorLogic.fetch.ship.Parts.Any(p => p.FindModuleImplementing<ModuleTooling>()?.IsUnlocked() == false);
            if (hasUntooledParts)
            {
                ShowUntooledPartsReminder();
            }
        }

        private static void ShowUntooledPartsReminder()
        {
            string msg = $"Tool them in the RP-1 menu to reduce vessel cost and build time.";
            DialogGUIBase[] options = new DialogGUIBase[2];
            options[0] = new DialogGUIButton("OK", () => { });
            options[1] = new DialogGUIButton("Never remind me again", () => { HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().NeverShowToolingReminders = true; });
            MultiOptionDialog diag = new MultiOptionDialog("ShowUntooledPartsReminder", msg, "Untooled parts", null, 300, options);
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin);
        }

        private void ShowRealChuteTip()
        {
            var rp0Settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            if (rp0Settings.RealChuteTipShown) return;

            rp0Settings.RealChuteTipShown = true;

            string msg = "RealChute has very old UI. To resize and configure the chute, enter Action Groups mode by using the button in the top left corner. " +
                         "Then click on the part to open up the configuration UI.";
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowRealChuteTip",
                                         "Configuring parachutes",
                                         msg,
                                         "OK",
                                         false,
                                         HighLogic.UISkin);
        }
    }
}
