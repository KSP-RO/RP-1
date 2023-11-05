using System.Collections;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using UnityEngine;

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
        protected virtual bool GetToggleable() => toggleable;
        internal bool TechAvailable => string.IsNullOrEmpty(techRequired) || HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX || ResearchAndDevelopment.GetTechnologyState(techRequired) == RDTech.State.Available;

        /// <summary>
        /// Returns current limit, based on enabled/disabled.
        /// Will be 0 if avionics is locked.
        /// </summary>
        public float CurrentMassLimit
        {
            get
            {
                if (currentlyEnabled && TechAvailable && !IsLockedByInterplanetary)
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
        public bool IsNearEarthAndLockedByInterplanetary => GetInternalMassLimit() > 0 && IsLockedByInterplanetary;

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

        public float PowerDraw(bool onRails = false) =>
            part.protoModuleCrew?.Count > 0 || (systemEnabled && !(GetToggleable() && onRails)) ?
            GetEnabledkW() : GetDisabledkW();

        protected void UpdateRate(bool onRails = false)
        {
            currentlyEnabled = systemEnabled && !(GetToggleable() && onRails);
            float currentKW = PowerDraw(onRails);
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
            var toggleAble = GetToggleable();
            Events[nameof(ToggleEvent)].guiActive = toggleAble;
            Events[nameof(ToggleEvent)].guiActiveEditor = toggleAble;
            Events[nameof(ToggleEvent)].guiName = (systemEnabled ? "Shutdown" : "Activate") + " Avionics";
            Events[nameof(ToggleEvent)].active = toggleAble;
            Actions[nameof(ActivateAction)].active = (!systemEnabled || HighLogic.LoadedSceneIsEditor) && toggleAble;
            Actions[nameof(ShutdownAction)].active = (systemEnabled || HighLogic.LoadedSceneIsEditor) && toggleAble;
            Actions[nameof(ToggleAction)].active = toggleAble;
            Fields[nameof(currentWatts)].guiActive = toggleAble;
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

        #endregion

        #region Overrides and callbacks

        public override void OnAwake()
        {
            base.OnAwake();
            GameEvents.onStageActivate.Add(StageActivated);
            if (HighLogic.LoadedScene != GameScenes.LOADING)
                KerbalismAPI ??= AssemblyLoader.loadedAssemblies.FirstOrDefault(x => x.name.StartsWith("Kerbalism"))?.assembly;
        }

        // OnStartFinished() instead of OnStart(), to let ModuleCommand configure itself first.
        public override void OnStartFinished(StartState _)
        {
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
            node.SetValue(nameof(currentWatts), PowerDraw(true) * 1000);
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

        #endregion

        #region Actions and Events

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Shutdown Avionics", groupName = PAWGroup)]
        public void ToggleEvent()
        {
            systemEnabled = !systemEnabled;
            if (part.protoModuleCrew?.Count > 0)
            {
                systemEnabled = true;
                ScreenMessages.PostScreenMessage("Cannot shut down avionics while crewed");
            }
            else if (!GetToggleable())
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
