using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToolbarControl_NS;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace KerbalConstructionTime
{
    public class KerbalConstructionTime : MonoBehaviour
    {
        public static KerbalConstructionTime Instance { get; private set; }

        public bool IsEditorRecalcuationRequired;

        private static bool _isGUIInitialized = false;

        private WaitForSeconds _wfsHalf = null, _wfsOne = null, _wfsTwo = null;
        private bool _isIconUpdated = false;
        private double _lastUT = 0;

        internal const string KCTLaunchLock = "KCTLaunchLock";
        internal const string KCTKSCLock = "KCTKSCLock";
        public static readonly Dictionary<string, KCTCostModifier> KCTCostModifiers = new Dictionary<string, KCTCostModifier>();

        private DateTime _simMoveDeferTime = DateTime.MaxValue;
        private int _simMoveSecondsRemain = 0;

        private GameObject _simWatermark;

        internal void OnFacilityContextMenuSpawn(KSCFacilityContextMenu menu)
        {
            if (KCT_GUI.IsPrimarilyDisabled) return;

            var overrider = new KSCContextMenuOverrider(menu);
            StartCoroutine(overrider.OnContextMenuSpawn());
        }

        public void OnDestroy()
        {
            _simWatermark?.DestroyGameObject();

            if (KCTGameStates.ToolbarControl != null)
            {
                KCTGameStates.ToolbarControl.OnDestroy();
                Destroy(KCTGameStates.ToolbarControl);
            }
        }

        internal void OnGUI()
        {
            if (Utilities.CurrentGameIsMission()) return;

            if (!_isGUIInitialized)
            {
                KCT_GUI.InitBuildListVars();
                KCT_GUI.InitBuildPlans();
                KCT_GUI.InitDevPartsToggle();
                _isGUIInitialized = true;
            }
            KCT_GUI.SetGUIPositions();
        }

        public void Awake()
        {
            if (Utilities.CurrentGameIsMission()) return;

            KCTDebug.Log("Awake called");
            KCTGameStates.PersistenceLoaded = false;

            Instance = this;

            KCTGameStates.Settings.Load();

            if (!File.Exists(PresetManager.SettingsFilePath))
            {
                KCTGameStates.IsFirstStart = true;
            }

            if (PresetManager.Instance == null)
            {
                PresetManager.Instance = new PresetManager();
            }
            PresetManager.Instance.SetActiveFromSaveData();

            // Create events for other mods
            if (!KCTEvents.Instance.CreatedEvents)
            {
                KCTEvents.Instance.CreateEvents();
            }

            var obj = new GameObject("KCTToolbarControl");
            KCTGameStates.ToolbarControl = obj.AddComponent<ToolbarControl>();
            KCTGameStates.ToolbarControl.AddToAllToolbars(null, null,
                null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.VAB,
                KCTGameStates._modId,
                "MainButton",
                Utilities._icon_KCT_On_38,
                Utilities._icon_KCT_Off_38,
                Utilities._icon_KCT_On_24,
                Utilities._icon_KCT_Off_24,
                KCTGameStates._modName
                );

            KCTGameStates.ToolbarControl.AddLeftRightClickCallbacks(KCT_GUI.ClickToggle, KCT_GUI.OnRightClick);
        }

        public void Start()
        {
            KCTDebug.Log("Start called");
            _wfsOne = new WaitForSeconds(1f);
            _wfsTwo = new WaitForSeconds(2f);
            _wfsHalf = new WaitForSeconds(0.5f);

            if (Utilities.CurrentGameIsMission()) return;

            // Subscribe to events from KSP and other mods
            if (!KCTEvents.Instance.SubscribedToEvents)
            {
                KCTEvents.Instance.SubscribeToEvents();
            }

            if (KCTGameStates.IsFirstStart)
            {
                PresetManager.Instance.SaveActiveToSaveData();

            }

            // Ghetto event queue
            if (HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                InvokeRepeating("EditorRecalculation", 1, 1);

                KCT_GUI.BuildRateForDisplay = null;
                if (!KCT_GUI.IsPrimarilyDisabled)
                {
                    Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                }
            }

            if (KCT_GUI.IsPrimarilyDisabled &&
                InputLockManager.GetControlLock(KCTLaunchLock) == ControlTypes.EDITOR_LAUNCH)
            {
                InputLockManager.RemoveControlLock(KCTLaunchLock);
            }

            KACWrapper.InitKACWrapper();

            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
            {
                if (InputLockManager.GetControlLock(KCTKSCLock) == ControlTypes.KSC_FACILITIES)
                    InputLockManager.RemoveControlLock(KCTKSCLock);
                return;
            }

            //Begin primary mod functions

            KCT_GUI.GuiDataSaver.Load();
            KCT_GUI.GUIStates.HideAllNonMainWindows();

            if (!HighLogic.LoadedSceneIsFlight)
            {
                bool b = KCTGameStates.SimulationParams.BuildSimulatedVessel;
                KCTGameStates.SimulationParams.Reset();
                if (b && KCTGameStates.LaunchedVessel != null)
                {
                    Utilities.AddVesselToBuildList(KCTGameStates.LaunchedVessel);
                }
            }

            switch (HighLogic.LoadedScene)
            {
                case GameScenes.EDITOR:
                    KCT_GUI.HideAll();
                    if (!KCT_GUI.IsPrimarilyDisabled)
                    {
                        KCT_GUI.GUIStates.ShowEditorGUI = KCTGameStates.ShowWindows[1];
                        if (KCTGameStates.EditorShipEditingMode)
                            KCT_GUI.EnsureEditModeIsVisible();
                        else
                            KCT_GUI.ToggleVisibility(KCT_GUI.GUIStates.ShowEditorGUI);
                    }
                    break;
                case GameScenes.SPACECENTER:
                    bool shouldStart = KCT_GUI.GUIStates.ShowFirstRun;
                    KCT_GUI.HideAll();
                    KCTGameStates.ClearVesselEditMode();
                    if (!shouldStart)
                    {
                        KCT_GUI.GUIStates.ShowBuildList = KCTGameStates.ShowWindows[0];
                        KCT_GUI.ToggleVisibility(KCT_GUI.GUIStates.ShowBuildList);
                    }
                    KCT_GUI.GUIStates.ShowFirstRun = shouldStart;
                    StartCoroutine(UpdateActiveLPLevel());
                    StartCoroutine(UpdateBuildRates());
                    break;
                case GameScenes.TRACKSTATION:
                    KCTGameStates.ClearVesselEditMode();
                    break;
                case GameScenes.FLIGHT:
                    if (!KCTGameStates.IsSimulatedFlight &&
                        FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH &&
                        FlightGlobals.ActiveVessel.GetCrewCount() == 0 && KCTGameStates.LaunchedCrew.Count > 0)
                    {
                        KerbalRoster roster = HighLogic.CurrentGame.CrewRoster;
                        foreach (Part p in FlightGlobals.ActiveVessel.parts)
                        {
                            KCTDebug.Log("Part being tested: " + p.partInfo.title);
                            if (!(KCTGameStates.LaunchedCrew.Find(part => part.PartID == p.craftID) is CrewedPart cp)) 
                                continue;
                            List<ProtoCrewMember> crewList = cp.CrewList;
                            KCTDebug.Log("cP.crewList.Count: " + cp.CrewList.Count);
                            foreach (ProtoCrewMember crewMember in crewList)
                            {
                                if (crewMember != null)     // Can this list can have null ProtoCrewMembers?
                                {
                                    ProtoCrewMember finalCrewMember = crewMember;
                                    if (crewMember.type == ProtoCrewMember.KerbalType.Crew)
                                    {
                                        finalCrewMember = roster.Crew.FirstOrDefault(c => c.name == crewMember.name);
                                    }
                                    else if (crewMember.type == ProtoCrewMember.KerbalType.Tourist)
                                    {
                                        finalCrewMember = roster.Tourist.FirstOrDefault(c => c.name == crewMember.name);
                                    }
                                    try
                                    {
                                        if (finalCrewMember is ProtoCrewMember && p.AddCrewmember(finalCrewMember))
                                        {
                                            KCTDebug.Log($"Assigned {finalCrewMember.name } to {p.partInfo.name}");
                                            finalCrewMember.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
                                            finalCrewMember.seat?.SpawnCrew();
                                        } else
                                        {
                                            KCTDebug.LogError($"Error when assigning {crewMember.name} to {p.partInfo.name}");
                                            finalCrewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        KCTDebug.LogError($"Error when assigning {crewMember.name} to {p.partInfo.name}: {ex}");
                                        finalCrewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                                    }
                                }
                            }
                        }
                        KCTGameStates.LaunchedCrew.Clear();
                    }

                    KCT_GUI.HideAll();
                    if (!KCTGameStates.IsSimulatedFlight && KCTGameStates.LaunchedVessel != null && FlightGlobals.ActiveVessel?.situation == Vessel.Situations.PRELAUNCH)
                    {
                        KCTGameStates.LaunchedVessel.KSC = null; //it's invalid now
                        KCTDebug.Log("Attempting to remove launched vessel from build list");
                        if (KCTGameStates.LaunchedVessel.RemoveFromBuildList()) //Only do these when the vessel is first removed from the list
                        {
                            //Add the cost of the ship to the funds so it can be removed again by KSP
                            Utilities.AddFunds(KCTGameStates.LaunchedVessel.Cost, TransactionReasons.VesselRollout);
                            FlightGlobals.ActiveVessel.vesselName = KCTGameStates.LaunchedVessel.ShipName;
                        }

                        if (KCTGameStates.ActiveKSC.Recon_Rollout.FirstOrDefault(r => r.AssociatedID == KCTGameStates.LaunchedVessel.Id.ToString()) is ReconRollout rollout)
                            KCTGameStates.ActiveKSC.Recon_Rollout.Remove(rollout);

                        if (KCTGameStates.ActiveKSC.AirlaunchPrep.FirstOrDefault(r => r.AssociatedID == KCTGameStates.LaunchedVessel.Id.ToString()) is AirlaunchPrep alPrep)
                            KCTGameStates.ActiveKSC.AirlaunchPrep.Remove(alPrep);

                        if (KCTGameStates.AirlaunchParams is AirlaunchParams alParams && alParams.KCTVesselId == KCTGameStates.LaunchedVessel.Id &&
                            (!alParams.KSPVesselId.HasValue || alParams.KSPVesselId == FlightGlobals.ActiveVessel.id))
                        {
                            if (!alParams.KSPVesselId.HasValue) alParams.KSPVesselId = FlightGlobals.ActiveVessel.id;
                            StartCoroutine(AirlaunchRoutine(alParams, FlightGlobals.ActiveVessel.id));
                        }
                    }
                    break;
            }
            KCTDebug.Log("Start finished");

            DelayedStart();

            UpdateTechlistIconColor();
            StartCoroutine(HandleEditorButton_Coroutine());
        }

        internal IEnumerator AirlaunchRoutine(AirlaunchParams launchParams, Guid vesselId, bool skipCountdown = false)
        {
            if (!skipCountdown)
                yield return _wfsTwo;

            for (int i = 10; i > 0 && !skipCountdown; i--)
            {
                if (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.id != vesselId)
                {
                    ScreenMessages.PostScreenMessage("[KCT] Airlaunch cancelled", 5f, ScreenMessageStyle.UPPER_CENTER, XKCDColors.Red);
                    yield break;
                }

                if (i == 1 && FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
                {
                    // Make sure that the vessel situation transitions from Prelaunch to Landed before airlaunching
                    FlightGlobals.ActiveVessel.situation = Vessel.Situations.LANDED;
                }

                ScreenMessages.PostScreenMessage($"[KCT] Launching in {i}...", 1f, ScreenMessageStyle.UPPER_CENTER, XKCDColors.Red);
                yield return _wfsOne;
            }

            HyperEdit_Utilities.DoAirlaunch(launchParams);

            if (Utilities.IsPrincipiaInstalled)
                StartCoroutine(ClobberPrincipia());
        }

        /// <summary>
        /// Need to keep the vessel in Prelaunch state for a while if Principia is installed.
        /// Otherwise the vessel will spin out in a random way.
        /// </summary>
        /// <returns></returns>
        private IEnumerator ClobberPrincipia()
        {
            if (FlightGlobals.ActiveVessel == null)
                yield return null;

            const int maxFramesWaited = 250;
            int i = 0;
            do
            {
                FlightGlobals.ActiveVessel.situation = Vessel.Situations.PRELAUNCH;
                yield return new WaitForFixedUpdate();
            } while (FlightGlobals.ActiveVessel.packed && i++ < maxFramesWaited);
        }

        protected void EditorRecalculation()
        {
            if (IsEditorRecalcuationRequired && !KCT_GUI.IsPrimarilyDisabled)
            {
                Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                IsEditorRecalcuationRequired = false;
            }
        }

        /// <summary>
        /// Coroutine to reset the launch button handlers every 1/2 second
        /// Needed because KSP seems to change them behind the scene sometimes
        /// </summary>
        /// <returns></returns>
        IEnumerator HandleEditorButton_Coroutine()
        {
            while (true)
            {
                if (HighLogic.LoadedSceneIsEditor)
                    Utilities.HandleEditorButton();
                yield return _wfsHalf;
            }
        }

        public void Update()
        {
            // Move constantly-checked things that don't need physics precision to here.
            if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                Utilities.SetActiveKSCToRSS();

            if (!KCT_GUI.IsPrimarilyDisabled && HighLogic.LoadedScene == GameScenes.SPACECENTER &&
                VesselSpawnDialog.Instance?.Visible == true)
            {
                VesselSpawnDialog.Instance.ButtonClose();
                KCTDebug.Log("Attempting to close spawn dialog!");
            }
        }

        public void FixedUpdate()
        {
            if (Utilities.CurrentGameIsMission()) return;
            if (!PresetManager.Instance?.ActivePreset?.GeneralSettings.Enabled == true)
                return;
            if (!KCT_GUI.IsPrimarilyDisabled)
                ProgressBuildTime();

            if (HighLogic.LoadedScene == GameScenes.FLIGHT && KCTGameStates.IsSimulatedFlight && KCTGameStates.SimulationParams != null)
            {
                ProcessSimulation();
            }


        }

        // Ran every 30 FixedUpdates, which we will treat as 0.5 seconds for now.
        private IEnumerator UpdateActiveLPLevel()
        {
            // Only run during Space Center in career mode
            yield return new WaitForFixedUpdate();
            while (HighLogic.LoadedScene == GameScenes.SPACECENTER && Utilities.CurrentGameIsCareer())
            {
                if (KCTGameStates.ActiveKSC?.ActiveLPInstance is KCT_LaunchPad pad)
                {
                    if (Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.LaunchPad) != pad.level)
                    {
                        KCTGameStates.ActiveKSC.SwitchLaunchPad(KCTGameStates.ActiveKSC.ActiveLaunchPadID, false);
                        pad.UpdateLaunchpadDestructionState(false);
                    }
                }
                yield return _wfsHalf;
            }
        }

        private IEnumerator UpdateBuildRates()
        {
            yield return new WaitForFixedUpdate();  // TODO: Check why we stall here versus run in Start()
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER
                && ScenarioUpgradeableFacilities.GetFacilityLevelCount(SpaceCenterFacility.VehicleAssemblyBuilding) >= 0)
            {
                KCTDebug.Log("Updating build rates");
                foreach (KSCItem KSC in KCTGameStates.KSCs)
                {
                    KSC?.RecalculateBuildRates();
                    KSC?.RecalculateUpgradedBuildRates();
                }

                KCTDebug.Log("Rates updated");

                foreach (SpaceCenterFacility facility in Enum.GetValues(typeof(SpaceCenterFacility)))
                {
                    KCTGameStates.BuildingMaxLevelCache[facility.ToString()] = ScenarioUpgradeableFacilities.GetFacilityLevelCount(facility);
                    KCTDebug.Log($"Cached {facility} max at {KCTGameStates.BuildingMaxLevelCache[facility.ToString()]}");
                }
            }
        }

        private void ProcessSimulation()
        {
            HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = false;

            SimulationParams simParams = KCTGameStates.SimulationParams;
            if (FlightGlobals.ActiveVessel.loaded && !FlightGlobals.ActiveVessel.packed && !simParams.IsVesselMoved)
            {
                if (simParams.DisableFailures)
                {
                    Utilities.ToggleFailures(!simParams.DisableFailures);
                }
                if (!simParams.SimulateInOrbit || !FlightDriver.CanRevertToPrelaunch)
                {
                    // Either the player does not want to start in orbit or they saved and then loaded back into that save
                    simParams.IsVesselMoved = true;
                    return;
                }

                int secondsForMove = simParams.DelayMoveSeconds;
                if (_simMoveDeferTime == DateTime.MaxValue)
                {
                    _simMoveDeferTime = DateTime.Now;
                }
                else if (DateTime.Now.CompareTo(_simMoveDeferTime.AddSeconds(secondsForMove)) > 0)
                {
                    StartCoroutine(SetSimOrbit(simParams));
                    simParams.IsVesselMoved = true;
                    _simMoveDeferTime = DateTime.MaxValue;
                }

                if (_simMoveDeferTime != DateTime.MaxValue && _simMoveSecondsRemain != (_simMoveDeferTime.AddSeconds(secondsForMove) - DateTime.Now).Seconds)
                {
                    double remaining = (_simMoveDeferTime.AddSeconds(secondsForMove) - DateTime.Now).TotalSeconds;
                    ScreenMessages.PostScreenMessage($"Moving vessel in {Math.Round(remaining)} seconds", (float)(remaining - Math.Floor(remaining)), ScreenMessageStyle.UPPER_CENTER);
                    _simMoveSecondsRemain = (int)remaining;
                }
            }
        }

        private static IEnumerator SetSimOrbit(SimulationParams simParams)
        {
            yield return new WaitForEndOfFrame();
            KCTDebug.Log($"Moving vessel to orbit. {simParams.SimulationBody.bodyName}:{simParams.SimOrbitAltitude}:{simParams.SimInclination}");
            HyperEdit_Utilities.PutInOrbitAround(simParams.SimulationBody, simParams.SimOrbitAltitude, simParams.SimInclination);
        }

        private void AddSimulationWatermark()
        {
            if (!KCTGameStates.Settings.ShowSimWatermark) return;

            var uiController = KSP.UI.UIMasterController.Instance;
            if (uiController == null)
            {
                KCTDebug.LogError("UIMasterController.Instance is null");
                return;
            }

            _simWatermark = new GameObject();
            _simWatermark.transform.SetParent(uiController.mainCanvas.transform, false);
            _simWatermark.name = "sim-watermark";

            var c = Color.gray;
            c.a = 0.65f;
            var text = _simWatermark.AddComponent<Text>();
            text.text = "Simulation";
            text.font = UISkinManager.defaultSkin.font;
            text.fontSize = (int)(40 * uiController.uiScale);
            text.color = c;
            text.alignment = TextAnchor.MiddleCenter;

            var rectTransform = text.GetComponent<RectTransform>();
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.85f);
            rectTransform.sizeDelta = new Vector2(190 * uiController.uiScale, 50 * uiController.uiScale);

            if (DateTime.Today.Month == 4 && DateTime.Today.Day == 1)
            {
                text.text = "Activate Windows";
                rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.8f, 0.2f);
                rectTransform.sizeDelta = new Vector2(300 * uiController.uiScale, 50 * uiController.uiScale);
            }
        }

        // Ran every FixedUpdate.  Will treat as 10/sec.
        public void ProgressBuildTime()
        {
            Profiler.BeginSample("KCT ProgressBuildTime");
            // Support EditorTime
            double UT = HighLogic.LoadedSceneIsEditor ? HighLogic.CurrentGame.flightState.universalTime : Planetarium.GetUniversalTime();
            if (_lastUT == 0)
                _lastUT = UT;
            double UTDiff = UT - _lastUT;
            if (UTDiff > 0)
            {
                foreach (KSCItem ksc in KCTGameStates.KSCs)
                {
                    foreach (var x in ksc.VABList)
                        x.IncrementProgress(UTDiff);
                    foreach (var x in ksc.SPHList)
                        x.IncrementProgress(UTDiff);
                    foreach (ReconRollout rr in ksc.Recon_Rollout)
                    {
                        rr.IncrementProgress(UTDiff);
                        //Reset the associated launchpad id when rollback completes
                        Profiler.BeginSample("KCT ProgressBuildTime.ReconRollout.FindBLVesselByID");
                        if (rr.RRType == ReconRollout.RolloutReconType.Rollback && rr.IsComplete()
                            && Utilities.FindBLVesselByID(new Guid(rr.AssociatedID)) is BuildListVessel blv)
                        {
                            blv.LaunchSiteID = -1;
                        }
                        Profiler.EndSample();
                    }

                    ksc.Recon_Rollout.RemoveAll(rr => !PresetManager.Instance.ActivePreset.GeneralSettings.ReconditioningTimes ||
                                                        (rr.RRType != ReconRollout.RolloutReconType.Rollout && rr.IsComplete()));

                    foreach (var x in ksc.AirlaunchPrep)
                        x.IncrementProgress(UTDiff);

                    ksc.AirlaunchPrep.RemoveAll(ap => ap.Direction != AirlaunchPrep.PrepDirection.Mount && ap.IsComplete());

                    foreach (var x in ksc.KSCTech)
                        x.IncrementProgress(UTDiff);

                    if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
                        ksc.KSCTech.RemoveAll(ub => ub.UpgradeProcessed);

                    foreach (var x in KCTGameStates.TechList)
                        x.IncrementProgress(UTDiff);
                }
            }

            _lastUT = UT;
            Profiler.EndSample();
        }


        public void LateUpdate()
        {
            if (Utilities.CurrentGameIsMission()) return;

            // If we're in the R&D and we haven't reset the tech node colors yet, do so
            if (RDController.Instance != null && !_isIconUpdated)
                UpdateTechlistIconColor();

            // Unset as soon as we leave R&D
            _isIconUpdated = RDController.Instance is RDController;
        }

        public void UpdateTechlistIconColor()
        {
            foreach (var node in RDController.Instance?.nodes.Where(x => x?.tech is RDTech) ?? Enumerable.Empty<RDNode>())
            {
                if (HasTechInList(node.tech.techID))
                {
                    node.graphics?.SetIconColor(XKCDColors.KSPNotSoGoodOrange);
                }
            }
        }

        protected bool HasTechInList(string id) => KCTGameStates.TechList.FirstOrDefault(x => x.TechID == id) != null;

        public void DelayedStart()
        {
            if (Utilities.CurrentGameIsMission()) return;

            KCTDebug.Log("DelayedStart start");
            if (PresetManager.Instance?.ActivePreset == null || !PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return;

            if (KCT_GUI.IsPrimarilyDisabled) return;

            //The following should only be executed when fully enabled for the save

            if (KCTGameStates.ActiveKSC == null)
            {
                Utilities.SetActiveKSCToRSS();
            }

            KCTDebug.Log("Checking vessels for missing parts.");
            //check that all parts are valid in all ships. If not, warn the user and disable that vessel (once that code is written)
            if (!KCTGameStates.VesselErrorAlerted)
            {
                var erroredVessels = new List<BuildListVessel>();
                foreach (KSCItem KSC in KCTGameStates.KSCs) //this is faster on subsequent scene changes
                {
                    foreach (BuildListVessel blv in KSC.VABList)
                    {
                        if (!blv.AllPartsValid)
                        {
                            KCTDebug.Log(blv.ShipName + " contains invalid parts!");
                            erroredVessels.Add(blv);
                        }
                    }
                    foreach (BuildListVessel blv in KSC.VABWarehouse)
                    {
                        if (!blv.AllPartsValid)
                        {
                            KCTDebug.Log(blv.ShipName + " contains invalid parts!");
                            erroredVessels.Add(blv);
                        }
                    }
                    foreach (BuildListVessel blv in KSC.SPHList)
                    {
                        if (!blv.AllPartsValid)
                        {
                            KCTDebug.Log(blv.ShipName + " contains invalid parts!");
                            erroredVessels.Add(blv);
                        }
                    }
                    foreach (BuildListVessel blv in KSC.SPHWarehouse)
                    {
                        if (!blv.AllPartsValid)
                        {
                            KCTDebug.Log(blv.ShipName + " contains invalid parts!");
                            erroredVessels.Add(blv);
                        }
                    }
                }
                if (erroredVessels.Count > 0)
                    PopUpVesselError(erroredVessels);
                KCTGameStates.VesselErrorAlerted = true;
            }

            if (HighLogic.LoadedSceneIsEditor && KCTGameStates.EditorShipEditingMode)
            {
                KCTDebug.Log($"Editing {KCTGameStates.EditedVessel.ShipName}");
                EditorLogic.fetch.shipNameField.text = KCTGameStates.EditedVessel.ShipName;
            }

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                KCTDebug.Log("SP Start");
                if (!KCT_GUI.IsPrimarilyDisabled)
                {
                    if (ToolbarManager.ToolbarAvailable && KCTGameStates.Settings.PreferBlizzyToolbar)
                    {
                        if (KCTGameStates.ShowWindows[0])
                            KCT_GUI.ToggleVisibility(true);
                        else
                        {
                            if (KCTEvents.Instance != null && KCTGameStates.ToolbarControl != null)
                            {
                                if (KCTGameStates.ShowWindows[0])
                                    KCT_GUI.ToggleVisibility(true);
                            }
                        }
                    }
                    KCT_GUI.ResetBLWindow();
                }
                else
                {
                    KCT_GUI.GUIStates.ShowBuildList = false;
                    KCTGameStates.ShowWindows[0] = false;
                }
                KCTDebug.Log("SP UI done");

                if (KCTGameStates.IsFirstStart)
                {
                    KCTDebug.Log("Showing first start.");
                    KCTGameStates.IsFirstStart = false;
                    KCT_GUI.GUIStates.ShowFirstRun = true;

                    //initialize the proper launchpad
                    KCTGameStates.ActiveKSC.ActiveLPInstance.level = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.LaunchPad);
                }

                KCTDebug.Log("SP switch starting");
                KCTGameStates.ActiveKSC.SwitchLaunchPad(KCTGameStates.ActiveKSC.ActiveLaunchPadID);
                KCTDebug.Log("SP switch done");

                foreach (KSCItem ksc in KCTGameStates.KSCs)
                {
                    for (int i = 0; i < ksc.Recon_Rollout.Count; i++)
                    {
                        ReconRollout rr = ksc.Recon_Rollout[i];
                        if (rr.RRType != ReconRollout.RolloutReconType.Reconditioning && Utilities.FindBLVesselByID(new Guid(rr.AssociatedID)) == null)
                        {
                            KCTDebug.Log($"Invalid Recon_Rollout at {ksc.KSCName}. ID {rr.AssociatedID} not found.");
                            ksc.Recon_Rollout.Remove(rr);
                            i--;
                        }
                    }

                    for (int i = 0; i < ksc.AirlaunchPrep.Count; i++)
                    {
                        AirlaunchPrep ap = ksc.AirlaunchPrep[i];
                        if (Utilities.FindBLVesselByID(new Guid(ap.AssociatedID)) == null)
                        {
                            KCTDebug.Log($"Invalid KCT_AirlaunchPrep at {ksc.KSCName}. ID {ap.AssociatedID} not found.");
                            ksc.AirlaunchPrep.Remove(ap);
                            i--;
                        }
                    }
                }
                KCTDebug.Log("SP done");
            }

            if (HighLogic.LoadedSceneIsFlight && KCTGameStates.IsSimulatedFlight)
            {
                Utilities.EnableSimulationLocks();
                if (KCTGameStates.SimulationParams.SimulationUT > 0 &&
                    FlightDriver.CanRevertToPrelaunch)    // Used for checking whether the player has saved and then loaded back into that save
                {
                    KCTDebug.Log($"Setting simulation UT to {KCTGameStates.SimulationParams.SimulationUT}");
                    Planetarium.SetUniversalTime(KCTGameStates.SimulationParams.SimulationUT);
                }

                AddSimulationWatermark();
            }

            if (KCTGameStates.IsSimulatedFlight && HighLogic.LoadedSceneIsGame && !HighLogic.LoadedSceneIsFlight)
            {
                string msg = "Current save appears to be a simulation with no way to automatically revert to the pre-simulation state. An older save needs to be loaded manually now.";
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "errorPopup", "KCT Simulation error", msg, "Understood", false, HighLogic.UISkin);
            }

            KCTDebug.Log("DelayedStart finished");
        }

        public static void PopUpVesselError(List<BuildListVessel> errored)
        {
            DialogGUIBase[] options = new DialogGUIBase[2];
            options[0] = new DialogGUIButton("Understood", () => { });
            options[1] = new DialogGUIButton("Delete Vessels", () =>
            {
                foreach (BuildListVessel blv in errored)
                {
                    blv.RemoveFromBuildList();
                    Utilities.AddFunds(blv.GetTotalCost(), TransactionReasons.VesselRollout);
                    //remove any associated recon_rollout
                }
            });

            string txt = "The following KCT vessels contain missing or invalid parts and have been quarantined. Either add the missing parts back into your game or delete the vessels. A file containing the ship names and missing parts has been added to your save folder.\n";
            string txtToWrite = "";
            foreach (BuildListVessel blv in errored)
            {
                txt += blv.ShipName + "\n";
                txtToWrite += blv.ShipName + "\n";
                txtToWrite += string.Join("\n", blv.GetMissingParts());
                txtToWrite += "\n\n";
            }

            //make new file for missing ships
            string filename = KSPUtil.ApplicationRootPath + "/saves/" + HighLogic.SaveFolder + "/missingParts.txt";
            File.WriteAllText(filename, txtToWrite);

            //remove all rollout and recon items since they're invalid without the ships
            foreach (BuildListVessel blv in errored)
            {
                //remove any associated recon_rollout
                foreach (KSCItem ksc in KCTGameStates.KSCs)
                {
                    for (int i = 0; i < ksc.Recon_Rollout.Count; i++)
                    {
                        ReconRollout rr = ksc.Recon_Rollout[i];
                        if (rr.AssociatedID == blv.Id.ToString())
                        {
                            ksc.Recon_Rollout.Remove(rr);
                            i--;
                        }
                    }

                    for (int i = 0; i < ksc.AirlaunchPrep.Count; i++)
                    {
                        AirlaunchPrep ap = ksc.AirlaunchPrep[i];
                        if (ap.AssociatedID == blv.Id.ToString())
                        {
                            ksc.AirlaunchPrep.Remove(ap);
                            i--;
                        }
                    }
                }
            }

            var diag = new MultiOptionDialog("missingPartsPopup", txt, "Vessels Contain Missing Parts", null, options);
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
        }

        public static void ShowLaunchAlert(string launchSite)
        {
            KCTDebug.Log("Showing Launch Alert");
            if (KCT_GUI.IsPrimarilyDisabled)
            {
                EditorLogic.fetch.launchVessel();
            }
            else
            {
                Utilities.AddVesselToBuildList(launchSite);
                Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
            }
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
