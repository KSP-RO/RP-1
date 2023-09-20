using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UniLinq;
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

        public bool IsEditorRecalcuationRequired = false;
        private bool _hasFirstRecalculated = false;

        private static bool _isGUIInitialized = false;

        private WaitForSeconds _wfsHalf = null, _wfsOne = null, _wfsTwo = null;
        private double _lastRateUpdateUT = 0;
        private double _lastYearMultUpdateUT = 0;

        internal const string KCTLaunchLock = "KCTLaunchLock";
        internal const string KCTKSCLock = "KCTKSCLock";
        private const float BUILD_TIME_INTERVAL = 0.5f;
        private const float YEAR_MULT_TIME_INTERVAL = 86400 * 7;
        public static readonly Dictionary<string, KCTCostModifier> KCTCostModifiers = new Dictionary<string, KCTCostModifier>();
        public static readonly Dictionary<string, KCTTechNodePeriod> TechNodePeriods = new Dictionary<string, KCTTechNodePeriod>();
        public static readonly RP0.DataTypes.PersistentDictionaryValueTypes<string, NodeType> NodeTypes = new RP0.DataTypes.PersistentDictionaryValueTypes<string, NodeType>();

        // These should live in the EditorAddon but we can't easily access it then.
        public BuildListVessel EditorVessel = new BuildListVessel("temp", "LaunchPad", 0d, 0d, 0d, string.Empty, 0f, 0f, EditorFacility.VAB, false);
        public Guid PreEditorSwapLCID = Guid.Empty;

        private DateTime _simMoveDeferTime = DateTime.MaxValue;
        private int _simMoveSecondsRemain = 0;

        private GameObject _simWatermark;

        public void OnDestroy()
        {
            _simWatermark?.DestroyGameObject();

            if (KCTGameStates.ToolbarControl != null)
            {
                KCTGameStates.ToolbarControl.OnDestroy();
                Destroy(KCTGameStates.ToolbarControl);
            }
            KCT_GUI.ClearTooltips();
            KCT_GUI.OnDestroy();
            
            Instance = null;
        }

        internal void OnGUI()
        {
            if (Utilities.CurrentGameIsMission()) return;

            if (!_isGUIInitialized)
            {
                KCT_GUI.InitBuildListVars();
                _isGUIInitialized = true;
            }
            KCT_GUI.SetGUIPositions();
        }

        public void Awake()
        {
            if (Utilities.CurrentGameIsMission()) return;

            KCTDebug.Log("Awake called");

            if (Instance != null)
                GameObject.Destroy(Instance);

            Instance = this;

            KCTGameStates.Settings.Load();

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
                KCT_GUI.BuildRateForDisplay = null;
                if (!KCT_GUI.IsPrimarilyDisabled)
                {
                    IsEditorRecalcuationRequired = true;
                }
                InvokeRepeating("EditorRecalculation", 0.02f, 1f);
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
                KerbalConstructionTimeData.Instance.SimulationParams.Reset();
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
            StartCoroutine(CacheFacilityLevels());
            KCTDebug.Log("Start finished");

            DelayedStart();

            StartCoroutine(HandleEditorButton_Coroutine());
        }

        private void ProcessFlightStart()
        {
            if (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.situation != Vessel.Situations.PRELAUNCH) return;

            BuildListVessel blv = KerbalConstructionTimeData.Instance.LaunchedVessel;
            var dataModule = (KCTVesselTracker)FlightGlobals.ActiveVessel.vesselModules.Find(vm => vm is KCTVesselTracker);
            if (dataModule != null)
            {
                if (string.IsNullOrWhiteSpace(dataModule.Data.LaunchID))
                {
                    dataModule.Data.LaunchID = Guid.NewGuid().ToString("N");
                    KCTDebug.Log($"Assigned LaunchID: {dataModule.Data.LaunchID}");
                }

                // This will only fire the first time, because we make it invalid afterwards by clearing the BLV
                if (blv.IsValid)
                {
                    dataModule.Data.FacilityBuiltIn = blv.FacilityBuiltIn;
                    dataModule.Data.VesselID = blv.KCTPersistentID;
                    dataModule.Data.LCID = blv.LCID;
                    if (dataModule.Data.LCID != Guid.Empty)
                        dataModule.Data.LCModID = blv.LC.ModID;
                }
            }

            if (KCT_GUI.IsPrimarilyDisabled) return;

            AssignCrewToCurrentVessel();

            // This only fires the first time because we clear the BLV afterwards.
            if (blv.IsValid)
            {
                LCItem vesselLC = blv.LC;
                KCTDebug.Log("Attempting to remove launched vessel from build list");
                if (blv.RemoveFromBuildList(out _)) //Only do these when the vessel is first removed from the list
                {
                    //Add the cost of the ship to the funds so it can be removed again by KSP
                    FlightGlobals.ActiveVessel.vesselName = blv.shipName;
                }
                if (vesselLC == null) vesselLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
                if (vesselLC.Recon_Rollout.FirstOrDefault(r => r.associatedID == blv.shipID.ToString()) is ReconRollout rollout)
                    vesselLC.Recon_Rollout.Remove(rollout);

                if (vesselLC.Airlaunch_Prep.FirstOrDefault(r => r.associatedID == blv.shipID.ToString()) is AirlaunchPrep alPrep)
                    vesselLC.Airlaunch_Prep.Remove(alPrep);

                KerbalConstructionTimeData.Instance.LaunchedVessel = new BuildListVessel();
            }

            var alParams = KerbalConstructionTimeData.Instance.AirlaunchParams;
            if ((blv.IsValid && alParams.KCTVesselId == blv.shipID) ||
                alParams.KSPVesselId == FlightGlobals.ActiveVessel.id)
            {
                if (alParams.KSPVesselId == Guid.Empty)
                    alParams.KSPVesselId = FlightGlobals.ActiveVessel.id;
                StartCoroutine(AirlaunchRoutine(alParams, FlightGlobals.ActiveVessel.id));

                // Clear the KCT vessel ID but keep KSP's own ID.
                // 'Revert To Launch' state is saved some frames after the scene got loaded so KerbalConstructionTimeData.Instance.LaunchedVessel is no longer there.
                // In this case we use KSP's own id to figure out if airlaunch should be done.
                KerbalConstructionTimeData.Instance.AirlaunchParams.KCTVesselId = Guid.Empty;
            }
        }

        private static void AssignCrewToCurrentVessel()
        {
            if (!KerbalConstructionTimeData.Instance.IsSimulatedFlight &&
                FlightGlobals.ActiveVessel.GetCrewCount() == 0 && KerbalConstructionTimeData.Instance.LaunchedCrew.Count > 0)
            {
                KerbalRoster roster = HighLogic.CurrentGame.CrewRoster;
                foreach (Part p in FlightGlobals.ActiveVessel.parts)
                {
                    KCTDebug.Log($"Part being tested: {p.partInfo.title}");
                    if (p.CrewCapacity == 0 || !(KerbalConstructionTimeData.Instance.LaunchedCrew.Find(part => part.PartID == p.craftID) is PartCrewAssignment cp))
                        continue;
                    List<CrewMemberAssignment> crewList = cp.CrewList;
                    KCTDebug.Log($"cP.crewList.Count: {cp.CrewList.Count}");
                    foreach (CrewMemberAssignment assign in crewList)
                    {
                        ProtoCrewMember crewMember = assign.PCM;
                        if (crewMember == null)
                            continue;

                        try
                        {
                            if (p.AddCrewmember(crewMember))
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
                KerbalConstructionTimeData.Instance.LaunchedCrew.Clear();
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
                if (EditorDriver.fetch != null && !EditorDriver.fetch.restartingEditor)
                {
                    _hasFirstRecalculated = true;
                    IsEditorRecalcuationRequired = false;
                    Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                }
                // make sure we're not destructing
                else if (!_hasFirstRecalculated && this != null)
                {
                    StartCoroutine(CallbackUtil.DelayedCallback(0.02f, EditorRecalculation));
                }
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
                if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
                    Utilities.HandleEditorButton();
                yield return _wfsHalf;
            }
        }

        public void FixedUpdate()
        {
            if (Utilities.CurrentGameIsMission()) return;
            if (!PresetManager.Instance?.ActivePreset?.GeneralSettings.Enabled == true)
                return;
            double UT = Planetarium.GetUniversalTime();
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

            if (HighLogic.LoadedSceneIsFlight && KerbalConstructionTimeData.Instance.IsSimulatedFlight)
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

        private IEnumerator CacheFacilityLevels()
        {
            do
            {
                yield return new WaitForFixedUpdate();    // No way to know when KSP has finally initialized the ScenarioUpgradeableFacilities data
            } while (HighLogic.LoadedScene == GameScenes.SPACECENTER && ScenarioUpgradeableFacilities.GetFacilityLevelCount(SpaceCenterFacility.VehicleAssemblyBuilding) < 0);

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

            SimulationParams simParams = KerbalConstructionTimeData.Instance.SimulationParams;
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

        public void ProgressBuildTime(double UTDiff)
        {
            Profiler.BeginSample("RP0ProgressBuildTime");

            if (UTDiff > 0)
            {
                int passes = 1;
                double remainingUT = UTDiff;
                if (remainingUT > 86400d)
                {
                    passes = (int)(UTDiff / 86400d);
                    remainingUT = UTDiff - passes * 86400d;
                    ++passes;
                }
                int rushingEngs = 0;

                int totalEngineers = 0;
                foreach (KSCItem ksc in KCTGameStates.KSCs)
                {
                    totalEngineers += ksc.Engineers;

                    for (int j = ksc.LaunchComplexes.Count - 1; j >= 0; j--)
                    {
                        LCItem currentLC = ksc.LaunchComplexes[j];
                        if (!currentLC.IsOperational || currentLC.Engineers == 0 || !currentLC.IsActive)
                            continue;

                        double portionEngineers = currentLC.Engineers / (double)currentLC.MaxEngineers;

                        if (currentLC.IsRushing)
                            rushingEngs += currentLC.Engineers;
                        else
                        {
                            for (int p = 0; p < passes; ++p)
                            {
                                double timestep = p == 0 ? remainingUT : 86400d;
                                currentLC.EfficiencySource?.IncreaseEfficiency(timestep, portionEngineers);
                            }
                        }

                        double timeForBuild = UTDiff;
                        while(timeForBuild > 0d && currentLC.BuildList.Count > 0)
                        {
                            timeForBuild = currentLC.BuildList[0].IncrementProgress(UTDiff);
                        }

                        for (int i = currentLC.Recon_Rollout.Count; i-- > 0;)
                        {
                            // These work in parallel so no need to track excess time
                            var rr = currentLC.Recon_Rollout[i];
                            rr.IncrementProgress(UTDiff);
                            //Reset the associated launchpad id when rollback completes
                            Profiler.BeginSample("RP0ProgressBuildTime.ReconRollout.FindBLVesselByID");
                            if (rr.RRType == ReconRollout.RolloutReconType.Rollback && rr.IsComplete()
                                && Utilities.FindBLVesselByID(rr.LC, new Guid(rr.associatedID)) is BuildListVessel blv)
                            {
                                blv.launchSiteIndex = -1;
                            }
                            Profiler.EndSample();
                        }

                        currentLC.Recon_Rollout.RemoveAll(rr => rr.RRType != ReconRollout.RolloutReconType.Rollout && rr.IsComplete());
                        
                        // These also are in parallel
                        for (int i = currentLC.Airlaunch_Prep.Count; i-- > 0;)
                            currentLC.Airlaunch_Prep[i].IncrementProgress(UTDiff);

                        currentLC.Airlaunch_Prep.RemoveAll(ap => ap.direction != AirlaunchPrep.PrepDirection.Mount && ap.IsComplete());
                    }

                    for (int i = ksc.Constructions.Count; i-- > 0;)
                    {
                        ksc.Constructions[i].IncrementProgress(UTDiff);
                    }

                    // Remove all completed items
                    for (int i = ksc.LaunchComplexes.Count; i-- > 0;)
                    {
                        ksc.LaunchComplexes[i].PadConstructions.RemoveAll(ub => ub.upgradeProcessed);
                    }
                    ksc.LCConstructions.RemoveAll(ub => ub.upgradeProcessed);
                    ksc.FacilityUpgrades.RemoveAll(ub => ub.upgradeProcessed);
                }
                
                double researchTime = UTDiff;
                while (researchTime > 0d && KerbalConstructionTimeData.Instance.TechList.Count > 0)
                {
                    researchTime = KerbalConstructionTimeData.Instance.TechList[0].IncrementProgress(UTDiff);
                }

                if (KerbalConstructionTimeData.Instance.fundTarget.IsValid && KerbalConstructionTimeData.Instance.fundTarget.GetTimeLeft() < 0.5d)
                    KerbalConstructionTimeData.Instance.fundTarget.Clear();
            }
            Profiler.EndSample();
        }

        private void UpdateTechYearMults()
        {
            for (int i = KerbalConstructionTimeData.Instance.TechList.Count - 1; i >= 0; i--)
            {
                var t = KerbalConstructionTimeData.Instance.TechList[i];
                t.UpdateBuildRate(i);
            }
        }

        public void DelayedStart()
        {
            if (Utilities.CurrentGameIsMission()) return;

            KCTDebug.Log("DelayedStart start");
            if (PresetManager.Instance?.ActivePreset == null || !PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return;

            if (KCT_GUI.IsPrimarilyDisabled) return;

            //The following should only be executed when fully enabled for the save

            if (KerbalConstructionTimeData.Instance.ActiveKSC == null)
            {
                // This should not be hit, because either KSCSwitcher's LastKSC loads after KCTData
                // or KCTData loads first and the harmony patch runs.
                // But I'm leaving it here just in case.
                KerbalConstructionTimeData.Instance.SetActiveKSCToRSS();
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
                                KCTDebug.Log(blv.shipName + " contains invalid parts!");
                                erroredVessels.Add(blv);
                            }
                        }
                        foreach (BuildListVessel blv in currentLC.Warehouse)
                        {
                            if (!blv.AllPartsValid)
                            {
                                KCTDebug.Log(blv.shipName + " contains invalid parts!");
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
                KCTDebug.Log($"Editing {KerbalConstructionTimeData.Instance.EditedVessel.shipName}");
                EditorLogic.fetch.shipNameField.text = KerbalConstructionTimeData.Instance.EditedVessel.shipName;
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

                    KerbalConstructionTimeData.Instance.Applicants = PresetManager.Instance.StartingPersonnel(HighLogic.CurrentGame.Mode);
                }
                else if (KerbalConstructionTimeData.Instance.FirstRunNotComplete)
                {
                    KCT_GUI.GUIStates.ShowFirstRun = true;
                }

                KCTDebug.Log("SP done");
            }

            if (HighLogic.LoadedSceneIsFlight && KerbalConstructionTimeData.Instance.IsSimulatedFlight)
            {
                Utilities.EnableSimulationLocks();
                if (KerbalConstructionTimeData.Instance.SimulationParams.SimulationUT > 0 &&
                    FlightDriver.CanRevertToPrelaunch)    // Used for checking whether the player has saved and then loaded back into that save
                {
                    // Advance building construction
                    double UToffset = KerbalConstructionTimeData.Instance.SimulationParams.SimulationUT - Planetarium.GetUniversalTime();
                    if (UToffset > 0)
                    {
                        foreach (var ksc in KCTGameStates.KSCs)
                        {
                            for(int i = 0; i < ksc.Constructions.Count; ++i)
                            {
                                var c = ksc.Constructions[i];
                                double t = c.GetTimeLeft();
                                if (t <= UToffset)
                                    c.progress = c.BP;
                            }
                        }
                    }
                    KCTDebug.Log($"Setting simulation UT to {KerbalConstructionTimeData.Instance.SimulationParams.SimulationUT}");
                    if (!Utilities.IsPrincipiaInstalled)
                        Planetarium.SetUniversalTime(KerbalConstructionTimeData.Instance.SimulationParams.SimulationUT);
                    else
                        StartCoroutine(EaseSimulationUT_Coroutine(Planetarium.GetUniversalTime(), KerbalConstructionTimeData.Instance.SimulationParams.SimulationUT));
                }

                AddSimulationWatermark();
            }

            if (KerbalConstructionTimeData.Instance.IsSimulatedFlight && HighLogic.LoadedSceneIsGame && !HighLogic.LoadedSceneIsFlight)
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
                    Utilities.AddFunds(blv.GetTotalCost(), RP0.TransactionReasonsRP0.VesselPurchase);
                    //remove any associated recon_rollout
                }
            });

            string txt = "The following stored/building vessels contain missing or invalid parts and have been quarantined. Either add the missing parts back into your game or delete the vessels. A file containing the ship names and missing parts has been added to your save folder.\n";
            string txtToWrite = "";
            foreach (BuildListVessel blv in errored)
            {
                txt += blv.shipName + "\n";
                txtToWrite += blv.shipName + "\n";
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
                            if (rr.associatedID == blv.shipID.ToString())
                            {
                                currentLC.Recon_Rollout.Remove(rr);
                                i--;
                            }
                        }

                        for (int i = 0; i < currentLC.Airlaunch_Prep.Count; i++)
                        {
                            AirlaunchPrep ap = currentLC.Airlaunch_Prep[i];
                            if (ap.associatedID == blv.shipID.ToString())
                            {
                                currentLC.Airlaunch_Prep.Remove(ap);
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
                // We are recalculating because vessel validation might have changed state.
                Instance.IsEditorRecalcuationRequired = true;
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
