using RealFuels;
using System.Collections;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    public class GameplayTips : MonoBehaviour
    {
        private static bool _airlaunchTipShown;
        private static bool _isInterplanetaryWarningShown;

        private bool _subcribedToPAWEvent;
        private bool _hasNewPurchasableRATL;
        private int _highestUnlockableRALvl;

        public static GameplayTips Instance { get; private set; }

        internal void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
        }

        internal void Start()
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

            if (HighLogic.LoadedSceneIsFlight)
            {
                var vessel = FlightGlobals.ActiveVessel;
                if (!_airlaunchTipShown && vessel &&
                    SpaceCenterManagement.Instance.IsSimulatedFlight &&
                    vessel.GetVesselBuiltAt() == EditorFacility.SPH &&
                    vessel.FindPartModuleImplementing<ModuleEngineConfigs>() != null)    // Does the vessel have a rocket engine?
                {
                    ShowAirlaunchTip();
                }

                StartCoroutine(CheckLandedWhileActuallyFlying());
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                LoadRAUpgradeStatus(rp0Settings);

                if (_hasNewPurchasableRATL)
                {
                    GameEvents.onPartActionUIShown.Add(OnPartActionUIShown);
                    _subcribedToPAWEvent = true;
                }
            }
        }

        internal void OnDestroy()
        {
            if (_subcribedToPAWEvent) GameEvents.onPartActionUIShown.Remove(OnPartActionUIShown);
        }

        private void OnPartActionUIShown(UIPartActionWindow paw, Part part)
        {
            if (_hasNewPurchasableRATL && part.Modules.Contains("ModuleRealAntenna"))
            {
                ShowRATechAvailableTip();
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
                                         KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                         false,
                                         HighLogic.UISkin,
                                         false);
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
                                         KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                         false,
                                         HighLogic.UISkin).HideGUIsWhilePopup();
        }

        public void CheckAndShowExcessECTip(ShipConstruct ship)
        {
            if (ShipHasExcessEC(ship))
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "ShowExcessECTip",
                                             KSP.Localization.Localizer.GetStringByTag("#rp0_GameplayTip_ExcessEC_Title"),
                                             KSP.Localization.Localizer.GetStringByTag("#rp0_GameplayTip_ExcessEC_Text"),
                                             KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                             false,
                                             HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }

        public bool ShipHasExcessEC(ShipConstruct ship)
        {
            const string techId = "electronicsSatellite";
            if (ResearchAndDevelopment.GetTechnologyState(techId) == RDTech.State.Available)
            {
                // No longer check this beyond the early game
                return false;
            }

            double ecTotal = ship.Parts.Sum(p => p.Resources.Get("ElectricCharge")?.maxAmount ?? 0);
            return ecTotal > 10000;
        }

        private void ShowRATechAvailableTip()
        {
            var rp0Settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            rp0Settings.RATLTipShown = _highestUnlockableRALvl;
            _hasNewPurchasableRATL = false;

            string msg = $"Communications Tech Level {_highestUnlockableRALvl} has been researched but not purchased. Purchase it in the R&D building to use higher-tech comms.";
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowRATLTip",
                                         "New comms tech available",
                                         msg,
                                         KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                         false,
                                         HighLogic.UISkin).HideGUIsWhilePopup();
        }

        private IEnumerator CheckLandedWhileActuallyFlying()
        {
            while (true)
            {
                yield return new WaitForSeconds(5f);

                Vessel v = FlightGlobals.ActiveVessel;
                if (v != null && v.LandedOrSplashed)
                {
                    const float checkThreshold = 150;
                    if (v.altitude > 0 &&                                             // Apparently the radar altitude is calculated from the bottom of the sea as soon as the vessel goes under the water
                        v.radarAltitude - v.vesselSize.magnitude > checkThreshold)
                    {
                        string msg = "Error: the game thinks you've landed while you're actually flying. Revert to launch and try again.";
                        PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                             new Vector2(0.5f, 0.5f),
                             "ShowLandedWhileActuallyFlyingTip",
                             "Vessel landed",
                             msg,
                             "OK",
                             false,
                             HighLogic.UISkin);
                        yield break;
                    }
                }
            }
        }

        private static readonly Dictionary<string, bool> _lackTrainingsCache = new Dictionary<string, bool>();

        public void ShowUntrainedTip(List<Part> craftParts)
        {
            if (HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().NeverShowUntrainedReminders || !HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsTrainingEnabled)
                return;

            List<AvailablePart> parts = new List<AvailablePart>();
            foreach (var p in craftParts)
            {
                if (p.CrewCapacity == 0)
                    continue;

                // This will check if the part requires training and report the synonym
                // to use later. If it doesn't need training, we just skip then and there.
                if (!Crew.TrainingDatabase.TrainingExists(p.name, out string training))
                    continue;

                // If we've already encountered this training type, use the cached state
                if (_lackTrainingsCache.TryGetValue(training, out bool state))
                {
                    if (!state && !parts.Contains(p.partInfo))
                        parts.Add(p.partInfo);

                    continue;
                }

                // Now we have to trawl through all crew and their trainings, and courses
                bool found = false;
                // First check courses, they're less expensive.
                foreach (var c in Crew.CrewHandler.Instance.TrainingCourses)
                {
                    // A mission course implies proficiency, so use either type here
                    if (c.Target == training)
                    {
                        found = true;
                        break;
                    }
                }
                // Now do the full search
                if (!found)
                {
                    foreach (var pcm in HighLogic.CurrentGame.CrewRoster.Crew)
                    {
                        if (pcm.type != ProtoCrewMember.KerbalType.Crew)
                            continue;

                        // Directly check for the training, we've already boiled it down
                        // to the right synonym to use. Only check prof, not mission.
                        if (Crew.CrewHandler.Instance.NautHasTrainingForPart(pcm, training, false))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                _lackTrainingsCache[training] = found;
                if (!found)
                    parts.Add(p.partInfo);
            }
            _lackTrainingsCache.Clear();

            if (parts.Count == 0)
                return;

            string partStr = parts[0].title;
            for (int i = 1; i < parts.Count; ++i)
                partStr += "\n" + parts[i].title;
            DialogGUIBase[] options = new DialogGUIBase[2];
            options[0] = new DialogGUIButton(KSP.Localization.Localizer.Format("#autoLOC_190905"), () => { });
            options[1] = new DialogGUIButton(KSP.Localization.Localizer.Format("#rp0_GameplayTip_DontShowAgain"), () => { HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().NeverShowUntrainedReminders = true; });
            MultiOptionDialog diag = new MultiOptionDialog("ShowUntrainedPartsReminder",
                KSP.Localization.Localizer.Format("#rp0_GameplayTip_LaunchUntrainedPart_Text", partStr),
                KSP.Localization.Localizer.Format("#rp0_GameplayTip_LaunchUntrainedPart_Title"), null, 500, options);
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
        }

        public void ShowHSFProgramTip()
        {
            if (HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().NeverShowHSFProgramReminders || !HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsTrainingEnabled)
                return;

            DialogGUIBase[] options = new DialogGUIBase[2];
            options[0] = new DialogGUIButton(KSP.Localization.Localizer.Format("#autoLOC_190905"), () => { });
            options[1] = new DialogGUIButton(KSP.Localization.Localizer.Format("#rp0_GameplayTip_DontShowAgain"), () => { HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().NeverShowHSFProgramReminders = true; });
            MultiOptionDialog diag = new MultiOptionDialog("ShowHSFProgramReminder",
                KSP.Localization.Localizer.Format("#rp0_GameplayTip_LaunchUntrainedPart_Text"),
                KSP.Localization.Localizer.Format("#rp0_GameplayTip_LaunchUntrainedPart_Title"), null, 300, options);
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
        }

        private void LoadRAUpgradeStatus(RP0Settings rp0Settings)
        {
            _highestUnlockableRALvl = rp0Settings.RATLTipShown;
            const int tlUpgradeCount = 9;
            for (int i = rp0Settings.RATLTipShown + 1; i <= tlUpgradeCount; i++)
            {
                string upgradeName = "commsTL" + i;
                if (PartUpgradeManager.Handler.IsEnabled(upgradeName))
                {
                    rp0Settings.RATLTipShown = i;
                    continue;
                }
        
                if (PartUpgradeManager.Handler.IsAvailableToUnlock(upgradeName))
                {
                    _highestUnlockableRALvl = i;
                    _hasNewPurchasableRATL = true;
                    continue;
                }
                break;
            }
        }
    }
}
