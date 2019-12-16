using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RP0
{
    class ModuleAvionics : PartModule
    {
        #region Members
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Controllable", guiFormat = "N1", guiUnits = "T")]
        public float massLimit = float.MaxValue; // default is unlimited

        [KSPField]
        public float enabledkW = -1f;

        [KSPField]
        public float disabledkW = -1f;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Power", guiFormat = "N1", guiUnits = "\u2009W")]
        public float currentWatts = 0f;

        [KSPField]
        public bool toggleable = false;

        [KSPField(isPersistant = true)]
        public bool systemEnabled = true;

        [KSPField]
        public string techRequired = "";

        [KSPField]
        public bool interplanetary = true;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Set to Debris"),
            UI_Toggle(disabledText = "Never", enabledText = "On Stage", affectSymCounterparts = UI_Scene.Editor)]
        public bool setToDebrisOnStage = false;

        protected ModuleResource commandChargeResource = null;
        protected bool wasWarping = false;
        protected bool currentlyEnabled = true;
        private const string ecName = "ElectricCharge";
        private static Assembly KerbalismAPI = null;

        protected virtual float GetInternalMassLimit() => massLimit;
        protected virtual float GetEnabledkW() => enabledkW;
        protected virtual float GetDisabledkW() => disabledkW;
        protected virtual bool GetToggleable() => toggleable;
        internal bool TechAvailable => string.IsNullOrEmpty(techRequired) || HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX || ResearchAndDevelopment.GetTechnologyState(techRequired) == RDTech.State.Available;

        // returns current limit, based on enabled/disabled
        public float CurrentMassLimit
        {
            get
            {
                if (currentlyEnabled && TechAvailable
                    && (interplanetary || part.vessel == null || part.vessel.mainBody == null 
                        || (part.vessel.mainBody.isHomeWorld && part.vessel.altitude < part.vessel.mainBody.scienceValues.spaceAltitudeThreshold * 2)))  // *2, so resonant satellite orbits can still be used
                    return GetInternalMassLimit();
                else
                    return 0f;
            }
        }
        #endregion

        protected void InitializeResourceRate()
        {
            commandChargeResource ??= FindCommandChargeResource();
            if (GetEnabledkW() < 0)
                enabledkW = (commandChargeResource is ModuleResource r) ? (float) r.rate : 0;
            if (commandChargeResource is ModuleResource res)
                res.rate = 0;   // Disable the CommandModule consuming electric charge on init.
        }

        protected void UpdateRate()
        {
            float currentKW;
            if (part.protoModuleCrew?.Count > 0)
            {
                currentlyEnabled = systemEnabled = true;
                currentKW = GetEnabledkW();
                ScreenMessages.PostScreenMessage("Cannot shut down avionics while crewed");
            }
            else
            {
                currentlyEnabled = systemEnabled && !(TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > 1f);
                currentKW = currentlyEnabled ? GetEnabledkW() : GetDisabledkW();
            }
            ecConsumption = new KeyValuePair<string, double>(ecName, -currentKW);
            currentWatts = currentKW * 1000;
            // If Kerbalism, Avionics will handle all power draw through it.
            // If not, then let the ModuleCommand go ahead and consume ec.
            if (KerbalismAPI == null && commandChargeResource is ModuleResource res)
                res.rate = currentKW;
        }

        #region Utility methods

        private ModuleResource FindCommandChargeResource()
        {
            if (part.FindModuleImplementing<ModuleCommand>() is ModuleCommand mC)
            {
                foreach (ModuleResource r in mC.resHandler.inputResources)
                {
                    if (r.id == PartResourceLibrary.ElectricityHashcode)
                        return r;
                }
//                return mC.resHandler.inputResources.First(r => r?.id == PartResourceLibrary.ElectricityHashcode);
            }
            return null;
        }

        protected void OnConfigurationUpdated()
        {
            SetActionsAndGui();
        }

        private void SetActionsAndGui()
        {
            var toggleAble = GetToggleable();
            Events[nameof(ToggleEvent)].guiActive = toggleAble;
            Events[nameof(ToggleEvent)].guiActiveEditor = toggleAble;
            Events[nameof(ToggleEvent)].guiName = (systemEnabled ? "Shutdown" : "Activate") + " Avionics";
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
                    if (p == part)
                        continue;

                    bool hasCommand = false;
                    bool allAvionicsDebris = true;
                    bool noAvionics = true;
                    foreach (PartModule pm in p.Modules)
                    {
                        if (pm is ModuleCommand)
                        {
                            hasCommand = true;
                        }
                        else if (pm is ModuleAvionics am)
                        {
                            noAvionics = false;
                            if (!am.setToDebrisOnStage)
                                allAvionicsDebris = false;
                        }
                    }
                    if (hasCommand && (noAvionics || !allAvionicsDebris))
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
                KerbalismAPI ??= AssemblyLoader.loadedAssemblies.First(x => x.name.StartsWith("Kerbalism"))?.assembly;
        }

        // OnStartFinished(), to let ModuleCommand configure itself first.
//        public override void OnStart(StartState _)
        public override void OnStartFinished(StartState _)
        {
            InitializeResourceRate();
            if (!GetToggleable())
                toggleable = false;
            UpdateRate();
            SetActionsAndGui();
            if (HighLogic.LoadedSceneIsEditor)
                GameEvents.onEditorShipModified.Add(OnShipModified);
        }

        private void OnShipModified(ShipConstruct _) => UpdateRate();

        protected void OnDestroy()
        {
            GameEvents.onStageActivate.Remove(StageActivated);
            GameEvents.onEditorShipModified.Remove(OnShipModified);
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

        public override void OnUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            // Automatic mode switch
            bool isWarping = (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > 1f);
            if (GetToggleable() && isWarping != wasWarping)
            {
                // Maybe do a screenmessage here?
                UpdateRate();
            }
            wasWarping = isWarping;
        }

        #endregion

        #region Actions and Events

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Shutdown Avionics")]
        public void ToggleEvent()
        {
            systemEnabled = !systemEnabled;
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
            return "Avionics";
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
                    resourceChangeRequest.Add(new KeyValuePair<string, double>(ecName, cw / 1000));
            }
            return "Avionics";
        }

        public virtual string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
        {
            resourceChangeRequest.Add(ecConsumption);   // ecConsumption is updated by the Toggle event
            return "Avionics";
        }

        #endregion

    }
}
