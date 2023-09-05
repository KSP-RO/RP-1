using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UniLinq;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class KCTEvents
    {
        public static KCTEvents Instance { get; private set; } = new KCTEvents();

        public bool SubscribedToEvents { get; private set; }
        public bool CreatedEvents { get; private set; }
        public bool KCTButtonStockImportant { get; set; }

        public static EventData<BuildListVessel> OnVesselAddedToBuildQueue;
        public static EventData<RDTech> OnTechQueued;
        public static EventData<TechItem> OnTechCompleted;
        public static EventData<FacilityUpgrade> OnFacilityUpgradeQueued;
        public static EventData<FacilityUpgrade> OnFacilityUpgradeCancel;
        public static EventData<FacilityUpgrade> OnFacilityUpgradeComplete;
        public static EventData<PadConstruction, KCT_LaunchPad> OnPadConstructionQueued;
        public static EventData<PadConstruction, KCT_LaunchPad> OnPadConstructionCancel;
        public static EventData<PadConstruction, KCT_LaunchPad> OnPadConstructionComplete;
        public static EventData<KCT_LaunchPad> OnPadDismantled;
        public static EventData<LCConstruction, LCItem> OnLCConstructionQueued;
        public static EventData<LCConstruction, LCItem> OnLCConstructionCancel;
        public static EventData<LCConstruction, LCItem> OnLCConstructionComplete;
        public static EventData<LCItem> OnLCDismantled;
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

        public KCTEvents()
        {
            KCTDebug.Log("KCT_Events constructor");
            SubscribedToEvents = false;
            CreatedEvents = false;
        }

        public void SubscribeToEvents()
        {
            // Fired via Tracking Station or via VesselRetrieval
            GameEvents.onVesselRecovered.Add(VesselRecoverEvent);

            // Global events
            GameEvents.onGameSceneLoadRequested.Add(GameSceneEvent);
            GameEvents.onGameStateLoad.Add(OnGameStateLoad);
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

            SubscribedToEvents = true;
        }

        private void OnEditorStarted()
        {
            Utilities.HandleEditorButton();
        }

        public void CreateEvents()
        {
            OnVesselAddedToBuildQueue = new EventData<BuildListVessel>("OnKctVesselAddedToBuildQueue");
            OnTechQueued = new EventData<RDTech>("OnKctTechQueued");
            OnTechCompleted = new EventData<TechItem>("OnKctTechCompleted");
            OnFacilityUpgradeQueued = new EventData<FacilityUpgrade>("OnKctFacilityUpgradeQueued");
            OnFacilityUpgradeCancel = new EventData<FacilityUpgrade>("OnKctFacilityUpgradeCancel");
            OnFacilityUpgradeComplete = new EventData<FacilityUpgrade>("OnKctFacilityUpgradeComplete");
            OnPadConstructionQueued = new EventData<PadConstruction, KCT_LaunchPad>("OnKctPadConstructionQueued");
            OnPadConstructionCancel = new EventData<PadConstruction, KCT_LaunchPad>("OnKctPadConstructionCancel");
            OnPadConstructionComplete = new EventData<PadConstruction, KCT_LaunchPad>("OnKctPadConstructionComplete");
            OnPadDismantled = new EventData<KCT_LaunchPad>("OnKctPadDismantled");
            OnLCConstructionQueued = new EventData<LCConstruction, LCItem>("OnKctLCConstructionQueued");
            OnLCConstructionCancel = new EventData<LCConstruction, LCItem>("OnKctLCConstructionCancel");
            OnLCConstructionComplete = new EventData<LCConstruction, LCItem>("OnKctLCConstructionComplete");
            OnLCDismantled = new EventData<LCItem>("OnKctLCDismantled");
            OnPersonnelChange = new EventVoid("OnKctPesonnelChange");
            OnRecalculateBuildRates = new EventVoid("OnKctRecalculateBuildRates");

            ApplyResearchRateMultiplier = new EventData<Boxed<double>, NodeType, string>("ApplyResearchRateMultiplier");
            ApplyPartEffectiveCostMultiplier = new EventData<Boxed<double>, IEnumerable<string>>("ApplyPartEffectiveCostMultiplier");
            ApplyGlobalEffectiveCostMultiplier = new EventData<Boxed<double>, IEnumerable<string>, Dictionary<string, double>>("ApplyGlobalEffectiveCostMultiplier");

            CreatedEvents = true;
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
            RP0.UIHolder.Instance.HideIfShowing();
        }

        private void ExitSCSubsceneAndShow()
        {
            ExitSCSubscene();
            RestoreAllGUIs();
            RP0.UIHolder.Instance.ShowIfWasHidden();
        }


        public void FacilityUpgradedEvent(Upgradeables.UpgradeableFacility facility, int lvl)
        {
            if (KCT_GUI.IsPrimarilyDisabled) return;

            KCTDebug.Log($"Facility {facility.id} upgraded to lvl {lvl}");
            KCTGameStates.RecalculateBuildRates();
        }

        public void FaciliyRepaired(DestructibleBuilding facility)
        {
            if (facility.id.Contains("LaunchPad"))
            {
                KCTDebug.Log("LaunchPad was repaired.");
                KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.ActiveLPInstance.RefreshDestructionNode();
                KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.ActiveLPInstance.CompletelyRepairNode();
            }
        }

        public void FacilityDestroyed(DestructibleBuilding facility)
        {
            if (facility.id.Contains("LaunchPad"))
            {
                KCTDebug.Log("LaunchPad was damaged.");
                KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.ActiveLPInstance.RefreshDestructionNode();
            }
        }

        private void OnCurrenciesModified(CurrencyModifierQuery query)
        {
            float changeDelta = query.GetTotal(Currency.Science);
            if (changeDelta == 0f) return;

            KCTDebug.Log($"Detected sci point change: {changeDelta}");
            Utilities.ProcessSciPointTotalChange(changeDelta);
        }

        private void ShipModifiedEvent(ShipConstruct vessel)
        {
            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
        }

        private void StageCountChangedEvent(int num)
        {
            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
        }

        private void StagingOrderChangedEvent()
        {
            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
        }

        private void PartStageabilityChangedEvent(Part p)
        {
            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
        }

        public void GameSceneEvent(GameScenes scene)
        {
            KCT_GUI.HideAll();

            if (scene == GameScenes.MAINMENU)
            {
                KCTGameStates.Reset();
                KCTGameStates.IsFirstStart = false;
                Utilities.DisableSimulationLocks();
                InputLockManager.RemoveControlLock(KerbalConstructionTime.KCTLaunchLock);

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
                if (Utilities.SimulationSaveExists())
                {
                    Utilities.LoadSimulationSave(false);
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
                if (PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                {
                    var dataModule = FlightGlobals.ActiveVessel.vesselModules.Find(vm => vm is KCTVesselTracker) as KCTVesselTracker;
                    if (dataModule != null && dataModule.Data.FacilityBuiltIn == EditorFacility.VAB && !dataModule.Data.HasStartedReconditioning)
                    {
                        string launchSite = FlightDriver.LaunchSiteName;
                        LCItem lc = KCTGameStates.FindLCFromID(dataModule.Data.LCID);
                        if (lc != null)
                        {
                            if (lc.LCType == LaunchComplexType.Pad && lc.ActiveLPInstance != null
                                && (launchSite == "LaunchPad" || lc.LaunchPads.Find(p => p.name == launchSite) == null))
                            {
                                launchSite = lc.ActiveLPInstance.name;
                            }
                            lc.Recon_Rollout.Add(new ReconRollout(ev.host, ReconRollout.RolloutReconType.Reconditioning, ev.host.id.ToString(), launchSite, lc));
                            dataModule.Data.HasStartedReconditioning = true;
                        }
                    }
                }
            }
        }

        public void VesselRecoverEvent(ProtoVessel v, bool quick)
        {
            if (!Utilities.IsVesselKCTRecovering(v))
                return;

            KCTDebug.Log($"VesselRecoverEvent for {v.vesselName}");

            LCItem targetLC = KerbalConstructionTimeData.Instance.RecoveredVessel.LC;
            if (targetLC == null)
                targetLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

            targetLC.Warehouse.Add(KerbalConstructionTimeData.Instance.RecoveredVessel);
            targetLC.Recon_Rollout.Add(new ReconRollout(KerbalConstructionTimeData.Instance.RecoveredVessel, ReconRollout.RolloutReconType.Recovery, KerbalConstructionTimeData.Instance.RecoveredVessel.shipID.ToString()));
            KerbalConstructionTimeData.Instance.RecoveredVessel = new BuildListVessel();
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
            Utilities.AddResearchedPartsToExperimental();
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
