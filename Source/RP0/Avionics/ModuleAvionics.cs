using System.Collections;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using UnityEngine;
using RealAntennas;

namespace RP0
{
    public class ModuleAvionics : PartModule
    {
        #region Members
        protected const string PAWGroup = "Avionics";

        [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiName = "Controllable", guiFormat = "N2", guiUnits = "T", groupName = PAWGroup, groupDisplayName = PAWGroup)]
        public float massLimit = float.MaxValue; // default is unlimited

        [KSPField]
        public float enabledkW = -1f;

        [KSPField]
        public float disabledkW = -1f;

        [KSPField]
        public bool allowAxial = true;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Power", guiFormat = "N1", guiUnits = "\u2009W", groupName = PAWGroup, groupDisplayName = PAWGroup)]
        public float currentWatts = 0f;

        [KSPField]
        public bool toggleable = false;

        [KSPField(isPersistant = true)]
        public bool systemEnabled = true;

        [KSPField]
        public bool canPermaDisable = true;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Permanently disabled", groupName = PAWGroup, groupDisplayName = PAWGroup)]
        public bool dead = false;

        [KSPField]
        public string techRequired = "";

        [KSPField]
        public bool interplanetary = true;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Set to Debris", groupName = PAWGroup, groupDisplayName = PAWGroup),
            UI_Toggle(disabledText = "Never", enabledText = "On Stage", affectSymCounterparts = UI_Scene.Editor)]
        public bool setToDebrisOnStage = false;

        public bool useKerbalismInFlight = true;
        private bool onRailsCached = false;
        protected ModuleResource commandChargeResource = null;
        protected bool currentlyEnabled = true;
        private const string ecName = "ElectricCharge";
        private static Assembly KerbalismAPI = null;

        protected virtual float GetInternalMassLimit() => massLimit;
        protected virtual float GetEnabledkW() => enabledkW;
        protected virtual float GetDisabledkW() => disabledkW;
        protected virtual bool GetToggleable() => !dead && toggleable;
        internal bool TechAvailable => string.IsNullOrEmpty(techRequired) || HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX || ResearchAndDevelopment.GetTechnologyState(techRequired) == RDTech.State.Available;

        /// <summary>
        /// Returns current limit, based on enabled/disabled.
        /// Will be 0 if avionics is locked.
        /// </summary>
        public float CurrentMassLimit
        {
            get
            {
                if (!dead && currentlyEnabled && TechAvailable && !IsLockedByInterplanetary)
                    return GetInternalMassLimit();
                else
                    return 0f;
            }
        }

        /// <summary>
        /// Whether the control is locked because the avionics is non-interplanetary and the vessel is beyond what we consider near Earth orbit.
        /// </summary>
        public bool IsLockedByInterplanetary
        {
            get
            {
                return !interplanetary && part?.vessel != null &&
                    (!part.vessel.mainBody.isHomeWorld || part.vessel.altitude > InterplanetaryAltitudeThreshold);
            }
        }

        /// <summary>
        /// Whether the avionics is NE with controllable mass > 0 and control is being locked by being interplanetary.
        /// </summary>
        public bool IsNearEarthAndLockedByInterplanetary => !dead && GetInternalMassLimit() > 0 && IsLockedByInterplanetary;

        /// <summary>
        /// The altitude threshold around home world above which interplanetary avionics is required.
        /// </summary>
        public static float InterplanetaryAltitudeThreshold => Planetarium.fetch.Home.scienceValues.spaceAltitudeThreshold * 2;  // *2, so resonant satellite orbits can still be used
        
        #endregion

        protected void InitializeResourceRate()
        {
            commandChargeResource ??= FindCommandChargeResource();
            if (GetEnabledkW() < 0)
                enabledkW = (commandChargeResource is ModuleResource r) ? (float) r.rate : 0;
            if (commandChargeResource is ModuleResource res)
                res.rate = 0;   // Disable the CommandModule consuming electric charge on init.
        }

        public float GetPowerDraw(bool onRails = false)
        {
            if (dead) return 0;

            return part.protoModuleCrew?.Count > 0 || (systemEnabled && !(GetToggleable() && onRails)) ?
                GetEnabledkW() : GetDisabledkW();
        }

        protected void UpdateRate(bool onRails = false)
        {
            currentlyEnabled = systemEnabled && !(GetToggleable() && onRails);
            float currentKW = GetPowerDraw(onRails);
            ecConsumption = new KeyValuePair<string, double>(ecName, -currentKW);
            currentWatts = currentKW * 1000;
            // If requested, let Kerbalism handle power draw, else handle via ModuleCommand resourceHandler.
            if (commandChargeResource is ModuleResource res)
                res.rate = useKerbalismInFlight ? 0 : currentKW;
        }

        #region Utility methods

        private ModuleResource FindCommandChargeResource() =>
            part.FindModuleImplementing<ModuleCommand>()?.resHandler.inputResources.FirstOrDefault(r => r?.id == PartResourceLibrary.ElectricityHashcode);

        protected virtual void SetActionsAndGui()
        {
            bool toggleable = GetToggleable();
            Events[nameof(ToggleEvent)].guiActive = toggleable;
            Events[nameof(ToggleEvent)].guiActiveEditor = toggleable;
            Events[nameof(ToggleEvent)].guiName = (systemEnabled ? "Shutdown" : "Activate") + " Avionics";
            Events[nameof(ToggleEvent)].active = toggleable;
            Actions[nameof(ActivateAction)].active = (!systemEnabled || HighLogic.LoadedSceneIsEditor) && toggleable;
            Actions[nameof(ShutdownAction)].active = (systemEnabled || HighLogic.LoadedSceneIsEditor) && toggleable;
            Actions[nameof(ToggleAction)].active = toggleable;
            Fields[nameof(currentWatts)].guiActive = toggleable;
            Actions[nameof(KillAction)].active = canPermaDisable && !dead;
            Events[nameof(KillEvent)].guiActive = canPermaDisable && !dead;
            Events[nameof(KillEvent)].active = canPermaDisable && !dead;
        }

        protected void StageActivated(int stage)
        {
            if (setToDebrisOnStage)
                StartCoroutine(CheckRenameDebris());
        }

        protected IEnumerator CheckRenameDebris()
        {
            bool rename = true;
            yield return new WaitForSeconds(1f);
            if (vessel != FlightGlobals.ActiveVessel)
            {
                foreach (Part p in vessel.Parts)
                {
                    ModuleAvionics avionics = p.FindModuleImplementing<ModuleAvionics>();
                    ModuleCommand command = p.FindModuleImplementing<ModuleCommand>();
                    bool debris = avionics?.setToDebrisOnStage ?? true;
                    if (command && !debris)
                    {
                        rename = false;
                        break;
                    }
                }

                if (rename)
                    vessel.vesselType = VesselType.Debris;
            }
        }

        /// <summary>
        /// Even after disabling ModuleCommand, the vessel will still have comms and be controllable through Kerbalism.
        /// If all the antenna PMs are disabled then RA will make sure that the issue is fixed in a permanent way.
        /// </summary>
        protected void KillAntennasIfLastAvionicsDead()
        {
            bool anyAlive = false;
            List<ModuleAvionics> avionicsModules = part.vessel.FindPartModulesImplementing<ModuleAvionics>();
            foreach (ModuleAvionics am in avionicsModules)
            {
                anyAlive |= !am.dead;
            }

            if (!anyAlive)
            {
                List<ModuleRealAntenna> antennaModules = part.vessel.FindPartModulesImplementing<ModuleRealAntenna>();
                foreach (ModuleRealAntenna mra in antennaModules)
                {
                    if (mra.Condition != AntennaCondition.Disabled)
                        mra.PermanentShutdownAction(null);
                }
            }
        }

        #endregion

        #region Overrides and callbacks

        public override void OnAwake()
        {
            base.OnAwake();
            GameEvents.onStageActivate.Add(StageActivated);
            if (HighLogic.LoadedScene != GameScenes.LOADING)
                KerbalismAPI ??= KerbalismUtils.Assembly;
        }

        // OnStartFinished() instead of OnStart(), to let ModuleCommand configure itself first.
        public override void OnStartFinished(StartState _)
        {
            if (dead)
            {
                DisableModuleCommand();
            }

            InitializeResourceRate();
            if (!GetToggleable())
                toggleable = false;
            UpdateRate();
            SetActionsAndGui();
            if (HighLogic.LoadedSceneIsEditor)
                GameEvents.onEditorShipModified.Add(OnShipModified);
            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onVesselGoOnRails.Add(GoOnRails);
                GameEvents.onVesselGoOffRails.Add(GoOffRails);
                OnSettingsApplied();
                GameEvents.OnGameSettingsApplied.Add(OnSettingsApplied);
            }
        }

        private void OnSettingsApplied()
        {
            useKerbalismInFlight = KerbalismAPI != null && HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().AvionicsUseKerbalism;
            UpdateRate(onRailsCached);
        }

        private void OnShipModified(ShipConstruct _) => UpdateRate(onRailsCached);
        private void GoOnRails(Vessel v) => RailChange(v, true);
        private void GoOffRails(Vessel v) => RailChange(v, false);

        private void RailChange(Vessel v, bool onRails)
        {
            onRailsCached = onRails;
            if (vessel == v)
                UpdateRate(onRails);
        }

        // Too many ways to exit a scene, so always write the Disabled power draw
        public override void OnSave(ConfigNode node)
        {
            node.SetValue(nameof(currentWatts), GetPowerDraw(true) * 1000);
        }

        protected void OnDestroy()
        {
            GameEvents.onStageActivate.Remove(StageActivated);
            GameEvents.onEditorShipModified.Remove(OnShipModified);
            GameEvents.onVesselGoOnRails.Remove(GoOnRails);
            GameEvents.onVesselGoOffRails.Remove(GoOffRails);
            GameEvents.OnGameSettingsApplied.Remove(OnSettingsApplied);
        }

        protected virtual string GetTonnageString()
        {
            string lim = (massLimit < float.MaxValue) ? $"up to {massLimit:N3} tons" : "any mass";
            return $"This part allows control of vessels of {lim}.";
        }

        public override string GetInfo()
        {
            string retStr = GetTonnageString();
            InitializeResourceRate();

            if (GetToggleable() && GetDisabledkW() >= 0)
            {
                retStr += "\nCan be disabled, to lower wattage from " 
                    + $"{(GetEnabledkW() * 1000d):N1}\u2009W to {(GetDisabledkW() * 1000d):N1}\u2009W.";
            } else
            {
                retStr += $"\nConsumes {(GetEnabledkW() * 1000d):N1}\u2009W";
            }

            if (!string.IsNullOrEmpty(techRequired))
                retStr += "\nNote: requires technology unlock to function.";

            if (!interplanetary)
                retStr += "\n<color=red>Note: Only works near Earth!</color>";

            return retStr;
        }

        protected void DisableModuleCommand()
        {
            ModuleCommand command = part.FindModuleImplementing<ModuleCommand>();
            if (command != null)
            {
                command.enabled = false;
                command.isEnabled = false;
                command.moduleIsEnabled = false;
                part.isControlSource = Vessel.ControlLevel.NONE;
            }
        }

        #endregion

        #region Actions and Events

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Shutdown Avionics", groupName = PAWGroup)]
        public void ToggleEvent()
        {
            systemEnabled = !systemEnabled;
            if (!dead && part.protoModuleCrew?.Count > 0)
            {
                systemEnabled = true;
                ScreenMessages.PostScreenMessage("Cannot shut down avionics while crewed");
            }
            else if (!dead && !GetToggleable())
            {
                systemEnabled = true;
                ScreenMessages.PostScreenMessage("Cannot shut down avionics on this part");
            }
            UpdateRate();
            SetActionsAndGui();
            if (HighLogic.LoadedSceneIsEditor)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        [KSPAction("Toggle Avionics")]
        public void ToggleAction(KSPActionParam _)
        {
            ToggleEvent();
        }

        [KSPAction("Shutdown Avionics")]
        public void ShutdownAction(KSPActionParam _)
        {
            systemEnabled = true;
            ToggleEvent();
        }

        [KSPAction("Activate Avionics")]
        public void ActivateAction(KSPActionParam _)
        {
            systemEnabled = false;
            ToggleEvent();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Disable Avionics permanently", groupName = PAWGroup)]
        public void KillEvent()
        {
            var options = new DialogGUIBase[] {
                new DialogGUIButton("Yes", () => KillAction(null)),
                new DialogGUIButton("No", () => {})
            };
            var dialog = new MultiOptionDialog("ConfirmDisableAvionics", "Are you sure you want to permanently disable the avionics unit? Doing this will prevent avionics from consuming power but it will no longer provide any control nor use its internal antenna. Note that disabling the last avionics unit on the vessel will also disable all antennas. (internal and external, ingoing and outgoing)", "Disable Avionics", HighLogic.UISkin, 300, options);
            PopupDialog.SpawnPopupDialog(dialog, true, HighLogic.UISkin);
        }

        [KSPAction("Disable Avionics permanently")]
        public void KillAction(KSPActionParam _)
        {
            dead = true;
            DisableModuleCommand();
            KillAntennasIfLastAvionicsDead();
            ToggleEvent();
        }
        #endregion

        #region Kerbalism

        private KeyValuePair<string, double> ecConsumption = new KeyValuePair<string, double>(ecName, 0);

        public string PlannerUpdate(List<KeyValuePair<string, double>> resources, CelestialBody _, Dictionary<string, double> environment)
        {
            resources.Add(ecConsumption);   // ecConsumption is updated by the Toggle event
            return "avionics";
        }

        public static string BackgroundUpdate(Vessel v,
            ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot,
            PartModule proto_part_module, Part proto_part,
            Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest,
            double elapsed_s)
        {
            if (availableResources.ContainsKey(ecName))
            {
                float cw = 0;
                if (module_snapshot.moduleValues.TryGetValue("currentWatts", ref cw))
                    resourceChangeRequest.Add(new KeyValuePair<string, double>(ecName, -cw / 1000));
            }
            return "avionics";
        }

        public virtual string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
        {
            if (useKerbalismInFlight)
                resourceChangeRequest.Add(ecConsumption);   // ecConsumption is updated by the Toggle event
            return "avionics";
        }

        #endregion

    }
}
