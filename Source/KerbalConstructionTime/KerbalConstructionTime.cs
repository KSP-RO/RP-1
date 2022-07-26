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
using System.Reflection;

namespace KerbalConstructionTime
{
    public class KerbalConstructionTime : MonoBehaviour
    {
        public static KerbalConstructionTime Instance { get; private set; }

        public EngineersReportClobberer ERClobberer { get; private set; }

        public bool IsEditorRecalcuationRequired;

        private static bool _isGUIInitialized = false;

        private WaitForSeconds _wfsHalf = null, _wfsOne = null, _wfsTwo = null;
        private bool _isIconUpdated = false;
        private double _lastRateUpdateUT = 0;
        private double _lastYearMultUpdateUT = 0;

        internal const string KCTLaunchLock = "KCTLaunchLock";
        internal const string KCTKSCLock = "KCTKSCLock";
        private const float BUILD_TIME_INTERVAL = 0.5f;
        private const float YEAR_MULT_TIME_INTERVAL = 86400 * 7;
        public static readonly Dictionary<string, KCTCostModifier> KCTCostModifiers = new Dictionary<string, KCTCostModifier>();
        public static readonly Dictionary<string, KCTTechNodePeriod> TechNodePeriods = new Dictionary<string, KCTTechNodePeriod>();

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
            KCT_GUI.ClearTooltips();
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
            ERClobberer = new EngineersReportClobberer(this);

            KCTGameStates.Settings.Load();

            if (!File.Exists(PresetManager.SettingsFilePath))
            {
                KCTGameStates.IsFirstStart = true;
                // In this case it is a new game, so we start on the current version.
                // Should not be meaningful because we only check LoadedSaveVersion during Load
                KCTGameStates.LoadedSaveVersion = KCTGameStates.VERSION;
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

            KCT_GUI.InitTooltips();

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
                KCTGameStates.SimulationParams.Reset();
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
                    break;
                case GameScenes.TRACKSTATION:
                    KCTGameStates.ClearVesselEditMode();
                    break;
                case GameScenes.FLIGHT:
                    KCT_GUI.HideAll();
                    ProcessFlightStart();
                    break;
            }
            // Need to do this in every scene.
            StartCoroutine(UpdateBuildRates());
            KCTDebug.Log("Start finished");

            DelayedStart();

            UpdateTechlistIconColor();
            StartCoroutine(HandleEditorButton_Coroutine());
        }

        private void ProcessFlightStart()
        {
            if (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.situation != Vessel.Situations.PRELAUNCH) return;

            var dataModule = (KCTVesselTracker)FlightGlobals.ActiveVessel.vesselModules.Find(vm => vm is KCTVesselTracker);
            if (dataModule != null)
            {
                if (string.IsNullOrWhiteSpace(dataModule.Data.LaunchID))
                {
                    dataModule.Data.LaunchID = Guid.NewGuid().ToString("N");
                    KCTDebug.Log($"Assigned LaunchID: {dataModule.Data.LaunchID}");
                }

                if (KCTGameStates.LaunchedVessel != null)
                {
                    dataModule.Data.FacilityBuiltIn = KCTGameStates.LaunchedVessel.FacilityBuiltIn;
                    dataModule.Data.VesselID = KCTGameStates.LaunchedVessel.KCTPersistentID;
                    dataModule.Data.LCID = KCTGameStates.LaunchedVessel.LCID.ToString("N");
                    if (KCTGameStates.LaunchedVessel.LC == null)
                    {
                        LCItem lc = KCTGameStates.FindLCFromID(KCTGameStates.LaunchedVessel.LCID);
                        if (lc != null)
                            dataModule.Data.LCModID = lc.ModID.ToString("N");
                    }
                    else
                    {
                        dataModule.Data.LCModID = KCTGameStates.LaunchedVessel.LC.ModID.ToString("N");
                    }
                }
            }

            if (KCT_GUI.IsPrimarilyDisabled) return;

            AssignCrewToCurrentVessel();

            if (KCTGameStates.LaunchedVessel != null && !KCTGameStates.IsSimulatedFlight)
            {
                LCItem vesselLC = KCTGameStates.LaunchedVessel.LC;
                KCTGameStates.LaunchedVessel.LCID = KCTGameStates.LaunchedVessel.LC.ID; // clear LC and force refind later.
                KCTDebug.Log("Attempting to remove launched vessel from build list");
                if (KCTGameStates.LaunchedVessel.RemoveFromBuildList(out _)) //Only do these when the vessel is first removed from the list
                {
                    //Add the cost of the ship to the funds so it can be removed again by KSP
                    Utilities.AddFunds(KCTGameStates.LaunchedVessel.Cost, TransactionReasons.VesselRollout);
                    FlightGlobals.ActiveVessel.vesselName = KCTGameStates.LaunchedVessel.ShipName;
                }
                if (vesselLC == null) vesselLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
                if (vesselLC.Recon_Rollout.FirstOrDefault(r => r.AssociatedID == KCTGameStates.LaunchedVessel.Id.ToString()) is ReconRollout rollout)
                    vesselLC.Recon_Rollout.Remove(rollout);

                if (vesselLC.AirlaunchPrep.FirstOrDefault(r => r.AssociatedID == KCTGameStates.LaunchedVessel.Id.ToString()) is AirlaunchPrep alPrep)
                    vesselLC.AirlaunchPrep.Remove(alPrep);

                if (KCTGameStates.AirlaunchParams is AirlaunchParams alParams && alParams.KCTVesselId == KCTGameStates.LaunchedVessel.Id &&
                    (!alParams.KSPVesselId.HasValue || alParams.KSPVesselId == FlightGlobals.ActiveVessel.id))
                {
                    if (!alParams.KSPVesselId.HasValue) alParams.KSPVesselId = FlightGlobals.ActiveVessel.id;
                    StartCoroutine(AirlaunchRoutine(alParams, FlightGlobals.ActiveVessel.id));
                }
            }
        }

        private static void AssignCrewToCurrentVessel()
        {
            if (!KCTGameStates.IsSimulatedFlight &&
                FlightGlobals.ActiveVessel.GetCrewCount() == 0 && KCTGameStates.LaunchedCrew.Count > 0)
            {
                KerbalRoster roster = HighLogic.CurrentGame.CrewRoster;
                foreach (Part p in FlightGlobals.ActiveVessel.parts)
                {
                    KCTDebug.Log($"Part being tested: {p.partInfo.title}");
                    if (p.CrewCapacity == 0 || !(KCTGameStates.LaunchedCrew.Find(part => part.PartID == p.craftID) is PartCrewAssignment cp))
                        continue;
                    List<CrewMemberAssignment> crewList = cp.CrewList;
                    KCTDebug.Log($"cP.crewList.Count: {cp.CrewList.Count}");
                    foreach (CrewMemberAssignment assign in crewList)
                    {
                        ProtoCrewMember crewMember = assign?.PCM;
                        if (crewMember == null) continue;

                        // We can't be sure that CrewRoster isn't reloaded from ConfigNode when starting flight.
                        // Thus need to re-fetch every ProtoCrewMember instance from CrewRoster before spawning them inside the vessel.
                        if (crewMember.type == ProtoCrewMember.KerbalType.Crew)
                        {
                            crewMember = roster.Crew.FirstOrDefault(c => c.name == crewMember.name);
                        }
                        else if (crewMember.type == ProtoCrewMember.KerbalType.Tourist)
                        {
                            crewMember = roster.Tourist.FirstOrDefault(c => c.name == crewMember.name);
                        }

                        try
                        {
                            if (crewMember is ProtoCrewMember && p.AddCrewmember(crewMember))
                            {
                                KCTDebug.Log($"Assigned {crewMember.name} to {p.partInfo.name}");
                                crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
                                crewMember.seat?.SpawnCrew();
                            }
                            else
                            {
                                KCTDebug.LogError($"Error when assigning {crewMember.name} to {p.partInfo.name}");
                                crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                            }
                        }
                        catch (Exception ex)
                        {
                            KCTDebug.LogError($"Error when assigning {crewMember.name} to {p.partInfo.name}: {ex}");
                            crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                        }
                    }
                }
                KCTGameStates.LaunchedCrew.Clear();
            }
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
            // Need to fire this so trip logger etc notice we're flying now.
            Debug.Log($"[RP-0] Finished clobbering vessel situation of {FlightGlobals.ActiveVessel.name} to PRELAUNCH (for Prinicipia stability), now firing change event to FLYING.");
            FlightGlobals.ActiveVessel.situation = Vessel.Situations.FLYING;
            GameEvents.onVesselSituationChange.Fire(new GameEvents.HostedFromToAction<Vessel, Vessel.Situations>(FlightGlobals.ActiveVessel, Vessel.Situations.PRELAUNCH, Vessel.Situations.FLYING));
        }

        protected void EditorRecalculation()
        {
            if (IsEditorRecalcuationRequired)
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

            ERClobberer.PollForChanges();

            if (!KCT_GUI.IsPrimarilyDisabled && HighLogic.LoadedScene == GameScenes.SPACECENTER &&
                VesselSpawnDialog.Instance?.Visible == true)
            {
                VesselSpawnDialog.Instance.ButtonClose();
                KCTDebug.Log("Attempting to close spawn dialog!");
            }

            UpdateRndScreen();
        }

        public void FixedUpdate()
        {
            if (Utilities.CurrentGameIsMission()) return;
            if (!PresetManager.Instance?.ActivePreset?.GeneralSettings.Enabled == true)
                return;
            double UT = Utilities.GetUT();
            if (_lastRateUpdateUT == 0d)
                _lastRateUpdateUT = UT;
            double UTDiff = UT - _lastRateUpdateUT;
            if (!KCT_GUI.IsPrimarilyDisabled && (TimeWarp.CurrentRateIndex > 0 || UTDiff > BUILD_TIME_INTERVAL))
            {
                // Drive this from RP-1: ProgressBuildTime(UTDiff);
                _lastRateUpdateUT = UT;

                if (UT - _lastYearMultUpdateUT > YEAR_MULT_TIME_INTERVAL)
                {
                    UpdateTechYearMults();
                    _lastYearMultUpdateUT = UT;
                }
            }

            if (HighLogic.LoadedSceneIsFlight && KCTGameStates.IsSimulatedFlight && KCTGameStates.SimulationParams != null)
            {
                ProcessSimulation();
            }
        }

        // Ran every 30 FixedUpdates, which we will treat as 0.5 seconds for now.
        private IEnumerator UpdateActiveLPLevel()
        {
            // Only run during Space Center in career mode
            // Also need to wait a bunch of frames until KSP has initialized Upgradable and Destructible facilities
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            while (HighLogic.LoadedScene == GameScenes.SPACECENTER && Utilities.CurrentGameIsCareer())
            {
                if (KCTGameStates.ActiveKSC?.ActiveLaunchComplexInstance?.ActiveLPInstance is KCT_LaunchPad pad)
                {
                    if (Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.LaunchPad) != pad.level)
                    {
                        KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.SwitchLaunchPad(KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.ActiveLaunchPadIndex, false);
                        pad.UpdateLaunchpadDestructionState(false);
                    }
                }
                yield return _wfsHalf;
            }
        }

        private IEnumerator UpdateBuildRates()
        {
            do
            {
                yield return new WaitForFixedUpdate();    // No way to know when KSP has finally initialized the ScenarioUpgradeableFacilities data
            } while (HighLogic.LoadedScene == GameScenes.SPACECENTER && ScenarioUpgradeableFacilities.GetFacilityLevelCount(SpaceCenterFacility.VehicleAssemblyBuilding) < 0);

            // Need to always update build rates, regardless of scene
            KCTDebug.Log("Updating build rates");
            foreach (KSCItem KSC in KCTGameStates.KSCs)
            {
                KSC?.RecalculateBuildRates();
            }
            KCTDebug.Log("Rates updated");

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER
                && ScenarioUpgradeableFacilities.GetFacilityLevelCount(SpaceCenterFacility.VehicleAssemblyBuilding) >= 0)
            {
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

        public void UpdateRndScreen()
        {
            if (Utilities.CurrentGameIsMission()) return;

            // If we're in the R&D and we haven't reset the tech node colors yet, do so.
            // Note: the nodes list is initialized at a later frame than RDController.Instance becomes available.
            if (!_isIconUpdated && RDController.Instance?.nodes?.Count > 0)
            {
                StartCoroutine(UpdateTechlistIconColorDelayed());
                _isIconUpdated = true;
            }

            if (RDController.Instance == null)
            {
                _isIconUpdated = false;
            }
        }

        public void ForceUpdateRndScreen()
        {
            _isIconUpdated = false;
            UpdateRndScreen();
        }

        public void ProgressBuildTime(double UTDiff)
        {
            Profiler.BeginSample("KCT ProgressBuildTime");

            if (UTDiff > 0)
            {
                bool skillupEng = false;
                int passes = 1;
                double remainingUT = UTDiff;
                if (remainingUT > 86400d)
                {
                    passes = (int)(UTDiff / 86400d);
                    remainingUT = UTDiff - passes * 86400d;
                    ++passes;
                }
                double remainingRushEfficMult = Math.Pow(PresetManager.Instance.ActivePreset.GeneralSettings.RushEfficMult, remainingUT / 86400d);
                int rushingEngs = 0;

                int totalEngineers = 0;
                foreach (KSCItem ksc in KCTGameStates.KSCs)
                {
                    totalEngineers += ksc.Engineers;

                    for (int j = ksc.LaunchComplexes.Count - 1; j >= 0; j--)
                    {
                        LCItem currentLC = ksc.LaunchComplexes[j];
                        bool increment = currentLC.IsOperational && currentLC.Engineers > 0 && currentLC.IsActive;
                        for (int p = 0; p < passes; ++p)
                        {
                            double timestep, rushEfficMult;
                            if (p == 0)
                            {
                                timestep = remainingUT;
                                rushEfficMult = remainingRushEfficMult;
                            }
                            else
                            {
                                timestep = 86400d;
                                rushEfficMult = PresetManager.Instance.ActivePreset.GeneralSettings.RushEfficMult;
                            }
                            if (increment)
                            {
                                skillupEng = true;

                                if (currentLC.IsRushing)
                                {
                                    double tmp = currentLC.EfficiencyEngineers * rushEfficMult;
                                    if (currentLC.EfficiencyEngineers > PresetManager.Instance.ActivePreset.GeneralSettings.RushEfficMin)
                                        currentLC.EfficiencyEngineers = Math.Max(PresetManager.Instance.ActivePreset.GeneralSettings.RushEfficMin, tmp);
                                }
                                else
                                {
                                    double max = PresetManager.Instance.ActivePreset.GeneralSettings.EngineerMaxEfficiency;
                                    double eval = PresetManager.Instance.ActivePreset.GeneralSettings.EngineerSkillupRate.Evaluate((float)currentLC.EfficiencyEngineers);
                                    double delta = eval * timestep / (365.25d * 86400d);
                                    //KCTDebug.Log($"For LC {currentLC.Name}, effic {currentLC.EfficiencyPersonnel}. Max {max}. Curve eval {eval}. So delta {delta}");
                                    currentLC.EfficiencyEngineers = Math.Min(max, currentLC.EfficiencyEngineers + delta);
                                }
                            }
                            if (currentLC.LastEngineers < currentLC.Engineers)
                                currentLC.LastEngineers = currentLC.Engineers;
                            else if (currentLC.LastEngineers > currentLC.Engineers)
                                currentLC.LastEngineers = Math.Max(currentLC.Engineers, currentLC.LastEngineers * (1d - PresetManager.Instance.ActivePreset.GeneralSettings.EngineerDecayRate * timestep / 86400d));
                        }

                        if (increment && currentLC.IsRushing)
                            rushingEngs += currentLC.Engineers;

                        if (!currentLC.IsOperational)
                            continue;

                        double timeForBuild = UTDiff;
                        while(timeForBuild > 0d)
                        {
                            double excess = 0d;
                            for (int i = currentLC.BuildList.Count - 1; i >= 0; i--)
                                excess += currentLC.BuildList[i].IncrementProgress(UTDiff);

                            timeForBuild = excess;
                        }

                        for (int i = currentLC.Recon_Rollout.Count - 1; i >= 0; i--)
                        {
                            // These work in parallel so no need to track excess time
                            var rr = currentLC.Recon_Rollout[i];
                            rr.IncrementProgress(UTDiff);
                            //Reset the associated launchpad id when rollback completes
                            Profiler.BeginSample("KCT ProgressBuildTime.ReconRollout.FindBLVesselByID");
                            if (rr.RRType == ReconRollout.RolloutReconType.Rollback && rr.IsComplete()
                                && Utilities.FindBLVesselByID(rr.LC, new Guid(rr.AssociatedID)) is BuildListVessel blv)
                            {
                                blv.LaunchSiteIndex = -1;
                            }
                            Profiler.EndSample();
                        }

                        currentLC.Recon_Rollout.RemoveAll(rr => rr.RRType != ReconRollout.RolloutReconType.Rollout && rr.IsComplete());
                        
                        // These also are in parallel
                        for (int i = currentLC.AirlaunchPrep.Count - 1; i >= 0; i--)
                            currentLC.AirlaunchPrep[i].IncrementProgress(UTDiff);

                        currentLC.AirlaunchPrep.RemoveAll(ap => ap.Direction != AirlaunchPrep.PrepDirection.Mount && ap.IsComplete());
                    }

                    double constructionTimeForBuild = UTDiff;
                    while (constructionTimeForBuild > 0d)
                    {
                        double excess = 0d;
                        for (int i = ksc.Constructions.Count - 1; i >= 0; --i)
                        {
                            excess += ksc.Constructions[i].IncrementProgress(constructionTimeForBuild);
                        }
                        constructionTimeForBuild = excess;

                        // Remove all completed items
                        for (int i = ksc.LaunchComplexes.Count - 1; i >= 0; --i)
                        {
                            ksc.LaunchComplexes[i].PadConstructions.RemoveAll(ub => ub.UpgradeProcessed);
                        }
                        ksc.LCConstructions.RemoveAll(ub => ub.UpgradeProcessed);
                        ksc.FacilityUpgrades.RemoveAll(ub => ub.UpgradeProcessed);
                    }
                }
                
                int techCount = KCTGameStates.TechList.Count;

                for (int p = 0; p < passes; ++p)
                {
                    double timestep, rushEfficMult;
                    if (p == 0)
                    {
                        timestep = remainingUT;
                        rushEfficMult = remainingRushEfficMult;
                    }
                    else
                    {
                        timestep = 86400d;
                        rushEfficMult = PresetManager.Instance.ActivePreset.GeneralSettings.RushEfficMult;
                    }

                    if (skillupEng)
                    {
                        double max = PresetManager.Instance.ActivePreset.GeneralSettings.GlobalEngineerMaxEfficiency;
                        double eval = PresetManager.Instance.ActivePreset.GeneralSettings.GlobalEngineerSkillupRate.Evaluate((float)KCTGameStates.EfficiencyEngineers);
                        double delta = eval * UTDiff / (365.25d * 86400d);
                        if (rushingEngs > 0)
                            delta = UtilMath.LerpUnclamped(delta, 0, rushingEngs / KCTGameStates.TotalEngineers);
                        //KCTDebug.Log($"Global eng effic {KCTGameStates.EfficiencyEngineers}. Max {max}. Curve eval {eval}. So delta {delta}");
                        KCTGameStates.EfficiencyEngineers = Math.Min(max, KCTGameStates.EfficiencyEngineers + delta);
                    }

                    if (KCTGameStates.LastEngineers < totalEngineers)
                        KCTGameStates.LastEngineers = totalEngineers;
                    else if (KCTGameStates.LastEngineers > totalEngineers)
                        KCTGameStates.LastEngineers = Math.Max(totalEngineers, KCTGameStates.LastEngineers * (1d - PresetManager.Instance.ActivePreset.GeneralSettings.GlobalEngineerDecayRate * timestep / 86400d));

                    if (techCount > 0 && KCTGameStates.Researchers > 0)
                    {
                        double max = PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherMaxEfficiency;
                        double eval = PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherSkillupRate.Evaluate((float)KCTGameStates.EfficiencyResearchers);
                        double delta = eval * UTDiff / (365.25d * 86400d);
                        //KCTDebug.Log($"For Researchers, effic {KCTGameStates.EfficiencyRDPersonnel}. Max {max}. Curve eval {eval}. So delta {delta}");
                        KCTGameStates.EfficiencyResearchers = Math.Min(max, KCTGameStates.EfficiencyResearchers + delta);
                    }
                    if (KCTGameStates.LastResearchers < KCTGameStates.Researchers)
                        KCTGameStates.LastResearchers = KCTGameStates.Researchers;
                    else if (KCTGameStates.LastResearchers > KCTGameStates.Researchers)
                        KCTGameStates.LastResearchers = Math.Max(KCTGameStates.Researchers, KCTGameStates.LastResearchers * (1d - PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherDecayRate * timestep / 86400d));
                }
                double researchTime = UTDiff;
                while (researchTime > 0d)
                {
                    double excess = 0d;
                    for (int i = techCount - 1; i >= 0; i--)
                        excess += KCTGameStates.TechList[i].IncrementProgress(UTDiff);
                    researchTime = excess;
                }
            }
            Profiler.EndSample();
        }

        private void UpdateTechYearMults()
        {
            for (int i = KCTGameStates.TechList.Count - 1; i >= 0; i--)
            {
                var t = KCTGameStates.TechList[i];
                t.UpdateBuildRate(i);
            }
        }

        private IEnumerator UpdateTechlistIconColorDelayed()
        {
            yield return new WaitForEndOfFrame();
            UpdateTechlistIconColor();
        }

        private void UpdateTechlistIconColor()
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
                    foreach (LCItem currentLC in KSC.LaunchComplexes)
                    {
                        foreach (BuildListVessel blv in currentLC.BuildList)
                        {
                            if (!blv.AllPartsValid)
                            {
                                KCTDebug.Log(blv.ShipName + " contains invalid parts!");
                                erroredVessels.Add(blv);
                            }
                        }
                        foreach (BuildListVessel blv in currentLC.Warehouse)
                        {
                            if (!blv.AllPartsValid)
                            {
                                KCTDebug.Log(blv.ShipName + " contains invalid parts!");
                                erroredVessels.Add(blv);
                            }
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
                    foreach (var ksc in KCTGameStates.KSCs)
                        ksc.EnsureStartingLaunchComplexes();

                    KCTGameStates.UnassignedPersonnel = PresetManager.Instance.StartingPersonnel(HighLogic.CurrentGame.Mode);
                }
                else if (KCTGameStates.FirstRunNotComplete)
                {
                    KCT_GUI.GUIStates.ShowFirstRun = true;
                }

                foreach (KSCItem ksc in KCTGameStates.KSCs)
                {
                    foreach (LCItem currentLC in ksc.LaunchComplexes)
                    {
                        for (int i = 0; i < currentLC.Recon_Rollout.Count; i++)
                        {
                            ReconRollout rr = currentLC.Recon_Rollout[i];
                            if (rr.RRType != ReconRollout.RolloutReconType.Reconditioning && Utilities.FindBLVesselByID(rr.LC, new Guid(rr.AssociatedID)) == null)
                            {
                                KCTDebug.Log($"Invalid Recon_Rollout at {ksc.KSCName}. ID {rr.AssociatedID} not found.");
                                currentLC.Recon_Rollout.Remove(rr);
                                i--;
                            }
                        }

                        for (int i = 0; i < currentLC.AirlaunchPrep.Count; i++)
                        {
                            AirlaunchPrep ap = currentLC.AirlaunchPrep[i];
                            if (Utilities.FindBLVesselByID(ap.LC, new Guid(ap.AssociatedID)) == null)
                            {
                                KCTDebug.Log($"Invalid KCT_AirlaunchPrep at {ksc.KSCName}. ID {ap.AssociatedID} not found.");
                                currentLC.AirlaunchPrep.Remove(ap);
                                i--;
                            }
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
                    // Advance building construction
                    double UToffset = KCTGameStates.SimulationParams.SimulationUT - Utilities.GetUT();
                    if (UToffset > 0)
                    {
                        foreach (var ksc in KCTGameStates.KSCs)
                        {
                            for(int i = 0; i < ksc.Constructions.Count; ++i)
                            {
                                var c = ksc.Constructions[i];
                                double t = c.GetTimeLeft();
                                if (t <= UToffset)
                                    c.Progress = c.BP;
                            }
                        }
                    }
                    KCTDebug.Log($"Setting simulation UT to {KCTGameStates.SimulationParams.SimulationUT}");
                    if (!Utilities.IsPrincipiaInstalled)
                        Planetarium.SetUniversalTime(KCTGameStates.SimulationParams.SimulationUT);
                    else
                        StartCoroutine(EaseSimulationUT_Coroutine(Planetarium.GetUniversalTime(), KCTGameStates.SimulationParams.SimulationUT));
                }

                AddSimulationWatermark();
            }

            if (KCTGameStates.IsSimulatedFlight && HighLogic.LoadedSceneIsGame && !HighLogic.LoadedSceneIsFlight)
            {
                string msg = $"The current save appears to be a simulation and we cannot automatically find a suitable pre-simulation save. Please load an older save manually; we recommend the backup that should have been saved to \\saves\\{HighLogic.SaveFolder}\\Backup\\KCT_simulation_backup.sfs";
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "errorPopup", "Simulation Error", msg, "Understood", false, HighLogic.UISkin);
            }

            KCTDebug.Log("DelayedStart finished");
        }

        private IEnumerator EaseSimulationUT_Coroutine(double startUT, double targetUT)
        {
            const double dayInSeconds = 86_400;

            if (targetUT <= Planetarium.GetUniversalTime()) yield break;

            KCTDebug.Log($"Easing jump to simulation UT in {dayInSeconds}s steps");

            int currentFrame = Time.frameCount;
            double nextUT = startUT;
            while (targetUT - nextUT > dayInSeconds)
            {
                nextUT += dayInSeconds;

                FlightDriver.fetch.framesBeforeInitialSave += Time.frameCount - currentFrame;
                currentFrame = Time.frameCount;
                OrbitPhysicsManager.HoldVesselUnpack();
                Planetarium.SetUniversalTime(nextUT);

                yield return new WaitForFixedUpdate();
            }

            OrbitPhysicsManager.HoldVesselUnpack();
            Planetarium.SetUniversalTime(targetUT);
        }

        public static void PopUpVesselError(List<BuildListVessel> errored)
        {
            DialogGUIBase[] options = new DialogGUIBase[2];
            options[0] = new DialogGUIButton("Understood", () => { });
            options[1] = new DialogGUIButton("Delete Vessels", () =>
            {
                foreach (BuildListVessel blv in errored)
                {
                    blv.RemoveFromBuildList(out _);
                    Utilities.AddFunds(blv.GetTotalCost(), TransactionReasons.VesselRollout);
                    //remove any associated recon_rollout
                }
            });

            string txt = "The following stored/building vessels contain missing or invalid parts and have been quarantined. Either add the missing parts back into your game or delete the vessels. A file containing the ship names and missing parts has been added to your save folder.\n";
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
                    foreach (LCItem currentLC in ksc.LaunchComplexes)
                    {
                        for (int i = 0; i < currentLC.Recon_Rollout.Count; i++)
                        {
                            ReconRollout rr = currentLC.Recon_Rollout[i];
                            if (rr.AssociatedID == blv.Id.ToString())
                            {
                                currentLC.Recon_Rollout.Remove(rr);
                                i--;
                            }
                        }

                        for (int i = 0; i < currentLC.AirlaunchPrep.Count; i++)
                        {
                            AirlaunchPrep ap = currentLC.AirlaunchPrep[i];
                            if (ap.AssociatedID == blv.Id.ToString())
                            {
                                currentLC.AirlaunchPrep.Remove(ap);
                                i--;
                            }
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
                Utilities.TryAddVesselToBuildList(launchSite);
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
