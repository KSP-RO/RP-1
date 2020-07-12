using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToolbarControl_NS;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class KerbalConstructionTime : MonoBehaviour
    {
        public static KerbalConstructionTime Instance { get; private set; }

        public bool IsEditorRecalcuationRequired;

        private static int _lvlCheckTimer = 0;
        private static bool _ratesUpdated = false;
        private static bool _isGUIInitialized = false;

        private WaitForSeconds _wfsHalf = null, _wfsOne = null, _wfsTwo = null;
        private bool _isIconUpdated = false;

        internal void OnFacilityContextMenuSpawn(KSCFacilityContextMenu menu)
        {
            if (KCT_GUI.IsPrimarilyDisabled) return;

            var overrider = new KSCContextMenuOverrider(menu);
            StartCoroutine(overrider.OnContextMenuSpawn());
        }

        public void OnDestroy()
        {
            if (KCTGameStates.ToolbarControl != null)
            {
                KCTGameStates.ToolbarControl.OnDestroy();
                Destroy(KCTGameStates.ToolbarControl);
            }
            KCT_GUI.GuiDataSaver.Save();
        }

        internal void OnGUI()
        {
            if (Utilities.CurrentGameIsMission()) return;

            if (!_isGUIInitialized)
            {
                KCT_GUI.InitBuildListVars();
                KCT_GUI.InitBuildPlans();
                _isGUIInitialized = true;
            }
            KCT_GUI.SetGUIPositions();
        }

        public void Awake()
        {
            if (Utilities.CurrentGameIsMission()) return;

            KCTDebug.Log("Awake called");
            KCTGameStates.ErroredDuringOnLoad.OnLoadStart();
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

            var obj = new GameObject();
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
            if (Utilities.CurrentGameIsMission()) return;

            // Subscribe to events from KSP and other mods
            if (!KCTEvents.Instance.SubscribedToEvents)
            {
                KCTEvents.Instance.SubscribeToEvents();
            }

            if (KCTGameStates.IsFirstStart)
                PresetManager.Instance.SaveActiveToSaveData();

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
                InputLockManager.GetControlLock("KCTLaunchLock") == ControlTypes.EDITOR_LAUNCH)
            {
                InputLockManager.RemoveControlLock("KCTLaunchLock");
            }

            KACWrapper.InitKACWrapper();

            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
            {
                if (InputLockManager.GetControlLock("KCTKSCLock") == ControlTypes.KSC_FACILITIES)
                    InputLockManager.RemoveControlLock("KCTKSCLock");
                return;
            }

            //Begin primary mod functions

            KCTGameStates.UT = Planetarium.GetUniversalTime();

            KCT_GUI.GuiDataSaver.Load();
            KCT_GUI.GUIStates.HideAllNonMainWindows();

            switch (HighLogic.LoadedScene)
            {
                case GameScenes.EDITOR:
                    KCT_GUI.HideAll();
                    if (!KCT_GUI.IsPrimarilyDisabled)
                    {
                        KCT_GUI.GUIStates.ShowEditorGUI = KCTGameStates.ShowWindows[1];
                        if (KCT_GUI.GUIStates.ShowEditorGUI)
                            KCT_GUI.ToggleVisibility(true);
                        else
                            KCT_GUI.ToggleVisibility(false);
                    }
                    break;
                case GameScenes.SPACECENTER:
                    bool shouldStart = KCT_GUI.GUIStates.ShowFirstRun;
                    KCT_GUI.HideAll();
                    if (!shouldStart)
                    {
                        KCT_GUI.GUIStates.ShowBuildList = KCTGameStates.ShowWindows[0];
                        if (KCT_GUI.GUIStates.ShowBuildList)
                            KCT_GUI.ToggleVisibility(true);
                        else
                            KCT_GUI.ToggleVisibility(false);
                    }
                    KCT_GUI.GUIStates.ShowFirstRun = shouldStart;
                    break;
                case GameScenes.FLIGHT:
                    if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH &&
                        FlightGlobals.ActiveVessel.GetCrewCount() == 0 && KCTGameStates.LaunchedCrew.Count > 0)
                    {
                        KerbalRoster roster = HighLogic.CurrentGame.CrewRoster;

                        for (int i = 0; i < FlightGlobals.ActiveVessel.parts.Count; i++)
                        {
                            Part p = FlightGlobals.ActiveVessel.parts[i];
                            KCTDebug.LogError("Part being tested: " + p.partInfo.title);
                            {
                                CrewedPart cP = KCTGameStates.LaunchedCrew.Find(part => part.PartID == p.craftID);
                                if (cP == null) continue;
                                List<ProtoCrewMember> crewList = cP.CrewList;
                                KCTDebug.LogError("cP.crewList.Count: " + cP.CrewList.Count);
                                foreach (ProtoCrewMember crewMember in crewList)
                                {
                                    if (crewMember != null)
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
                                        if (finalCrewMember == null)
                                        {
                                            KCTDebug.LogError("Error when assigning " + crewMember.name + " to " + p.partInfo.name + ". Cannot find Kerbal in list.");
                                            continue;
                                        }
                                        try
                                        {
                                            KCTDebug.Log("Assigning " + finalCrewMember.name + " to " + p.partInfo.name);
                                            if (p.AddCrewmember(finalCrewMember))
                                            {
                                                finalCrewMember.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
                                                if (finalCrewMember.seat != null)
                                                    finalCrewMember.seat.SpawnCrew();
                                            }
                                            else
                                            {
                                                KCTDebug.LogError("Error when assigning " + crewMember.name + " to " + p.partInfo.name);
                                                finalCrewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                                                continue;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            KCTDebug.LogError($"Error when assigning {crewMember.name} to {p.partInfo.name}: {ex}");
                                            finalCrewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                        KCTGameStates.LaunchedCrew.Clear();
                    }

                    KCT_GUI.HideAll();
                    if (KCTGameStates.LaunchedVessel != null && FlightGlobals.ActiveVessel?.situation == Vessel.Situations.PRELAUNCH)
                    {
                        KCTGameStates.LaunchedVessel.KSC = null; //it's invalid now
                        KCTDebug.Log("Attempting to remove launched vessel from build list");
                        bool removed = KCTGameStates.LaunchedVessel.RemoveFromBuildList();
                        if (removed) //Only do these when the vessel is first removed from the list
                        {
                            //Add the cost of the ship to the funds so it can be removed again by KSP
                            Utilities.AddFunds(KCTGameStates.LaunchedVessel.Cost, TransactionReasons.VesselRollout);
                            FlightGlobals.ActiveVessel.vesselName = KCTGameStates.LaunchedVessel.ShipName;
                        }

                        ReconRollout rollout = KCTGameStates.ActiveKSC.Recon_Rollout.FirstOrDefault(r => r.AssociatedID == KCTGameStates.LaunchedVessel.Id.ToString());
                        if (rollout != null)
                            KCTGameStates.ActiveKSC.Recon_Rollout.Remove(rollout);

                        AirlaunchPrep alPrep = KCTGameStates.ActiveKSC.AirlaunchPrep.FirstOrDefault(r => r.AssociatedID == KCTGameStates.LaunchedVessel.Id.ToString());
                        if (alPrep != null)
                            KCTGameStates.ActiveKSC.AirlaunchPrep.Remove(alPrep);

                        AirlaunchParams alParams = KCTGameStates.AirlaunchParams;
                        if (alParams != null && alParams.KCTVesselId == KCTGameStates.LaunchedVessel.Id &&
                            (!alParams.KSPVesselId.HasValue || alParams.KSPVesselId == FlightGlobals.ActiveVessel.id))
                        {
                            if (!alParams.KSPVesselId.HasValue) alParams.KSPVesselId = FlightGlobals.ActiveVessel.id;
                            StartCoroutine(AirlaunchRoutine(alParams, FlightGlobals.ActiveVessel.id));
                        }
                    }
                    break;
            }

            _ratesUpdated = false;
            KCTDebug.Log("Start finished");

            _wfsOne = new WaitForSeconds(1f);
            _wfsTwo = new WaitForSeconds(2f);
            _wfsHalf = new WaitForSeconds(0.5f);

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

        public void FixedUpdate()
        {
            if (Utilities.CurrentGameIsMission()) return;

            double lastUT = KCTGameStates.UT > 0 ? KCTGameStates.UT : Planetarium.GetUniversalTime();
            KCTGameStates.UT = Planetarium.GetUniversalTime();
            try
            {
                if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                    return;

                if (!KCTGameStates.ErroredDuringOnLoad.AlertFired && KCTGameStates.ErroredDuringOnLoad.HasErrored())
                {
                    KCTGameStates.ErroredDuringOnLoad.FireAlert();
                }

                if (KCTGameStates.UpdateLaunchpadDestructionState)
                {
                    KCTDebug.Log("Updating launchpad destruction state.");
                    KCTGameStates.UpdateLaunchpadDestructionState = false;
                    KCTGameStates.ActiveKSC.ActiveLPInstance.SetDestructibleStateFromNode();
                    if (KCTGameStates.ActiveKSC.ActiveLPInstance.upgradeRepair)
                    {
                        //repair everything, then update the node
                        KCTGameStates.ActiveKSC.ActiveLPInstance.RefreshDestructionNode();
                        KCTGameStates.ActiveKSC.ActiveLPInstance.CompletelyRepairNode();
                        KCTGameStates.ActiveKSC.ActiveLPInstance.SetDestructibleStateFromNode();
                    }

                }

                UpdateBuildRates();
                UpdateActiveLPLevel();

                if (!KCT_GUI.IsPrimarilyDisabled && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION))
                {
                    ProcessWarp(lastUT);
                }

                if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                {
                    Utilities.SetActiveKSCToRSS();
                }

                if (!KCT_GUI.IsPrimarilyDisabled && HighLogic.LoadedScene == GameScenes.SPACECENTER &&
                    VesselSpawnDialog.Instance != null && VesselSpawnDialog.Instance.Visible)
                {
                    VesselSpawnDialog.Instance.ButtonClose();
                    KCTDebug.Log("Attempting to close spawn dialog!");
                }

                if (!KCT_GUI.IsPrimarilyDisabled)
                    Utilities.ProgressBuildTime();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void UpdateActiveLPLevel()
        {
            if (KCTGameStates.ActiveKSC?.ActiveLPInstance != null && HighLogic.LoadedScene == GameScenes.SPACECENTER && Utilities.CurrentGameIsCareer())
            {
                if (_lvlCheckTimer++ > 30)
                {
                    _lvlCheckTimer = 0;
                    if (Utilities.BuildingUpgradeLevel(SpaceCenterFacility.LaunchPad) != KCTGameStates.ActiveKSC.ActiveLPInstance.level)
                    {
                        KCTGameStates.ActiveKSC.SwitchLaunchPad(KCTGameStates.ActiveKSC.ActiveLaunchPadID, false);
                        KCTGameStates.UpdateLaunchpadDestructionState = true;
                    }
                }
            }
        }

        private static void UpdateBuildRates()
        {
            if (!_ratesUpdated)
            {
                if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
                {
                    if (ScenarioUpgradeableFacilities.GetFacilityLevelCount(SpaceCenterFacility.VehicleAssemblyBuilding) >= 0)
                    {
                        _ratesUpdated = true;
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
                            KCTDebug.Log("Cached " + facility.ToString() + " max at " + KCTGameStates.BuildingMaxLevelCache[facility.ToString()]);
                        }
                    }
                }
                else
                {
                    _ratesUpdated = true;
                }
            }
        }

        private static void ProcessWarp(double lastUT)
        {
            IKCTBuildItem iKctItem = Utilities.GetNextThingToFinish();
            if (KCTGameStates.TargetedItem == null && iKctItem != null) 
                KCTGameStates.TargetedItem = iKctItem;
            double remaining = iKctItem != null ? iKctItem.GetTimeLeft() : -1;
            double dT = TimeWarp.CurrentRate / (KCTGameStates.UT - lastUT);
            if (dT >= 20)
                dT = 0.1;
            int nBuffers = 1;
            if (KCTGameStates.CanWarp && iKctItem != null && !iKctItem.IsComplete())
            {
                int warpRate = TimeWarp.CurrentRateIndex;
                if (warpRate < KCTGameStates.LastWarpRate) //if something else changes the warp rate then release control to them, such as Kerbal Alarm Clock
                {
                    KCTGameStates.CanWarp = false;
                    KCTGameStates.LastWarpRate = 0;
                }
                else
                {
                    if (iKctItem == KCTGameStates.TargetedItem && warpRate > 0 && 
                        TimeWarp.fetch.warpRates[warpRate] * dT * nBuffers > Math.Max(remaining, 0))
                    {
                        int newRate = warpRate;
                        //find the first rate that is lower than the current rate
                        while (newRate > 0)
                        {
                            if (TimeWarp.fetch.warpRates[newRate] * dT * nBuffers < remaining)
                                break;
                            newRate--;
                        }
                        KCTDebug.Log("Warping down to " + newRate + " (delta: " + (TimeWarp.fetch.warpRates[newRate] * dT) + ")");
                        TimeWarp.SetRate(newRate, true); //hopefully a faster warp down than before
                        warpRate = newRate;
                    }
                    else if (warpRate == 0 && KCTGameStates.WarpInitiated)
                    {
                        KCTGameStates.CanWarp = false;
                        KCTGameStates.WarpInitiated = false;
                        KCTGameStates.TargetedItem = null;

                    }
                    KCTGameStates.LastWarpRate = warpRate;
                }

            }
            else if (iKctItem != null && iKctItem == KCTGameStates.TargetedItem && 
                     (KCTGameStates.WarpInitiated || KCTGameStates.Settings.ForceStopWarp) && 
                     TimeWarp.CurrentRateIndex > 0 && (remaining < 1) && (!iKctItem.IsComplete())) //Still warp down even if we don't control the clock
            {
                TimeWarp.SetRate(0, true);
                KCTGameStates.WarpInitiated = false;
                KCTGameStates.TargetedItem = null;
            }
        }

        public void LateUpdate()
        {
            if (Utilities.CurrentGameIsMission()) return;

            if (RDController.Instance != null && !_isIconUpdated)
            {
                UpdateTechlistIconColor();
                _isIconUpdated = true;
            }
            else
                _isIconUpdated = false;
        }

        public void UpdateTechlistIconColor()
        {
            if (RDController.Instance != null)
            {
                for (int i = RDController.Instance.nodes.Count; i-- > 0;)
                {
                    RDNode node = RDController.Instance.nodes[i];
                    if (node?.tech != null)
                    {
                        if (HasTechInList(node.tech.techID))
                        {
                            node.graphics?.SetIconColor(XKCDColors.KSPNotSoGoodOrange);
                        }
                        // else reset? Bleh, why bother.
                    }
                }
            }
        }

        protected bool HasTechInList(string id)
        {
            for (int i = KCTGameStates.TechList.Count; i-- > 0;)
            {
                if (KCTGameStates.TechList[i].TechID == id)
                    return true;
            }

            return false;
        }

        public static void DelayedStart()
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
                KCTDebug.Log("Editing " + KCTGameStates.EditedVessel.ShipName);
                EditorLogic.fetch.shipNameField.text = KCTGameStates.EditedVessel.ShipName;
            }

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                KCTDebug.Log("SP Start");
                if (!KCT_GUI.IsPrimarilyDisabled)
                {
                    if (ToolbarManager.ToolbarAvailable && KCTGameStates.Settings.PreferBlizzyToolbar)
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
                    KCTGameStates.ActiveKSC.ActiveLPInstance.level = Utilities.BuildingUpgradeLevel(SpaceCenterFacility.LaunchPad);
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
                            KCTDebug.Log("Invalid Recon_Rollout at " + ksc.KSCName + ". ID " + rr.AssociatedID + " not found.");
                            ksc.Recon_Rollout.Remove(rr);
                            i--;
                        }
                    }

                    for (int i = 0; i < ksc.AirlaunchPrep.Count; i++)
                    {
                        AirlaunchPrep ap = ksc.AirlaunchPrep[i];
                        if (Utilities.FindBLVesselByID(new Guid(ap.AssociatedID)) == null)
                        {
                            KCTDebug.Log("Invalid KCT_AirlaunchPrep at " + ksc.KSCName + ". ID " + ap.AssociatedID + " not found.");
                            ksc.AirlaunchPrep.Remove(ap);
                            i--;
                        }
                    }
                }
                KCTDebug.Log("SP done");
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
