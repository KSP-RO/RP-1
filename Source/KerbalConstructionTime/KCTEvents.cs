using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalConstructionTime
{
    public class KCTEvents
    {
        public static KCTEvents Instance { get; private set; } = new KCTEvents();
        public static bool AllowedToUpgrade = false;

        public bool SubscribedToEvents { get; private set; }
        public bool CreatedEvents { get; private set; }
        public bool KCTButtonStockImportant { get; set; }

        public static EventData<BuildListVessel> OnVesselAddedToBuildQueue;
        public static EventData<RDTech> OnTechQueued;
        public static EventData<TechItem> OnTechCompleted;
        public static EventData<FacilityUpgrade> OnFacilityUpgradeQueued;
        public static EventData<FacilityUpgrade> OnFacilityUpgradeComplete;
        public static EventData<PadConstruction, KCT_LaunchPad> OnPadConstructionQueued;
        public static EventData<PadConstruction, KCT_LaunchPad> OnPadConstructionComplete;
        public static EventData<LCConstruction, LCItem> OnLCConstructionQueued;
        public static EventData<LCConstruction, LCItem> OnLCConstructionComplete;


        public KCTEvents()
        {
            KCTDebug.Log("KCT_Events constructor");
            SubscribedToEvents = false;
            CreatedEvents = false;
        }

        public void SubscribeToEvents()
        {
            GameEvents.onGUILaunchScreenSpawn.Add(LaunchScreenOpenEvent);
            GameEvents.onVesselRecovered.Add(VesselRecoverEvent);

            GameEvents.onVesselSituationChange.Add(VesselSituationChange);
            GameEvents.onGameSceneLoadRequested.Add(GameSceneEvent);
            GameEvents.OnTechnologyResearched.Add(TechUnlockEvent);
            GameEvents.onEditorShipModified.Add(ShipModifiedEvent);
            GameEvents.onEditorShowPartList.Add(PartListEvent);
            KSP.UI.BaseCrewAssignmentDialog.onCrewDialogChange.Add(CrewDialogChange);
            GameEvents.OnPartPurchased.Add(PartPurchasedEvent);
            GameEvents.onGUIRnDComplexSpawn.Add(TechEnableEvent);
            GameEvents.onGUIRnDComplexDespawn.Add(TechDisableEvent);
            GameEvents.OnKSCFacilityUpgraded.Add(FacilityUpgradedEvent);

            GameEvents.onGUIEngineersReportReady.Add(EngineersReportReady);

            GameEvents.OnKSCStructureRepaired.Add(FaciliyRepaired);
            GameEvents.OnKSCStructureCollapsed.Add(FacilityDestroyed);

            GameEvents.Modifiers.OnCurrencyModified.Add(OnCurrenciesModified);

            GameEvents.StageManager.OnGUIStageAdded.Add(StageCountChangedEvent);
            GameEvents.StageManager.OnGUIStageRemoved.Add(StageCountChangedEvent);
            GameEvents.StageManager.OnGUIStageSequenceModified.Add(StagingOrderChangedEvent);
            GameEvents.StageManager.OnPartUpdateStageability.Add(PartStageabilityChangedEvent);

            GameEvents.FindEvent<EventVoid>("OnSYInventoryAppliedToVessel")?.Add(SYInventoryApplied);
            GameEvents.FindEvent<EventVoid>("OnSYReady")?.Add(SYReady);
            GameEvents.FindEvent<EventData<Part>>("OnSYInventoryAppliedToPart")?.Add(OnSYInventoryAppliedToPart);

            GameEvents.onGUIAdministrationFacilitySpawn.Add(HideAllGUIs);
            GameEvents.onGUIAstronautComplexSpawn.Add(HideAllGUIs);
            GameEvents.onGUIMissionControlSpawn.Add(HideAllGUIs);
            GameEvents.onGUIRnDComplexSpawn.Add(HideAllGUIs);
            GameEvents.onGUIKSPediaSpawn.Add(HideAllGUIs);

            GameEvents.onGUIAdministrationFacilityDespawn.Add(RestoreAllGUIs);
            GameEvents.onGUIAstronautComplexDespawn.Add(RestoreAllGUIs);
            GameEvents.onGUIMissionControlDespawn.Add(RestoreAllGUIs);
            GameEvents.onGUIRnDComplexDespawn.Add(RestoreAllGUIs);
            GameEvents.onGUIKSPediaDespawn.Add(RestoreAllGUIs);

            GameEvents.onEditorStarted.Add(OnEditorStarted);
            GameEvents.onFacilityContextMenuSpawn.Add(FacilityContextMenuSpawn);

            SubscribedToEvents = true;
        }

        private void OnSYInventoryAppliedToPart(Part p)
        {
            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
        }

        private void OnEditorStarted()
        {
            Utilities.HandleEditorButton();
            KerbalConstructionTime.Instance.ERClobberer.EditorStarted();
        }

        public void CreateEvents()
        {
            OnVesselAddedToBuildQueue = new EventData<BuildListVessel>("OnKctVesselAddedToBuildQueue");
            OnTechQueued = new EventData<RDTech>("OnKctTechQueued");
            OnTechCompleted = new EventData<TechItem>("OnKctTechCompleted");
            OnFacilityUpgradeQueued = new EventData<FacilityUpgrade>("OnKctFacilityUpgradeQueued");
            OnFacilityUpgradeComplete = new EventData<FacilityUpgrade>("OnKctFacilityUpgradeComplete");
            OnPadConstructionQueued = new EventData<PadConstruction, KCT_LaunchPad>("OnKctPadConstructionQueued");
            OnPadConstructionComplete = new EventData<PadConstruction, KCT_LaunchPad>("OnKctPadConstructionComplete");
            OnLCConstructionQueued = new EventData<LCConstruction, LCItem>("OnKctLCConstructionQueued");
            OnLCConstructionComplete = new EventData<LCConstruction, LCItem>("OnKctLCConstructionComplete");
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

        public void FacilityUpgradedEvent(Upgradeables.UpgradeableFacility facility, int lvl)
        {
            if (KCT_GUI.IsPrimarilyDisabled) return;

            KCTDebug.Log($"Facility {facility.id} upgraded to lvl {lvl}");
            AllowedToUpgrade = false;
            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                ksc.RecalculateBuildRates();
            }
            for (int i = KCTGameStates.TechList.Count - 1; i >= 0; i--)
            {
                TechItem tech = KCTGameStates.TechList[i];
                tech.UpdateBuildRate(KCTGameStates.TechList.IndexOf(tech));
            }
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

        public void FacilityContextMenuSpawn(KSCFacilityContextMenu menu)
        {
            KerbalConstructionTime.Instance.OnFacilityContextMenuSpawn(menu);
        }

        private void SYInventoryApplied()
        {
            KCTDebug.Log("Inventory was applied. Recalculating.");
            if (HighLogic.LoadedSceneIsEditor)
            {
                KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
            }
        }

        private void SYReady()
        {
            if (HighLogic.LoadedSceneIsEditor && KCTGameStates.EditorShipEditingMode && KCTGameStates.EditedVessel != null)
            {
                KCTDebug.Log("Removing SY tracking of this vessel.");
                string id = ScrapYardWrapper.GetPartID(KCTGameStates.EditedVessel.ExtractedPartNodes[0]);
                ScrapYardWrapper.SetProcessedStatus(id, false);

                KCTDebug.Log("Adding parts back to inventory for editing...");
                foreach (ConfigNode partNode in KCTGameStates.EditedVessel.ExtractedPartNodes)
                {
                    if (ScrapYardWrapper.PartIsFromInventory(partNode))
                    {
                        ScrapYardWrapper.AddPartToInventory(partNode, false);
                    }
                }
            }
        }

        private void ShipModifiedEvent(ShipConstruct vessel)
        {
            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
            KerbalConstructionTime.Instance.ERClobberer.StartClobberingCoroutine();
        }

        private void PartListEvent()
        {
            KerbalConstructionTime.Instance.ERClobberer.StartClobberingCoroutine();
        }

        private void CrewDialogChange(VesselCrewManifest vcm)
        {
            KerbalConstructionTime.Instance.ERClobberer.StartClobberingCoroutine();
        }

        private void EngineersReportReady()
        {
            KerbalConstructionTime.Instance.ERClobberer.BindToEngineersReport();
        }

        private void StageCountChangedEvent(int num)
        {
            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
            KerbalConstructionTime.Instance.ERClobberer.StartClobberingCoroutine();
        }

        private void StagingOrderChangedEvent()
        {
            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
            KerbalConstructionTime.Instance.ERClobberer.StartClobberingCoroutine();
        }

        private void PartStageabilityChangedEvent(Part p)
        {
            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
            KerbalConstructionTime.Instance.ERClobberer.StartClobberingCoroutine();
        }

        public void PartPurchasedEvent(AvailablePart part)
        {
            if (HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
                return;
            TechItem tech = KCTGameStates.TechList.OfType<TechItem>().FirstOrDefault(t => t.TechID == part.TechRequired);
            if (tech != null && tech.IsInList())
            {
                ScreenMessages.PostScreenMessage("You must wait until the node is fully researched to purchase parts!", 4f, ScreenMessageStyle.UPPER_LEFT);
                if (part.costsFunds)
                {
                    Utilities.AddFunds(part.entryCost, TransactionReasons.RnDPartPurchase);
                }
                tech.ProtoNode.partsPurchased.Remove(part);
                tech.DisableTech();
            }
            else
            {
                Utilities.RemoveExperimentalPart(part);
            }
        }

        public void TechUnlockEvent(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> ev)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled) return;
            if (ev.target == RDTech.OperationResult.Successful)
            {
                var tech = new TechItem();
                if (ev.host != null)
                    tech = new TechItem(ev.host);

                if (!tech.IsInList())
                {
                    if (PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes && PresetManager.Instance.ActivePreset.GeneralSettings.BuildTimes)
                    {
                        KCTGameStates.TechList.Add(tech);
                        foreach (TechItem techItem in KCTGameStates.TechList)
                            techItem.UpdateBuildRate(KCTGameStates.TechList.IndexOf(techItem));
                        double timeLeft = tech.BuildRate > 0 ? tech.TimeLeft : tech.EstimatedTimeLeft;
                        ScreenMessages.PostScreenMessage($"Node will unlock in {MagiCore.Utilities.GetFormattedTime(timeLeft)}", 4f, ScreenMessageStyle.UPPER_LEFT);

                        OnTechQueued.Fire(ev.host);
                    }
                }
                else
                {
                    ResearchAndDevelopment.Instance.AddScience(tech.ScienceCost, TransactionReasons.RnDTechResearch);
                    ScreenMessages.PostScreenMessage("This node is already being researched!", 4f, ScreenMessageStyle.UPPER_LEFT);
                    ScreenMessages.PostScreenMessage($"It will unlock in {MagiCore.Utilities.GetFormattedTime((KCTGameStates.TechList.First(t => t.TechID == ev.host.techID)).TimeLeft)}", 4f, ScreenMessageStyle.UPPER_LEFT);
                }
            }
        }

        public void TechDisableEvent()
        {
            TechDisableEventFinal(true);

            Utilities.AddResearchedPartsToExperimental();
        }

        public void TechEnableEvent()
        {
            if (PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes && PresetManager.Instance.ActivePreset.GeneralSettings.BuildTimes)
            {
                foreach (TechItem techItem in KCTGameStates.TechList)
                    techItem.EnableTech();
            }

            Utilities.RemoveResearchedPartsFromExperimental();
        }

        public void TechDisableEventFinal(bool save = false)
        {
            if (PresetManager.Instance != null && PresetManager.Instance.ActivePreset != null &&
                PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes && PresetManager.Instance.ActivePreset.GeneralSettings.BuildTimes)
            {
                foreach (TechItem tech in KCTGameStates.TechList)
                {

                    tech.DisableTech();
                }

                //Need to somehow update the R&D instance
                if (save)
                {
                    GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                }
            }
        }

        public void GameSceneEvent(GameScenes scene)
        {
            KCT_GUI.HideAll();
            KCTGameStates.SimulationParams.IsVesselMoved = false;

            if (scene == GameScenes.MAINMENU)
            {
                KCTGameStates.Reset();
                KCTGameStates.IsFirstStart = false;
                Utilities.DisableSimulationLocks();
                InputLockManager.RemoveControlLock(KerbalConstructionTime.KCTLaunchLock);
                KCTGameStates.ActiveKSCName = Utilities._defaultKscId;
                KCTGameStates.ActiveKSC = new KSCItem(Utilities._defaultKscId);
                KCTGameStates.KSCs = new List<KSCItem>() { KCTGameStates.ActiveKSC };

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
                TechDisableEventFinal();
            }

            if (HighLogic.LoadedScene == scene && scene == GameScenes.EDITOR)    //Fix for null reference when using new or load buttons in editor
            {
                GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                EditorLogic.fetch.Unlock("KCTEditorMouseLock");
            }

            if (scene == GameScenes.EDITOR && !HighLogic.LoadedSceneIsEditor)
                KCT_GUI.FirstOnGUIUpdate = true;
        }

        public void LaunchScreenOpenEvent(GameEvents.VesselSpawnInfo v)
        {
            if (!KCT_GUI.IsPrimarilyDisabled)
            {
                // PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "Warning!", "To launch vessels you must first build them in the VAB or SPH, then launch them through the main KCT window in the Space Center!", "Ok", false, HighLogic.UISkin);
                //open the build list to the right page
                string selection = v.craftSubfolder.Contains("SPH") ? "SPH" : "VAB";
                KCT_GUI.ToggleVisibility(true);
                KCT_GUI.SelectList("");
                KCT_GUI.SelectList("Vessels");
                KCTDebug.Log($"Opening the GUI to the {selection}");
            }
        }

        public void VesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> ev)
        {
            if (ev.from == Vessel.Situations.PRELAUNCH && ev.host == FlightGlobals.ActiveVessel)
            {
                if (PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                {
                    if (HighLogic.CurrentGame.editorFacility == EditorFacility.VAB)
                    {
                        string launchSite = FlightDriver.LaunchSiteName;
                        if (launchSite == "LaunchPad") launchSite = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.ActiveLPInstance.name;
                        KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.Recon_Rollout.Add(new ReconRollout(ev.host, ReconRollout.RolloutReconType.Reconditioning, ev.host.id.ToString(), launchSite));
                    }
                }
            }
        }

        public void VesselRecoverEvent(ProtoVessel v, bool unknownAsOfNow)
        {
            KCTDebug.Log($"VesselRecoverEvent for {v.vesselName}");
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
            {
                //KCTDebug.LogError("Disabled!");
                return;
            }
            if (KCTGameStates.IsSimulatedFlight)
            {
                //KCTDebug.LogError("Sim!");
                return;
            }

            if (v.vesselRef.isEVA)
            {
                //KCTDebug.LogError("Is eva!");
                return;
            }
            if (KCTGameStates.RecoveredVessel == null)
            {
                //KCTDebug.LogError("Recovered vessel is null!");
                return;
            }
            if (v.vesselName != KCTGameStates.RecoveredVessel.ShipName)
            {
                //KCTDebug.LogError("Recovered vessel, " + KCTGameStates.RecoveredVessel.ShipName +", doesn't match!");
                return;
            }
            //rebuy the ship if ScrapYard isn't overriding funds
            if (!ScrapYardWrapper.OverrideFunds)
            {
                Utilities.SpendFunds(KCTGameStates.RecoveredVessel.Cost, TransactionReasons.VesselRollout);    //pay for the ship again
            }

            //pull all of the parts out of the inventory
            //This is a bit funky since we grab the part id from our part, grab the inventory part out, then try to reapply that ontop of our part
            if (ScrapYardWrapper.Available)
            {
                foreach (ConfigNode partNode in KCTGameStates.RecoveredVessel.ExtractedPartNodes)
                {
                    string id = ScrapYardWrapper.GetPartID(partNode);
                    ConfigNode inventoryVersion = ScrapYardWrapper.FindInventoryPart(id);
                    if (inventoryVersion != null)
                    {
                        //apply it to our copy of the part
                        ConfigNode ourTracker = partNode.GetNodes("MODULE").FirstOrDefault(n => string.Equals(n.GetValue("name"), "ModuleSYPartTracker", StringComparison.Ordinal));
                        if (ourTracker != null)
                        {
                            ourTracker.SetValue("TimesRecovered", inventoryVersion.GetValue("_timesRecovered"));
                            ourTracker.SetValue("Inventoried", inventoryVersion.GetValue("_inventoried"));
                        }
                    }
                }

                //process the vessel in ScrapYard
                ScrapYardWrapper.ProcessVessel(KCTGameStates.RecoveredVessel.ExtractedPartNodes);

                //reset the BP
                KCTGameStates.RecoveredVessel.BuildPoints = Utilities.GetBuildPoints(KCTGameStates.RecoveredVessel.ExtractedPartNodes);
                KCTGameStates.RecoveredVessel.IntegrationPoints = MathParser.ParseIntegrationTimeFormula(KCTGameStates.RecoveredVessel);
            }

            LCItem targetLC = KCTGameStates.RecoveredVessel.LC;
            if (targetLC == null)
            {
                if (KCTGameStates.RecoveredVessel.Type == BuildListVessel.ListType.VAB)
                {
                    double mass = KCTGameStates.RecoveredVessel.GetTotalMass();
                    if (KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.MassMax >= mass &&
                        KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.MassMin <= mass)
                        targetLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
                    else
                    {
                        targetLC = KCTGameStates.ActiveKSC.LaunchComplexes.FirstOrDefault(lc => lc.IsOperational && lc.IsPad && lc.MassMax >= mass && lc.MassMin <= mass);

                        // If we still can't find a match, just recover it to the current launch complex.
                        if (targetLC == null)
                            targetLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
                    }
                }
                else
                {
                    targetLC = KCTGameStates.ActiveKSC.Hangar;
                }

                KCTGameStates.RecoveredVessel.LC = targetLC;
            }

            targetLC.Warehouse.Add(KCTGameStates.RecoveredVessel);
            targetLC.Recon_Rollout.Add(new ReconRollout(KCTGameStates.RecoveredVessel, ReconRollout.RolloutReconType.Recovery, KCTGameStates.RecoveredVessel.Id.ToString()));
            KCTGameStates.RecoveredVessel = null;
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
