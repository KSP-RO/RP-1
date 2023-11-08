using System.Collections.Generic;
using ROUtils.DataTypes;
using ToolbarControl_NS;
using ROUtils;

namespace RP0
{
    public class SCMEvents : HostedSingleton
    {
        new public static SCMEvents Instance { get; private set; }

        public SCMEvents(SingletonHost host) : base(host)
        {
            Instance = this;
            CreateEvents();
        }

        public override void Awake()
        {
            SubscribeToEvents();
        }

        public override void Start()
        {
            ToolbarControl.RegisterMod(SpaceCenterManagement._modId, SpaceCenterManagement._modName);
        }

        public bool KCTButtonStockImportant { get; set; }

        public static EventData<VesselProject> OnVesselAddedToBuildQueue;
        public static EventData<RDTech> OnTechQueued;
        public static EventData<ResearchProject> OnTechCompleted;
        public static EventData<FacilityUpgradeProject> OnFacilityUpgradeQueued;
        public static EventData<FacilityUpgradeProject> OnFacilityUpgradeCancel;
        public static EventData<FacilityUpgradeProject> OnFacilityUpgradeComplete;
        public static EventData<PadConstructionProject, LCLaunchPad> OnPadConstructionQueued;
        public static EventData<PadConstructionProject, LCLaunchPad> OnPadConstructionCancel;
        public static EventData<PadConstructionProject, LCLaunchPad> OnPadConstructionComplete;
        public static EventData<LCLaunchPad> OnPadDismantled;
        public static EventData<LCConstructionProject, LaunchComplex> OnLCConstructionQueued;
        public static EventData<LCConstructionProject, LaunchComplex> OnLCConstructionCancel;
        public static EventData<LCConstructionProject, LaunchComplex> OnLCConstructionComplete;
        public static EventData<LaunchComplex> OnLCDismantled;
        public static EventVoid OnPersonnelChange;
        public static EventVoid OnRecalculateBuildRates;

        // Multiplier events
        // Using the first arg as a shared return value since these are voids.
        /// <summary>
        /// Rate, Node type, nodeID
        /// </summary>
        public static EventData<Boxed<double>, NodeType, string> ApplyResearchRateMultiplier;

        /// <summary>
        /// Rate, tags, resource amounts, part name
        /// </summary>
        public static EventData<Boxed<double>, IEnumerable<string>> ApplyPartEffectiveCostMultiplier;

        /// <summary>
        /// Rate, tags, resource amounts
        /// </summary>
        public static EventData<Boxed<double>, IEnumerable<string>, Dictionary<string, double>> ApplyGlobalEffectiveCostMultiplier;

        public void SubscribeToEvents()
        {
            // Fired via Tracking Station or via VesselRetrieval
            GameEvents.onVesselRecovered.Add(VesselRecoverEvent);

            // Global events
            GameEvents.onGameSceneLoadRequested.Add(GameSceneEvent);
            GameEvents.onGameStateLoad.Add(OnGameStateLoad);
            GameEvents.OnPartPurchased.Add(OnPartPurchased);
            GameEvents.Modifiers.OnCurrencyModified.Add(OnCurrenciesModified);

            // Flight
            GameEvents.onVesselSituationChange.Add(VesselSituationChange);
            
            // Editor
            GameEvents.onEditorShipModified.Add(ShipModifiedEvent);
            GameEvents.StageManager.OnGUIStageAdded.Add(StageCountChangedEvent);
            GameEvents.StageManager.OnGUIStageRemoved.Add(StageCountChangedEvent);
            GameEvents.StageManager.OnGUIStageSequenceModified.Add(StagingOrderChangedEvent);
            GameEvents.StageManager.OnPartUpdateStageability.Add(PartStageabilityChangedEvent);
            GameEvents.onEditorStarted.Add(OnEditorStarted);

            // Space Center
            GameEvents.OnKSCFacilityUpgraded.Add(FacilityUpgradedEvent);
            GameEvents.OnKSCStructureRepaired.Add(FaciliyRepaired);
            GameEvents.OnKSCStructureCollapsed.Add(FacilityDestroyed);

            // Space Center GUIs
            GameEvents.onGUIAdministrationFacilitySpawn.Add(EnterSCSubsceneAndHide);
            GameEvents.onGUIAstronautComplexSpawn.Add(EnterSCSubsceneAndHide);
            GameEvents.onGUIMissionControlSpawn.Add(EnterSCSubsceneAndHide);
            GameEvents.onGUIRnDComplexSpawn.Add(EnterSCSubsceneAndHide);
            GameEvents.onGUIKSPediaSpawn.Add(HideAllGUIs);

            GameEvents.onGUIAdministrationFacilityDespawn.Add(ExitSCSubsceneAndShow);
            GameEvents.onGUIAstronautComplexDespawn.Add(ExitSCSubsceneAndShow);
            GameEvents.onGUIMissionControlDespawn.Add(ExitSCSubsceneAndShow);
            GameEvents.onGUIRnDComplexDespawn.Add(ExitSCSubsceneAndShow);
            GameEvents.onGUIKSPediaDespawn.Add(RestoreAllGUIs);
        }

        private void OnEditorStarted()
        {
            KCTUtilities.HandleEditorButton();
        }

        public void CreateEvents()
        {
            OnVesselAddedToBuildQueue = new EventData<VesselProject>("OnKctVesselAddedToBuildQueue");
            OnTechQueued = new EventData<RDTech>("OnKctTechQueued");
            OnTechCompleted = new EventData<ResearchProject>("OnKctTechCompleted");
            OnFacilityUpgradeQueued = new EventData<FacilityUpgradeProject>("OnKctFacilityUpgradeQueued");
            OnFacilityUpgradeCancel = new EventData<FacilityUpgradeProject>("OnKctFacilityUpgradeCancel");
            OnFacilityUpgradeComplete = new EventData<FacilityUpgradeProject>("OnKctFacilityUpgradeComplete");
            OnPadConstructionQueued = new EventData<PadConstructionProject, LCLaunchPad>("OnKctPadConstructionQueued");
            OnPadConstructionCancel = new EventData<PadConstructionProject, LCLaunchPad>("OnKctPadConstructionCancel");
            OnPadConstructionComplete = new EventData<PadConstructionProject, LCLaunchPad>("OnKctPadConstructionComplete");
            OnPadDismantled = new EventData<LCLaunchPad>("OnKctPadDismantled");
            OnLCConstructionQueued = new EventData<LCConstructionProject, LaunchComplex>("OnKctLCConstructionQueued");
            OnLCConstructionCancel = new EventData<LCConstructionProject, LaunchComplex>("OnKctLCConstructionCancel");
            OnLCConstructionComplete = new EventData<LCConstructionProject, LaunchComplex>("OnKctLCConstructionComplete");
            OnLCDismantled = new EventData<LaunchComplex>("OnKctLCDismantled");
            OnPersonnelChange = new EventVoid("OnKctPesonnelChange");
            OnRecalculateBuildRates = new EventVoid("OnKctRecalculateBuildRates");

            ApplyResearchRateMultiplier = new EventData<Boxed<double>, NodeType, string>("ApplyResearchRateMultiplier");
            ApplyPartEffectiveCostMultiplier = new EventData<Boxed<double>, IEnumerable<string>>("ApplyPartEffectiveCostMultiplier");
            ApplyGlobalEffectiveCostMultiplier = new EventData<Boxed<double>, IEnumerable<string>, Dictionary<string, double>>("ApplyGlobalEffectiveCostMultiplier");
        }

        public void HideAllGUIs()
        {
            KCT_GUI.BackupUIState();
            KCT_GUI.ToggleVisibility(false);
        }

        private void RestoreAllGUIs()
        {
            KCT_GUI.RestorePrevUIState();
        }

        private void EnterSCSubscene()
        {
            KCT_GUI.EnterSCSubcene();
        }

        private void ExitSCSubscene()
        {
            KCT_GUI.ExitSCSubcene();
        }

        private void EnterSCSubsceneAndHide()
        {
            EnterSCSubscene();
            HideAllGUIs();
            UIHolder.Instance.HideIfShowing();
        }

        private void ExitSCSubsceneAndShow()
        {
            ExitSCSubscene();
            RestoreAllGUIs();
            UIHolder.Instance.ShowIfWasHidden();
        }


        public void FacilityUpgradedEvent(Upgradeables.UpgradeableFacility facility, int lvl)
        {
            if (KCT_GUI.IsPrimarilyDisabled) return;

            RP0Debug.Log($"Facility {facility.id} upgraded to lvl {lvl}");
            SpaceCenterManagement.Instance.RecalculateBuildRates();
        }

        public void FaciliyRepaired(DestructibleBuilding facility)
        {
            if (facility.id.Contains("LaunchPad"))
            {
                RP0Debug.Log("LaunchPad was repaired.");
                SpaceCenterManagement.Instance.ActiveSC.ActiveLC.ActiveLPInstance.RefreshDestructionNode();
                SpaceCenterManagement.Instance.ActiveSC.ActiveLC.ActiveLPInstance.CompletelyRepairNode();
            }
        }

        public void FacilityDestroyed(DestructibleBuilding facility)
        {
            if (facility.id.Contains("LaunchPad"))
            {
                RP0Debug.Log("LaunchPad was damaged.");
                SpaceCenterManagement.Instance.ActiveSC.ActiveLC.ActiveLPInstance.RefreshDestructionNode();
            }
        }

        private void OnCurrenciesModified(CurrencyModifierQuery query)
        {
            float changeDelta = query.GetTotal(Currency.Science);
            if (changeDelta == 0f) return;

            RP0Debug.Log($"Detected sci point change: {changeDelta}");
            KCTUtilities.ProcessSciPointTotalChange(changeDelta);
        }

        private void ShipModifiedEvent(ShipConstruct vessel)
        {
            SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
        }

        private void StageCountChangedEvent(int num)
        {
            SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
        }

        private void StagingOrderChangedEvent()
        {
            SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
        }

        private void PartStageabilityChangedEvent(Part p)
        {
            SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
        }

        public void GameSceneEvent(GameScenes scene)
        {
            KCT_GUI.PrevGUIStates.Clear();
            KCT_GUI.HideAll();

            if (scene == GameScenes.MAINMENU)
            {
                SpaceCenterManagement.Reset();
                KCTUtilities.DisableSimulationLocks();
                InputLockManager.RemoveControlLock(SpaceCenterManagement.KCTLaunchLock);

                if (PresetManager.Instance != null)
                {
                    PresetManager.Instance.ClearPresets();
                    PresetManager.Instance = null;
                }

                return;
            }

            if (PresetManager.PresetLoaded() && !PresetManager.Instance.ActivePreset.GeneralSettings.Enabled) return;
            var validScenes = new List<GameScenes> { GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.EDITOR };
            if (validScenes.Contains(scene))
            {
                if (KCTUtilities.SimulationSaveExists())
                {
                    KCTUtilities.LoadSimulationSave(false);
                }
            }

            if (HighLogic.LoadedScene == scene && scene == GameScenes.EDITOR)    //Fix for null reference when using new or load buttons in editor
            {
                GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                EditorLogic.fetch.Unlock("KCTEditorMouseLock");
            }
        }

        public void VesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> ev)
        {
            if (ev.from == Vessel.Situations.PRELAUNCH && ev.host == FlightGlobals.ActiveVessel)
            {
                KCT_Preset_General settings = PresetManager.Instance.ActivePreset.GeneralSettings;
                if (settings.Enabled && settings.BuildTimes)
                {
                    var dataModule = FlightGlobals.ActiveVessel.vesselModules.Find(vm => vm is KCTVesselTracker) as KCTVesselTracker;
                    if (dataModule != null && dataModule.Data.FacilityBuiltIn == EditorFacility.VAB && !dataModule.Data.HasStartedReconditioning)
                    {
                        string launchSite = FlightDriver.LaunchSiteName;
                        LaunchComplex lc = SpaceCenterManagement.Instance.FindLCFromID(dataModule.Data.LCID);
                        if (lc != null)
                        {
                            if (lc.LCType == LaunchComplexType.Pad && lc.ActiveLPInstance != null
                                && (launchSite == "LaunchPad" || lc.LaunchPads.Find(p => p.name == launchSite) == null))
                            {
                                launchSite = lc.ActiveLPInstance.name;
                            }
                            lc.Recon_Rollout.Add(new ReconRolloutProject(ev.host, ReconRolloutProject.RolloutReconType.Reconditioning, ev.host.id.ToString(), launchSite, lc));
                            dataModule.Data.HasStartedReconditioning = true;
                        }
                    }
                }
            }
        }

        public void VesselRecoverEvent(ProtoVessel v, bool quick)
        {
            if (!KCTUtilities.IsVesselKCTRecovering(v))
                return;

            RP0Debug.Log($"VesselRecoverEvent for {v.vesselName}");

            LaunchComplex targetLC = SpaceCenterManagement.Instance.RecoveredVessel.LC;
            if (targetLC == null)
                targetLC = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;

            targetLC.Warehouse.Add(SpaceCenterManagement.Instance.RecoveredVessel);
            targetLC.Recon_Rollout.Add(new ReconRolloutProject(SpaceCenterManagement.Instance.RecoveredVessel, ReconRolloutProject.RolloutReconType.Recovery, SpaceCenterManagement.Instance.RecoveredVessel.shipID.ToString()));
            SpaceCenterManagement.Instance.RecoveredVessel = new VesselProject();
        }

        public void OnExitAdmin()
        {
            GameEvents.onGUIAdministrationFacilityDespawn.Remove(OnExitAdmin);
            InputLockManager.RemoveControlLock("administrationFacility");
        }

        public void OnExitMC()
        {
            GameEvents.onGUIMissionControlDespawn.Remove(OnExitMC);
            InputLockManager.RemoveControlLock("administrationFacility");
        }

        public void OnGameStateLoad(ConfigNode config)
        {
            // Run this after all scenariomodules have loaded (i.e. both us *and* RnD)
            KCTUtilities.AddResearchedPartsToExperimental();
        }

        public void OnPartPurchased(AvailablePart ap)
        {
            KCTUtilities.RemoveExperimentalPart(ap);
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
