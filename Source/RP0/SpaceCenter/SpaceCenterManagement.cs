using System;
using System.Reflection;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using ROUtils.DataTypes;
using ToolbarControl_NS;
using KSP.UI.Screens;
using System.Collections;
using System.IO;
using UnityEngine.Profiling;
using UnityEngine.UI;
using RP0.UI;
using KSP.Localization;
using ROUtils;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class SpaceCenterManagement : ScenarioModule
    {
        #region Statics

        // global flags
        public static bool IsRefundingScience = false;
        public static bool TechListIgnoreUpdates = false;

        internal const string _modId = "KCT_NS";
        internal const string _modName = "Kerbal Construction Time";
        public static ToolbarControl ToolbarControl;

        // Per saveslot values
        public static bool VesselErrorAlerted = false;

        public static bool EditorShipEditingMode = false;
        public static double EditorRolloutCost = 0;
        public static double EditorRolloutBP = 0;
        public static double EditorUnlockCosts = 0;
        public static double EditorToolingCosts = 0;
        public static List<string> EditorRequiredTechs = new List<string>();

        public static List<bool> ShowWindows = new List<bool> { false, true };    //build list, editor

        private static bool _isGUIInitialized = false;


        public static void Reset()
        {
            VesselErrorAlerted = false;

            KCT_GUI.ResetFormulaRateHolders();
            KCT_GUI.ResetShowFirstRunAgain();
        }

        public static void ClearVesselEditMode()
        {
            EditorShipEditingMode = false;
            Instance.EditedVessel = new VesselProject();
            Instance.MergedVessels.Clear();

            InputLockManager.RemoveControlLock("KCTEditExit");
            InputLockManager.RemoveControlLock("KCTEditLoad");
            InputLockManager.RemoveControlLock("KCTEditNew");
            InputLockManager.RemoveControlLock("KCTEditLaunch");
            EditorLogic.fetch?.Unlock("KCTEditorMouseLock");
        }

        #endregion

        #region Savegame Data

        [KSPField(isPersistant = true)]
        public bool enabledForSave = HighLogic.CurrentGame.Mode == Game.Modes.CAREER ||
                                     HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX ||
                                     HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX;

        [KSPField(isPersistant = true)] public float SciPointsTotal = -1f;
        [KSPField(isPersistant = true)] public bool IsSimulatedFlight = false;
        [KSPField(isPersistant = true)] public bool ExperimentalPartsEnabled = true;
        [KSPField(isPersistant = true)] public bool DisableFailuresInSim = true;
        [KSPField(isPersistant = true)] public int Researchers = 0;
        [KSPField(isPersistant = true)] public int Applicants = 0;

        [KSPField(isPersistant = true)] public string KACAlarmId = string.Empty;
        [KSPField(isPersistant = true)] public double KACAlarmUT = 0;

        [KSPField(isPersistant = true)] public bool ErroredDuringOnLoad = false;

        #region First Run
        [KSPField(isPersistant = true)] public bool StarterLCBuilding = false;
        [KSPField(isPersistant = true)] public bool HiredStarterApplicants = false;
        [KSPField(isPersistant = true)] public bool StartedProgram = false;
        [KSPField(isPersistant = true)] public bool AcceptedContract = false;
        public bool FirstRunNotComplete => !(StarterLCBuilding && HiredStarterApplicants && StartedProgram && AcceptedContract)
            && !DontShowFirstRunAgain;
        [KSPField(isPersistant = true)] public bool DontShowFirstRunAgain = false;
        #endregion

        public const int VERSION = 8;
        [KSPField(isPersistant = true)] public int LoadedSaveVersion = VERSION;

        [KSPField(isPersistant = true)] public bool IsFirstStart = true;

        [KSPField(isPersistant = true)] public SimulationParams SimulationParams = new SimulationParams();


        [KSPField(isPersistant = true)]
        private PersistentList<LCEfficiency> _lcEfficiencies = new PersistentList<LCEfficiency>();
        public PersistentList<LCEfficiency> LCEfficiencies => _lcEfficiencies;
        public Dictionary<LaunchComplex, LCEfficiency> LCToEfficiency = new Dictionary<LaunchComplex, LCEfficiency>();

        private readonly Dictionary<Guid, LaunchComplex> _LCIDtoLC = new Dictionary<Guid, LaunchComplex>();
        public LaunchComplex LC(Guid id) => _LCIDtoLC.TryGetValue(id, out var lc) ? lc : null;
        private readonly Dictionary<Guid, LCLaunchPad> _LPIDtoLP = new Dictionary<Guid, LCLaunchPad>();
        public LCLaunchPad LP(Guid id) => _LPIDtoLP[id];

        [KSPField(isPersistant = true)]
        public PersistentObservableList<ResearchProject> TechList = new PersistentObservableList<ResearchProject>();

        [KSPField(isPersistant = true)]
        public PersistentSortedListValueTypeKey<string, VesselProject> BuildPlans = new PersistentSortedListValueTypeKey<string, VesselProject>();

        [KSPField(isPersistant = true)]
        public PersistentList<LCSpaceCenter> KSCs = new PersistentList<LCSpaceCenter>();
        public LCSpaceCenter ActiveSC = null;

        [KSPField(isPersistant = true)]
        public VesselProject LaunchedVessel = new VesselProject();
        [KSPField(isPersistant = true)]
        public VesselProject EditedVessel = new VesselProject();
        [KSPField(isPersistant = true)]
        public VesselProject RecoveredVessel = new VesselProject();

        [KSPField(isPersistant = true)]
        public PersistentList<PartCrewAssignment> LaunchedCrew = new PersistentList<PartCrewAssignment>();

        [KSPField(isPersistant = true)]
        public AirlaunchParams AirlaunchParams = new AirlaunchParams();

        [KSPField(isPersistant = true)]
        public FundTargetProject fundTarget = new FundTargetProject();

        [KSPField(isPersistant = true)]
        public HireStaffProject staffTarget = new HireStaffProject();

        #endregion

        #region Fields

        public bool DoingVesselRepair;

        public bool MergingAvailable;
        public List<VesselProject> MergedVessels = new List<VesselProject>();

        private Button.ButtonClickedEvent _recoverCallback, _flyCallback;
        private SpaceTracking _trackingStation;

        public bool IsEditorRecalcuationRequired = false;
        private bool _hasFirstRecalculated = false;

        private static WaitForSeconds _wfsHalf = new WaitForSeconds(0.5f), _wfsOne = new WaitForSeconds(1f), _wfsTwo = new WaitForSeconds(2f);
        private double _lastRateUpdateUT = 0;
        private double _lastYearMultUpdateUT = 0;

        internal const string KCTLaunchLock = "KCTLaunchLock";
        internal const string KCTKSCLock = "KCTKSCLock";
        private const float BUILD_TIME_INTERVAL = 0.5f;
        private const float YEAR_MULT_TIME_INTERVAL = 86400 * 7;

        // Editor fields
        public VesselProject EditorVessel = new VesselProject("temp", "LaunchPad", 0d, 0d, string.Empty, 0f, EditorFacility.VAB, false);
        public Guid PreEditorSwapLCID = Guid.Empty;
        public bool IsLaunchSiteControllerDisabled;

        private DateTime _simMoveDeferTime = DateTime.MaxValue;
        private int _simMoveSecondsRemain = 0;

        private GameObject _simWatermark;

        #endregion

        public static SpaceCenterManagement Instance { get; private set; }

        #region Lifecycle

        public override void OnAwake()
        {
            base.OnAwake();
            if (Instance != null)
                Destroy(Instance);

            Instance = this;

            if (KSPUtils.CurrentGameIsMission()) return;

            KCTSettings.Instance.Load();

            if (PresetManager.Instance == null)
            {
                PresetManager.Instance = new PresetManager();
            }
            PresetManager.Instance.SetActiveFromSaveData();

            var obj = new GameObject("KCTToolbarControl");
            ToolbarControl = obj.AddComponent<ToolbarControl>();
            ToolbarControl.AddToAllToolbars(null, null,
                null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.VAB,
                _modId,
                "MainButton",
                KCTUtilities._icon_KCT_On_38,
                KCTUtilities._icon_KCT_Off_38,
                KCTUtilities._icon_KCT_On_24,
                KCTUtilities._icon_KCT_Off_24,
                _modName
                );

            ToolbarControl.AddLeftRightClickCallbacks(KCT_GUI.ClickToggle, KCT_GUI.OnRightClick);
        }

        public void Start()
        {
            KCT_GUI.InitTooltips();

            if (KSPUtils.CurrentGameIsMission()) return;

            if (IsFirstStart)
            {
                PresetManager.Instance.SaveActiveToSaveData();
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
                SimulationParams.Reset();
            }

            switch (HighLogic.LoadedScene)
            {
                case GameScenes.EDITOR:
                    KCT_GUI.HideAll();
                    if (!KCT_GUI.IsPrimarilyDisabled)
                    {
                        KCT_GUI.GUIStates.ShowEditorGUI = ShowWindows[1];
                        if (EditorShipEditingMode)
                            KCT_GUI.EnsureEditModeIsVisible();
                        else
                            KCT_GUI.ToggleVisibility(KCT_GUI.GUIStates.ShowEditorGUI);
                    }
                    EditorStart();
                    break;
                case GameScenes.SPACECENTER:
                    bool showFirstRun = FirstRunNotComplete;
                    KCT_GUI.HideAll();
                    ClearVesselEditMode();
                    if (showFirstRun)
                    {
                        KCT_GUI.GUIStates.ShowFirstRun = true;
                    }
                    else
                    {
                        KCT_GUI.GUIStates.ShowBuildList = ShowWindows[0];
                        KCT_GUI.ToggleVisibility(KCT_GUI.GUIStates.ShowBuildList);
                    }
                    StartCoroutine(UpdateFacilityLevels());
                    break;
                case GameScenes.TRACKSTATION:
                    ClearVesselEditMode();
                    _trackingStation = FindObjectOfType<SpaceTracking>();
                    if (_trackingStation != null)
                    {
                        _recoverCallback = _trackingStation.RecoverButton.onClick;
                        _flyCallback = _trackingStation.FlyButton.onClick;

                        _trackingStation.RecoverButton.onClick = new Button.ButtonClickedEvent();
                        _trackingStation.RecoverButton.onClick.AddListener(RecoveryChoiceTS);
                    }
                    break;
                case GameScenes.FLIGHT:
                    KCT_GUI.HideAll();
                    FlightStart();
                    break;
            }

            StartFinished();
        }

        private void EditorStart()
        {
            KCT_GUI.BuildRateForDisplay = null;
            if (!KCT_GUI.IsPrimarilyDisabled)
            {
                IsEditorRecalcuationRequired = true;
            }
            InvokeRepeating("EditorRecalculation", 0.02f, 1f);
            StartCoroutine(HandleEditorButton_Coroutine());
        }

        private void FlightStart()
        {
            if (FindObjectOfType<AltimeterSliderButtons>() is AltimeterSliderButtons altimeter)
            {
                _recoverCallback = altimeter.vesselRecoveryButton.onClick;

                altimeter.vesselRecoveryButton.onClick = new Button.ButtonClickedEvent();
                altimeter.vesselRecoveryButton.onClick.AddListener(RecoveryChoiceFlight);
            }

            if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
                ProcessNewFlight();
        }

        private void StartFinished()
        {
            RP0Debug.Log("DelayedStart start");
            if (PresetManager.Instance?.ActivePreset == null || !PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return;

            if (KCT_GUI.IsPrimarilyDisabled) return;

            //The following should only be executed when fully enabled for the save

            if (ActiveSC == null)
            {
                // This should not be hit, because either KSCSwitcher's LastKSC loads after KCTData
                // or KCTData loads first and the harmony patch runs.
                // But I'm leaving it here just in case.
                SetActiveKSCToRSS();
            }

            CheckMissingParts();

            switch (HighLogic.LoadedScene)
            {
                case GameScenes.EDITOR:
                    if (EditorShipEditingMode)
                    {
                        RP0Debug.Log($"Editing {EditedVessel.shipName}");
                        EditorLogic.fetch.shipNameField.text = EditedVessel.shipName;
                    }
                    break;
                case GameScenes.SPACECENTER:
                    if (!KCT_GUI.IsPrimarilyDisabled)
                    {
                        // TODO: This looks like a duplicate of the code in Start's switch, can we combine?
                        if (ToolbarManager.ToolbarAvailable && KCTSettings.Instance.PreferBlizzyToolbar)
                        {
                            if (ShowWindows[0])
                                KCT_GUI.ToggleVisibility(true);
                            else
                            {
                                if (SCMEvents.Instance != null && ToolbarControl != null)
                                {
                                    if (ShowWindows[0])
                                        KCT_GUI.ToggleVisibility(true);
                                }
                            }
                        }
                        KCT_GUI.ResetBLWindow();
                    }
                    else
                    {
                        KCT_GUI.GUIStates.ShowBuildList = false;
                        ShowWindows[0] = false;
                    }

                    if (IsFirstStart)
                    {
                        IsFirstStart = false;
                        KCT_GUI.GUIStates.ShowFirstRun = true;
                        foreach (var ksc in KSCs)
                            ksc.EnsureStartingLaunchComplexes();

                        Applicants = Database.SettingsSC.GetStartingPersonnel(HighLogic.CurrentGame.Mode);
                    }
                    else if (FirstRunNotComplete)
                    {
                        KCT_GUI.GUIStates.ShowFirstRun = true;
                    }

                    break;

                case GameScenes.FLIGHT:
                    if (IsSimulatedFlight)
                    {
                        KCTUtilities.EnableSimulationLocks();
                        if (SimulationParams.SimulationUT > 0 &&
                            FlightDriver.CanRevertToPrelaunch)    // Used for checking whether the player has saved and then loaded back into that save
                        {
                            // Advance building construction
                            double UToffset = SimulationParams.SimulationUT - Planetarium.GetUniversalTime();
                            if (UToffset > 0)
                            {
                                foreach (var ksc in KSCs)
                                {
                                    for (int i = 0; i < ksc.Constructions.Count; ++i)
                                    {
                                        var c = ksc.Constructions[i];
                                        double t = c.GetTimeLeft();
                                        if (t <= UToffset)
                                            c.progress = c.BP;
                                    }
                                }
                            }
                            RP0Debug.Log($"Setting simulation UT to {SimulationParams.SimulationUT}");
                            if (!ModUtils.IsPrincipiaInstalled)
                                Planetarium.SetUniversalTime(SimulationParams.SimulationUT);
                            else
                                StartCoroutine(EaseSimulationUT_Coroutine(Planetarium.GetUniversalTime(), SimulationParams.SimulationUT));
                        }

                        AddSimulationWatermark();
                    }
                    break;
            }

            if (IsSimulatedFlight && HighLogic.LoadedSceneIsGame && !HighLogic.LoadedSceneIsFlight)
            {
                string msg = $"The current save appears to be a simulation and we cannot automatically find a suitable pre-simulation save. Please load an older save manually; we recommend the backup that should have been saved to \\saves\\{HighLogic.SaveFolder}\\Backup\\KCT_simulation_backup.sfs";
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "errorPopup", "Simulation Error", msg, "Understood", false, HighLogic.UISkin);
            }
        }

        public void FixedUpdate()
        {
            if (KSPUtils.CurrentGameIsMission()) return;
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

            if (HighLogic.LoadedSceneIsFlight && IsSimulatedFlight)
            {
                ProcessSimulation();
            }
        }

        public void OnDestroy()
        {
            _simWatermark?.DestroyGameObject();

            if (ToolbarControl != null)
            {
                ToolbarControl.OnDestroy();
                Destroy(ToolbarControl);
            }
            KCT_GUI.ClearTooltips();
            KCT_GUI.OnDestroy();

            if (Instance == this)
                Instance = null;
        }

        public void OnGUI()
        {
            if (KSPUtils.CurrentGameIsMission()) return;

            if (!_isGUIInitialized)
            {
                KCT_GUI.InitBuildListVars();
                _isGUIInitialized = true;
            }
            KCT_GUI.SetGUIPositions();
        }

        #endregion
                
        #region Persistence

        public override void OnSave(ConfigNode node)
        {
            if (KSPUtils.CurrentGameIsMission()) return;

            RP0Debug.Log("Writing to persistence.");
            base.OnSave(node);

            KCT_GUI.GuiDataSaver.Save();
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                base.OnLoad(node);
                Database.LoadTree();

                if (KSPUtils.CurrentGameIsMission()) return;

                RP0Debug.Log("Reading from persistence.");

                TechList.Updated += techListUpdated;

                bool foundStockKSC = false;
                foreach (var ksc in KSCs)
                {
                    if (ksc.KSCName.Length > 0 && string.Equals(ksc.KSCName, KSCSwitcherInterop.LegacyDefaultKscId, StringComparison.OrdinalIgnoreCase))
                    {
                        foundStockKSC = true;
                        break;
                    }
                }

                SetActiveKSCToRSS();
                if (foundStockKSC)
                    TryMigrateStockKSC();

                // Prune bad or inactive KSCs.
                for (int i = KSCs.Count; i-- > 0;)
                {
                    LCSpaceCenter ksc = KSCs[i];
                    if (ksc.KSCName == null || ksc.KSCName.Length == 0 || (ksc.IsEmpty && ksc != ActiveSC))
                        KSCs.RemoveAt(i);
                }

                foreach (var vp in BuildPlans.Values)
                    vp.LinkToLC(null);

                LaunchedVessel.LinkToLC(LC(LaunchedVessel.LCID));
                RecoveredVessel.LinkToLC(LC(RecoveredVessel.LCID));
                EditedVessel.LinkToLC(LC(EditedVessel.LCID));

                LCEfficiency.RelinkAll();

                if (LoadedSaveVersion < VERSION)
                {
                    // This upgrades to new payloads
                    // NOTE this upgrade has to come before other upgrades
                    // that touch ship nodes, because they will do the UpgradePipeline
                    // and lose the resources with no signal to the player
                    if (LoadedSaveVersion < 5)
                    {
                        List<VesselProject> upgradedVessels = new List<VesselProject>();
                        foreach (var ksc in KSCs)
                        {
                            foreach (var lc in ksc.LaunchComplexes)
                            {
                                foreach (var vp in lc.BuildList)
                                    if (FixVesselSatPayload(vp))
                                        upgradedVessels.Add(vp);
                                foreach (var vp in lc.Warehouse)
                                    if (FixVesselSatPayload(vp))
                                        upgradedVessels.Add(vp);
                            }
                        }
                        if (upgradedVessels.Count > 0)
                        {
                            string vesselStr = string.Empty;
                            foreach (var vp in upgradedVessels)
                                vesselStr += Localizer.Format("#rp0_Persistence_UpgradedSatPayload_Vessel", vp.shipName, vp.LC.LCType == LaunchComplexType.Pad ? vp.LC.Name : Localizer.GetStringByTag("#rp0_Hangar"));

                            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "RP0VesselsUpgradedPayload",
                                         Localizer.GetStringByTag("#rp0_Persistence_UpgradedSatPayload_Title"),
                                         Localizer.Format("#rp0_Persistence_UpgradedSatPayload_Text", vesselStr),
                                         Localizer.GetStringByTag("#autoLOC_190905"),
                                         false,
                                         HighLogic.UISkin,
                                         false);
                            // Note: not hiding GUIs because this is during load
                        }
                    }

                    // This upgrades to leader effect tracking
                    // Note that if the vessel had a payload resource, the BP
                    // will now be a bit wrong for editing to use the new part
                    if (LoadedSaveVersion < 4)
                    {
                        foreach (var ksc in KSCs)
                        {
                            foreach (var lc in ksc.LaunchComplexes)
                            {
                                foreach (var vp in lc.BuildList)
                                    vp.RecalculateFromNode(true);
                                foreach (var vp in lc.Warehouse)
                                    vp.RecalculateFromNode(true);
                            }
                        }
                    }
                    
                    LoadedSaveVersion = VERSION;
                }
            }
            catch (Exception ex)
            {
                ErroredDuringOnLoad = true;
                RP0Debug.LogError("ERROR! An error while KCT loading data occurred. Things will be seriously broken!\n" + ex);
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "errorPopup", "Error Loading RP-1 Data", "ERROR! An error occurred while loading RP-1 data. Things will be seriously broken! Please report this error to RP-1 GitHub and attach the log file. The game will be UNPLAYABLE in this state!", "Understood", false, HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }

        private bool FixVesselSatPayload(VesselProject vp)
        {
            if (vp.resourceAmounts.ContainsKey("ComSatPayload")
                || vp.resourceAmounts.ContainsKey("NavSatPayload")
                || vp.resourceAmounts.ContainsKey("WeatherSatPayload"))
            {
                if (!vp.UpgradeShipNode())
                {
                    RP0Debug.LogError(vp.shipName + " has sat payload but upgrade failed! Either was already upgraded or something else went wrong. Aborting.");
                    return false;
                }

                vp.resourceAmounts.Remove("ComSatPayload");
                vp.resourceAmounts.Remove("NavSatPayload");
                vp.resourceAmounts.Remove("WeatherSatPayload");

                RP0Debug.Log(vp.shipName + " contains sat payload resources!");
                return true;
            }

            return false;
        }

        private void TryMigrateStockKSC()
        {
            LCSpaceCenter stockKsc = KSCs.Find(k => string.Equals(k.KSCName, KSCSwitcherInterop.LegacyDefaultKscId, StringComparison.OrdinalIgnoreCase));
            if (KSCs.Count == 1)
            {
                // Rename the stock KSC to the new default (Cape)
                stockKsc.KSCName = KSCSwitcherInterop.DefaultKscId;
                SetActiveKSC(stockKsc.KSCName);
                return;
            }

            if (stockKsc.IsEmpty)
            {
                // Nothing provisioned into the stock KSC so it's safe to just delete it
                KSCs.Remove(stockKsc);
                SetActiveKSCToRSS();
                return;
            }

            int numOtherUsedKSCs = KSCs.Count(k => !k.IsEmpty && k != stockKsc);
            if (numOtherUsedKSCs == 0)
            {
                string kscName = KSCSwitcherInterop.GetActiveRSSKSC() ?? KSCSwitcherInterop.DefaultKscId;
                LCSpaceCenter newDefault = KSCs.Find(k => string.Equals(k.KSCName, kscName, StringComparison.OrdinalIgnoreCase));
                if (newDefault != null)
                {
                    // Stock KSC isn't empty but the new default one is - safe to rename the stock and remove the old default item
                    stockKsc.KSCName = newDefault.KSCName;
                    KSCs.Remove(newDefault);
                    SetActiveKSC(stockKsc);
                    return;
                }
            }

            // Can't really do anything if there's multiple KSCs in use.
            if (!KSCSwitcherInterop.IsKSCSwitcherInstalled)
            {
                // Need to switch back to the legacy "Stock" KSC if KSCSwitcher isn't installed
                SetActiveKSC(stockKsc.KSCName);
            }
        }

        private void CheckMissingParts()
        {
            RP0Debug.Log("Checking vessels for missing parts.");
            //check that all parts are valid in all ships. If not, warn the user and disable that vessel (once that code is written)
            if (!VesselErrorAlerted)
            {
                var erroredVessels = new List<VesselProject>();
                foreach (LCSpaceCenter KSC in KSCs)
                {
                    foreach (LaunchComplex currentLC in KSC.LaunchComplexes)
                    {
                        foreach (VesselProject vp in currentLC.BuildList)
                        {
                            if (!vp.AllPartsValid) // will cache for later use in this scene
                            {
                                RP0Debug.Log(vp.shipName + " contains invalid parts!");
                                erroredVessels.Add(vp);
                            }
                        }
                        foreach (VesselProject vp in currentLC.Warehouse)
                        {
                            if (!vp.AllPartsValid)
                            {
                                RP0Debug.Log(vp.shipName + " contains invalid parts!");
                                erroredVessels.Add(vp);
                            }
                        }
                    }
                }
                if (erroredVessels.Count > 0)
                    PopUpVesselError(erroredVessels);
                VesselErrorAlerted = true;
            }
        }

        private void PopUpVesselError(List<VesselProject> errored)
        {
            DialogGUIBase[] options = new DialogGUIBase[2];
            options[0] = new DialogGUIButton("Understood", () => { });
            options[1] = new DialogGUIButton("Delete Vessels", () =>
            {
                foreach (VesselProject vp in errored)
                {
                    vp.RemoveFromBuildList(out _);
                    KCTUtilities.AddFunds(vp.GetTotalCost(), TransactionReasonsRP0.VesselPurchase);
                    //remove any associated recon_rollout
                }
            });

            string txt = "The following stored/building vessels contain missing or invalid parts and have been quarantined. Either add the missing parts back into your game or delete the vessels. A file containing the ship names and missing parts has been added to your save folder.\n";
            string txtToWrite = "";
            foreach (VesselProject vp in errored)
            {
                txt += vp.shipName + "\n";
                txtToWrite += vp.shipName + "\n";
                txtToWrite += string.Join("\n", vp.GetMissingParts());
                txtToWrite += "\n\n";
            }

            //make new file for missing ships
            string filename = KSPUtil.ApplicationRootPath + "/saves/" + HighLogic.SaveFolder + "/missingParts.txt";
            File.WriteAllText(filename, txtToWrite);

            //remove all rollout and recon items since they're invalid without the ships
            foreach (VesselProject vp in errored)
            {
                //remove any associated recon_rollout
                foreach (LCSpaceCenter ksc in KSCs)
                {
                    foreach (LaunchComplex currentLC in ksc.LaunchComplexes)
                    {
                        for (int i = 0; i < currentLC.Recon_Rollout.Count; i++)
                        {
                            ReconRolloutProject rr = currentLC.Recon_Rollout[i];
                            if (rr.AssociatedIdAsGuid == vp.shipID)
                            {
                                currentLC.Recon_Rollout.Remove(rr);
                                i--;
                            }
                        }
                    }
                }
            }

            var diag = new MultiOptionDialog("missingPartsPopup", txt, "Vessels Contain Missing Parts", null, options);
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
        }

        #endregion

        #region Tech

        public bool TechListHas(string techID)
        {
            return TechListIndex(techID) != -1;
        }

        public int TechListIndex(string techID)
        {
            for (int i = TechList.Count; i-- > 0;)
                if (TechList[i].techID == techID)
                    return i;

            return -1;
        }

        private void UpdateTechYearMults()
        {
            for (int i = TechList.Count - 1; i >= 0; i--)
            {
                var t = TechList[i];
                t.UpdateBuildRate(i);
            }
        }

        public void UpdateTechTimes()
        {
            for (int j = 0; j < TechList.Count; j++)
                TechList[j].UpdateBuildRate(j);
        }

        private void techListUpdated()
        {
            if (TechListIgnoreUpdates)
                return;

            TechListUpdated();
        }

        public void TechListUpdated()
        {
            MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
            Harmony.PatchRDTechTree.Instance?.RefreshUI();
        }

        #endregion

        #region LC

        public void RegisterLC(LaunchComplex lc)
        {
            _LCIDtoLC[lc.ID] = lc;
        }

        public bool UnregisterLC(LaunchComplex lc)
        {
            return _LCIDtoLC.Remove(lc.ID);
        }

        public void RegisterLP(LCLaunchPad lp)
        {
            _LPIDtoLP[lp.id] = lp;
        }

        public bool UnregsiterLP(LCLaunchPad lp)
        {
            return _LPIDtoLP.Remove(lp.id);
        }

        public LaunchComplex FindLCFromID(Guid guid)
        {
            return LC(guid);
        }

        public VesselRepairProject FindRepairForVessel(Vessel v)
        {
            foreach (var ksc in KSCs)
            {
                foreach (var lc in ksc.LaunchComplexes)
                {
                    var r = lc.VesselRepairs.Find(r => r.AssociatedIdAsGuid == v.id);
                    if (r != null) return r;
                }
            }

            return null;
        }

        #endregion

        #region KSC

        private void SetActiveKSCToRSS()
        {
            string site = KSCSwitcherInterop.GetActiveRSSKSC();
            SetActiveKSC(site);
        }

        public void SetActiveKSC(string site)
        {
            if (site == null || site.Length == 0)
                site = KSCSwitcherInterop.DefaultKscId;
            if (ActiveSC == null || site != ActiveSC.KSCName)
            {
                RP0Debug.Log($"Setting active site to {site}");
                LCSpaceCenter newKsc = KSCs.FirstOrDefault(ksc => ksc.KSCName == site);
                if (newKsc == null)
                {
                    newKsc = new LCSpaceCenter(site);
                    newKsc.EnsureStartingLaunchComplexes();
                    KSCs.Add(newKsc);
                }

                SetActiveKSC(newKsc);
            }
        }

        private void SetActiveKSC(LCSpaceCenter ksc)
        {
            if (ksc == null || ksc == ActiveSC)
                return;

            // TODO: Allow setting KSC outside the tracking station
            // which will require doing some work on KSC switch
            ActiveSC = ksc;
        }

        #endregion

        #region Budget

        public double GetEffectiveIntegrationEngineersForSalary(LCSpaceCenter ksc)
        {
            double engineers = 0d;
            foreach (var lc in ksc.LaunchComplexes)
                engineers += GetEffectiveEngineersForSalary(lc);
            return engineers + ksc.UnassignedEngineers * Database.SettingsSC.IdleSalaryMult;
        }

        public double GetEffectiveEngineersForSalary(LCSpaceCenter ksc) => GetEffectiveIntegrationEngineersForSalary(ksc);

        public double GetEffectiveEngineersForSalary(LaunchComplex lc)
        {
            if (lc.IsOperational && lc.Engineers > 0)
            {
                if (!lc.IsActive)
                    return lc.Engineers * Database.SettingsSC.IdleSalaryMult;

                if (lc.IsHumanRated && lc.BuildList.Count > 0 && !lc.BuildList[0].humanRated)
                {
                    int num = Math.Min(lc.Engineers, lc.MaxEngineersFor(lc.BuildList[0]));
                    return num * lc.RushSalary + (lc.Engineers - num) * Database.SettingsSC.IdleSalaryMult;
                }

                return lc.Engineers * lc.RushSalary;
            }

            return 0;
        }

        public double GetBudgetDelta(double deltaTime)
        {
            // note NetUpkeepPerDay is negative or 0.

            double averageSubsidyPerDay = CurrencyUtils.Funds(TransactionReasonsRP0.Subsidy, MaintenanceHandler.GetAverageSubsidyForPeriod(deltaTime)) * (1d / 365.25d);
            double fundDelta = Math.Min(0d, MaintenanceHandler.Instance.UpkeepPerDayForDisplay + averageSubsidyPerDay) * deltaTime * (1d / 86400d)
                + GetConstructionCostOverTime(deltaTime) + GetRolloutCostOverTime(deltaTime)
                + Programs.ProgramHandler.Instance.GetDisplayProgramFunding(deltaTime);

            return fundDelta;
        }

        public double GetConstructionCostOverTime(double time)
        {
            double delta = 0;
            foreach (var ksc in KSCs)
            {
                delta += GetConstructionCostOverTime(time, ksc);
            }
            return delta;
        }

        public double GetConstructionCostOverTime(double time, LCSpaceCenter ksc)
        {
            double delta = 0;
            foreach (var c in ksc.Constructions)
                delta += c.GetConstructionCostOverTime(time);

            return delta;
        }

        public double GetConstructionCostOverTime(double time, string kscName)
        {
            foreach (var ksc in KSCs)
            {
                if (ksc.KSCName == kscName)
                {
                    return GetConstructionCostOverTime(time, ksc);
                }
            }

            return 0d;
        }

        public double GetRolloutCostOverTime(double time)
        {
            double delta = 0;
            foreach (var ksc in KSCs)
            {
                delta += GetRolloutCostOverTime(time, ksc);
            }
            return delta;
        }

        public double GetRolloutCostOverTime(double time, LCSpaceCenter ksc)
        {
            double delta = 0;
            for (int i = 0; i < ksc.LaunchComplexes.Count; ++i)
                delta += GetRolloutCostOverTime(time, ksc.LaunchComplexes[i]);

            return delta;
        }

        public double GetRolloutCostOverTime(double time, LaunchComplex lc)
        {
            double delta = 0;
            foreach (var rr in lc.Recon_Rollout)
            {
                if (rr.RRType != ReconRolloutProject.RolloutReconType.Rollout && rr.RRType != ReconRolloutProject.RolloutReconType.AirlaunchMount)
                    continue;

                double t = rr.GetTimeLeft();
                double fac = 1d;
                if (t > time)
                    fac = time / t;

                delta += CurrencyUtils.Funds(rr.TransactionReason, -rr.cost * (1d - rr.progress / rr.BP) * fac);
            }

            return delta;
        }

        public double GetRolloutCostOverTime(double time, string kscName)
        {
            foreach (var ksc in KSCs)
            {
                if (ksc.KSCName == kscName)
                {
                    return GetRolloutCostOverTime(time, ksc);
                }
            }

            return 0d;
        }

        public int TotalEngineers
        {
            get
            {
                int eng = 0;
                foreach (var ksc in KSCs)
                    eng += ksc.Engineers;

                return eng;
            }
        }

        public double WeightedAverageEfficiencyEngineers
        {
            get
            {
                double effic = 0d;
                int engineers = 0;
                foreach (var ksc in KSCs)
                {
                    foreach (var lc in ksc.LaunchComplexes)
                    {
                        if (!lc.IsOperational || lc.LCType == LaunchComplexType.Hangar)
                            continue;

                        if (lc.Engineers == 0d)
                            continue;

                        engineers += lc.Engineers;
                        effic += lc.Efficiency * engineers;
                    }
                }

                if (engineers == 0)
                    return 0d;

                return effic / engineers;
            }
        }
        #endregion

        #region Flight

        private void ProcessNewFlight()
        {
            VesselProject vp = LaunchedVessel;
            var dataModule = (KCTVesselTracker)FlightGlobals.ActiveVessel.vesselModules.Find(vm => vm is KCTVesselTracker);
            if (dataModule != null)
            {
                if (string.IsNullOrWhiteSpace(dataModule.Data.LaunchID))
                {
                    dataModule.Data.LaunchID = Guid.NewGuid().ToString("N");
                    RP0Debug.Log($"Assigned LaunchID: {dataModule.Data.LaunchID}");
                }

                // This will only fire the first time, because we make it invalid afterwards by clearing the VP
                if (vp.IsValid)
                {
                    dataModule.Data.FacilityBuiltIn = vp.FacilityBuiltIn;
                    dataModule.Data.VesselID = vp.KCTPersistentID;
                    dataModule.Data.LCID = vp.LCID;
                    if (dataModule.Data.LCID != Guid.Empty)
                        dataModule.Data.LCModID = vp.LC.ModID;
                }
            }

            if (KCT_GUI.IsPrimarilyDisabled) return;

            AssignCrewToCurrentVessel();

            // This only fires the first time because we clear the VP afterwards.
            if (vp.IsValid)
            {
                LaunchComplex vesselLC = vp.LC;
                RP0Debug.Log("Attempting to remove launched vessel from build list");
                if (vp.RemoveFromBuildList(out _)) //Only do these when the vessel is first removed from the list
                {
                    //Add the cost of the ship to the funds so it can be removed again by KSP
                    FlightGlobals.ActiveVessel.vesselName = vp.shipName;
                }
                if (vesselLC == null) vesselLC = ActiveSC.ActiveLC;
                if (vesselLC.Recon_Rollout.FirstOrDefault(r => r.AssociatedIdAsGuid == vp.shipID) is ReconRolloutProject rollout)
                    vesselLC.Recon_Rollout.Remove(rollout);

                LaunchedVessel = new VesselProject();
            }

            var alParams = AirlaunchParams;
            if ((vp.IsValid && alParams.KCTVesselId == vp.shipID) ||
                alParams.KSPVesselId == FlightGlobals.ActiveVessel.id)
            {
                if (alParams.KSPVesselId == Guid.Empty)
                    alParams.KSPVesselId = FlightGlobals.ActiveVessel.id;
                StartCoroutine(AirlaunchRoutine(alParams, FlightGlobals.ActiveVessel.id));

                // Clear the KCT vessel ID but keep KSP's own ID.
                // 'Revert To Launch' state is saved some frames after the scene got loaded so LaunchedVessel is no longer there.
                // In this case we use KSP's own id to figure out if airlaunch should be done.
                AirlaunchParams.KCTVesselId = Guid.Empty;
            }
        }

        private void AssignCrewToCurrentVessel()
        {
            if (!IsSimulatedFlight &&
                FlightGlobals.ActiveVessel.GetCrewCount() == 0 && LaunchedCrew.Count > 0)
            {
                KerbalRoster roster = HighLogic.CurrentGame.CrewRoster;
                foreach (Part p in FlightGlobals.ActiveVessel.parts)
                {
                    RP0Debug.Log($"Part being tested: {p.partInfo.title}");
                    if (p.CrewCapacity == 0 || !(LaunchedCrew.Find(part => part.PartID == p.craftID) is PartCrewAssignment cp))
                        continue;
                    List<CrewMemberAssignment> crewList = cp.CrewList;
                    RP0Debug.Log($"cP.crewList.Count: {cp.CrewList.Count}");
                    foreach (CrewMemberAssignment assign in crewList)
                    {
                        ProtoCrewMember crewMember = assign.PCM;
                        if (crewMember == null)
                            continue;

                        try
                        {
                            if (p.AddCrewmember(crewMember))
                            {
                                RP0Debug.Log($"Assigned {crewMember.name} to {p.partInfo.name}");
                                crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
                                crewMember.seat?.SpawnCrew();
                            }
                            else
                            {
                                RP0Debug.LogError($"Error when assigning {crewMember.name} to {p.partInfo.name}");
                                crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                            }
                        }
                        catch (Exception ex)
                        {
                            RP0Debug.LogError($"Error when assigning {crewMember.name} to {p.partInfo.name}: {ex}");
                            crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                        }
                    }
                }
                LaunchedCrew.Clear();
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

            KCTUtilities.DoAirlaunch(launchParams);

            if (ModUtils.IsPrincipiaInstalled)
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
            RP0Debug.Log($"Finished clobbering vessel situation of {FlightGlobals.ActiveVessel.name} to PRELAUNCH (for Prinicipia stability), now firing change event to FLYING.");
            FlightGlobals.ActiveVessel.situation = Vessel.Situations.FLYING;
            GameEvents.onVesselSituationChange.Fire(new GameEvents.HostedFromToAction<Vessel, Vessel.Situations>(FlightGlobals.ActiveVessel, Vessel.Situations.PRELAUNCH, Vessel.Situations.FLYING));
        }

        private void ProcessSimulation()
        {
            HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = false;

            SimulationParams simParams = SimulationParams;
            if (FlightGlobals.ActiveVessel.loaded && !FlightGlobals.ActiveVessel.packed && !simParams.IsVesselMoved)
            {
                if (simParams.DisableFailures)
                {
                    ModUtils.ToggleFailures(!simParams.DisableFailures);
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
            RP0Debug.Log($"Moving vessel to orbit. {simParams.SimulationBody.bodyName}:{simParams.SimOrbitAltitude}:{simParams.SimInclination}");
            HyperEdit_Utilities.PutInOrbitAround(simParams.SimulationBody, simParams.SimOrbitAltitude, simParams.SimInclination);
        }

        private void AddSimulationWatermark()
        {
            if (!KCTSettings.Instance.ShowSimWatermark) return;

            var uiController = KSP.UI.UIMasterController.Instance;
            if (uiController == null)
            {
                RP0Debug.LogError("UIMasterController.Instance is null");
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

        private IEnumerator EaseSimulationUT_Coroutine(double startUT, double targetUT)
        {
            const double dayInSeconds = 86_400;

            if (targetUT <= Planetarium.GetUniversalTime()) yield break;

            RP0Debug.Log($"Easing jump to simulation UT in {dayInSeconds}s steps");

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

        #endregion

        #region Editor

        private void EditorRecalculation()
        {
            if (IsEditorRecalcuationRequired)
            {
                if (EditorDriver.fetch != null && !EditorDriver.fetch.restartingEditor)
                {
                    _hasFirstRecalculated = true;
                    IsEditorRecalcuationRequired = false;
                    RecalculateEditorBuildTime(EditorLogic.fetch.ship);
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
                    KCTUtilities.HandleEditorButton();
                yield return _wfsHalf;
            }
        }

        public static void ShowLaunchAlert(string launchSite)
        {
            RP0Debug.Log("Showing Launch Alert");
            if (KCT_GUI.IsPrimarilyDisabled)
            {
                EditorLogic.fetch.launchVessel();
            }
            else
            {
                KCTUtilities.TryAddVesselToBuildList(launchSite);
                // We are recalculating because vessel validation might have changed state.
                Instance.IsEditorRecalcuationRequired = true;
            }
        }

        private void RecalculateEditorBuildTime(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            LaunchComplex oldLC = EditorVessel.LC;
            var oldFac = EditorVessel.FacilityBuiltIn;

            EditorVessel = new VesselProject(ship, EditorLogic.fetch.launchSiteName, EditorLogic.FlagURL, false);
            // override LC in case of vessel editing
            if (EditorShipEditingMode)
            {
                EditorVessel.LCID = EditedVessel.LCID;
            }
            else
            {
                // Check if we switched editors
                if (oldFac != EditorVessel.FacilityBuiltIn)
                {
                    if (oldFac == EditorFacility.VAB)
                    {
                        if (oldLC.LCType == LaunchComplexType.Pad)
                        {
                            // cache this off -- we swapped editors
                            PreEditorSwapLCID = oldLC.ID;
                        }
                        // the VP constructor sets our LC type to Hangar. But let's swap to it as well.
                        if (ActiveSC.ActiveLC.LCType != LaunchComplexType.Hangar && ActiveSC.Hangar.IsOperational)
                        {
                            ActiveSC.SwitchLaunchComplex(LCSpaceCenter.HangarIndex);
                        }
                    }
                    else
                    {
                        // Try to recover a pad LC
                        bool swappedLC = false;
                        if (ActiveSC.LaunchComplexCount > 1)
                        {
                            if (PreEditorSwapLCID != Guid.Empty && ActiveSC.SwitchToLaunchComplex(PreEditorSwapLCID))
                            {
                                swappedLC = true;
                            }
                            else
                            {
                                int idx = ActiveSC.GetLaunchComplexIdxToSwitchTo(true, true);
                                if (idx != -1)
                                {
                                    ActiveSC.SwitchLaunchComplex(idx);
                                    swappedLC = true;
                                }
                            }
                            if (swappedLC)
                            {
                                EditorVessel.LC = ActiveSC.ActiveLC;
                            }
                        }
                    }
                }
            }

            if (EditorDriver.editorFacility == EditorFacility.VAB)
            {
                EditorRolloutCost = Formula.GetRolloutCost(EditorVessel);
                EditorRolloutBP = Formula.GetRolloutBP(EditorVessel);
            }
            else
            {
                // SPH lacks rollout times and costs
                EditorRolloutCost = 0;
                EditorRolloutBP = 0;
            }

            Tuple<float, List<string>> unlockInfo = KCTUtilities.GetVesselUnlockInfo(ship);
            EditorUnlockCosts = unlockInfo.Item1;
            EditorRequiredTechs = unlockInfo.Item2;
            ToolingGUI.GetUntooledPartsAndCost(out _, out float toolingCost);
            EditorToolingCosts = toolingCost;

            // It would be better to only do this if necessary, but eh.
            // It's not easy to know if various buried fields in the vp changed.
            // It would *also* be nice to not run the ER before the vp is ready
            // post craft-load, but...also eh. This is fine.
            Harmony.PatchEngineersReport.UpdateCraftStats();
        }

        #endregion

        #region Spacecenter

        // Ran every 30 FixedUpdates, which we will treat as 0.5 seconds for now.
        // First we update locked buildings, then we loop on pad.
        // FIXME we could do this on event, but sometimes things get hinky.
        private IEnumerator UpdateFacilityLevels()
        {
            // Only run during Space Center in career mode
            // Also need to wait a bunch of frames until KSP has initialized Upgradable and Destructible facilities
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            if (HighLogic.LoadedScene != GameScenes.SPACECENTER || !KSPUtils.CurrentGameIsCareer())
                yield break;

            FacilityUpgradeProject.UpgradeLockedFacilities();

            while (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                if (ActiveSC.ActiveLC.ActiveLPInstance is LCLaunchPad pad)
                {
                    if (KCTUtilities.GetFacilityLevel(SpaceCenterFacility.LaunchPad) != pad.level)
                    {
                        ActiveSC.ActiveLC.SwitchLaunchPad(ActiveSC.ActiveLC.ActiveLaunchPadIndex, false);
                        pad.UpdateLaunchpadDestructionState(false);
                    }
                }
                yield return _wfsHalf;
            }
        }

        #endregion

        #region Build handling

        public void RecalculateBuildRates()
        {
            LCEfficiency.RecalculateConstants();

            foreach (var ksc in KSCs)
                ksc.RecalculateBuildRates(true);

            for (int i = TechList.Count; i-- > 0;)
            {
                ResearchProject tech = TechList[i];
                tech.UpdateBuildRate(i);
            }

            Crew.CrewHandler.Instance?.RecalculateBuildRates();

            SCMEvents.OnRecalculateBuildRates.Fire();
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
                foreach (LCSpaceCenter ksc in KSCs)
                {
                    totalEngineers += ksc.Engineers;

                    for (int j = ksc.LaunchComplexes.Count - 1; j >= 0; j--)
                    {
                        LaunchComplex currentLC = ksc.LaunchComplexes[j];
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
                        while (timeForBuild > 0d && currentLC.BuildList.Count > 0)
                        {
                            timeForBuild = currentLC.BuildList[0].IncrementProgress(UTDiff);
                        }

                        for (int i = currentLC.Recon_Rollout.Count; i-- > 0;)
                        {
                            // These work in parallel so no need to track excess time
                            // FIXME: that's not _quite_ true, but it's close enough: when one
                            // completes, the others speed up, but that's hard to deal with here
                            // so I think we just eat the cost.
                            var rr = currentLC.Recon_Rollout[i];
                            rr.IncrementProgress(UTDiff);
                            //Reset the associated launchpad id when rollback completes
                            Profiler.BeginSample("RP0ProgressBuildTime.ReconRollout.FindVPesselByID");
                            if (rr.RRType == ReconRolloutProject.RolloutReconType.Rollback && rr.IsComplete()
                                && KCTUtilities.FindVPByID(rr.LC, rr.AssociatedIdAsGuid) is VesselProject vp)
                            {
                                vp.launchSiteIndex = -1;
                            }
                            Profiler.EndSample();
                        }

                        for (int i = currentLC.VesselRepairs.Count; i-- > 0;)
                        {
                            var vr = currentLC.VesselRepairs[i];
                            vr.IncrementProgress(UTDiff);
                            if (vr.IsComplete() && HighLogic.LoadedSceneIsFlight &&
                                vr.ApplyRepairs())
                            {
                                currentLC.VesselRepairs.Remove(vr);
                            }
                        }

                        currentLC.Recon_Rollout.RemoveAll(rr => rr.RRType != ReconRolloutProject.RolloutReconType.Rollout && rr.RRType != ReconRolloutProject.RolloutReconType.AirlaunchMount && rr.IsComplete());
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
                while (researchTime > 0d && TechList.Count > 0)
                {
                    researchTime = TechList[0].IncrementProgress(UTDiff);
                }

                if (fundTarget.IsValid && fundTarget.GetTimeLeft() < 0.5d)
                    fundTarget.Clear();
            }

            if (staffTarget.IsValid)
            {
                staffTarget.IncrementProgress(UTDiff);
                if (staffTarget.IsComplete())
                    staffTarget.Clear();
            }

            Profiler.EndSample();
        }

        #endregion

        #region Recovery

        // TS code
        private void Fly()
        {
            _flyCallback.Invoke();
        }

        private void PopupNoKCTRecoveryInTS()
        {
            DialogGUIBase[] options = new DialogGUIBase[2];
            options[0] = new DialogGUIButton("Go to Flight scene", Fly);
            options[1] = new DialogGUIButton("Cancel", () => { });

            var diag = new MultiOptionDialog("recoverVesselPopup", "Vessels can only be recovered for reuse in the Flight scene", "Recover Vessel", null, options: options);
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
        }

        private void RecoverToVAB()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                PopupNoKCTRecoveryInTS();
                return;
            }

            if (!KCTUtilities.RecoverActiveVesselToStorage(ProjectType.VAB))
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "vesselRecoverErrorPopup", "Error!", "There was an error while recovering the ship. Sometimes reloading the scene and trying again works. Sometimes a vessel just can't be recovered this way and you must use the stock recover system.", KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), false, HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }

        private void RecoverToSPH()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                PopupNoKCTRecoveryInTS();
                return;
            }

            if (!KCTUtilities.RecoverActiveVesselToStorage(ProjectType.SPH))
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "recoverShipErrorPopup", "Error!", "There was an error while recovering the ship. Sometimes reloading the scene and trying again works. Sometimes a vessel just can't be recovered this way and you must use the stock recover system.", KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), false, HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }

        private void DoNormalRecovery()
        {
            _recoverCallback.Invoke();
        }

        private void QueueRepairFailures()
        {
            KCT_Preset_General settings = PresetManager.Instance.ActivePreset.GeneralSettings;
            if (settings.BuildTimes)
            {
                var dataModule = FlightGlobals.ActiveVessel.vesselModules.Find(vm => vm is KCTVesselTracker) as KCTVesselTracker;
                if (dataModule != null && dataModule.Data.FacilityBuiltIn == EditorFacility.VAB)
                {
                    string launchSite = FlightDriver.LaunchSiteName;
                    LaunchComplex lc = Instance.FindLCFromID(dataModule.Data.LCID);
                    if (lc != null)
                    {
                        if (lc.LCType == LaunchComplexType.Pad && lc.ActiveLPInstance != null
                            && (launchSite == "LaunchPad" || lc.LaunchPads.Find(p => p.name == launchSite) == null))
                        {
                            launchSite = lc.ActiveLPInstance.name;
                        }
                        var proj = new VesselRepairProject(FlightGlobals.ActiveVessel, launchSite, lc);
                        lc.VesselRepairs.Add(proj);

                        KCT_GUI.GUIStates.ShowBuildList = true;
                    }
                }
            }
            else
            {
                // Do immediately?
            }
        }

        private void RecoveryChoiceTS()
        {
            if (!(_trackingStation != null && _trackingStation.SelectedVessel is Vessel selectedVessel))
            {
                RP0Debug.LogError("No Vessel selected.");
                return;
            }

            bool canRecoverSPH = KCTUtilities.IsSphRecoveryAvailable(selectedVessel);
            bool canRecoverVAB = KCTUtilities.IsVabRecoveryAvailable(selectedVessel);

            var options = new List<DialogGUIBase>();
            if (canRecoverSPH)
                options.Add(new DialogGUIButton("Recover to SPH", RecoverToSPH));
            if (canRecoverVAB)
                options.Add(new DialogGUIButton("Recover to VAB", RecoverToVAB));
            options.Add(new DialogGUIButton("Normal recovery", DoNormalRecovery));
            options.Add(new DialogGUIButton("Cancel", () => { }));

            var diag = new MultiOptionDialog("scrapVesselPopup", string.Empty, "Recover Vessel", null, options: options.ToArray());
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
        }

        private void RecoveryChoiceFlight()
        {
            if (IsSimulatedFlight)
            {
                KCT_GUI.GUIStates.ShowSimulationGUI = true;
                return;
            }

            Vessel v = FlightGlobals.ActiveVessel;
            bool isSPHAllowed = KCTUtilities.IsSphRecoveryAvailable(v);
            bool isVABAllowed = KCTUtilities.IsVabRecoveryAvailable(v);
            var options = new List<DialogGUIBase>();
            if (!v.isEVA)
            {
                string nodeTitle = ResearchAndDevelopment.GetTechnologyTitle(Database.SettingsSC.VABRecoveryTech);
                string techLimitText = string.IsNullOrEmpty(nodeTitle) ? string.Empty :
                                       $"\nAdditionally requires {nodeTitle} tech node to be researched (unless the vessel is in Prelaunch state).";
                string genericReuseText = "Allows the vessel to be launched again after a short recovery delay.";

                options.Add(new DialogGUIButtonWithTooltip("Recover to SPH", RecoverToSPH)
                {
                    OptionInteractableCondition = () => isSPHAllowed,
                    tooltipText = isSPHAllowed ? genericReuseText : "Can only be used when the vessel was built in SPH."
                });

                options.Add(new DialogGUIButtonWithTooltip("Recover to VAB", RecoverToVAB)
                {
                    OptionInteractableCondition = () => isVABAllowed,
                    tooltipText = isVABAllowed ? genericReuseText : $"Can only be used when the vessel was built in VAB.{techLimitText}"
                });

                options.Add(new DialogGUIButtonWithTooltip("Normal recovery", DoNormalRecovery)
                {
                    tooltipText = "Vessel will be scrapped and the total value of recovered parts will be refunded."
                });

                if (TFInterop.HasSupportForReset && v.GetVesselBuiltAt() != EditorFacility.SPH &&
                    TFInterop.VesselHasFailedParts(v) && FindRepairForVessel(v) == null)
                {
                    options.Add(new DialogGUIButtonWithTooltip("Repair failures", QueueRepairFailures)
                    {
                        tooltipText = "All failures will be repaired without having to leave the flight scene."
                    });
                }
            }
            else
            {
                options.Add(new DialogGUIButtonWithTooltip("Recover", DoNormalRecovery));
            }

            options.Add(new DialogGUIButton("Cancel", () => { }));

            var diag = new MultiOptionDialog("RecoverVesselPopup",
                string.Empty,
                "Recover vessel",
                null, options: options.ToArray());
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
        }

        #endregion
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
