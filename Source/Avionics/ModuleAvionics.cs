using System.Collections;
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

        [KSPField(guiActive = false, guiName = "Power", guiFormat = "N1", guiUnits = "\u2009W")]
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

        #region Utility methods
        protected double ResourceRate()
        {
            if (part.FindModuleImplementing<ModuleCommand>() is ModuleCommand mC)
            {
                foreach (ModuleResource r in mC.resHandler.inputResources)
                {
                    if (r.id == PartResourceLibrary.ElectricityHashcode)
                    {
                        commandChargeResource = r;
                        if (GetEnabledkW() < 0)
                        {
                            enabledkW = (float)r.rate;
                        }
                        else
                        {
                            r.rate = GetEnabledkW();
                        }
                        return r.rate;
                    }
                }
            }
            return -1;
        }

        protected void UpdateRate()
        {
            if (commandChargeResource == null) {
                UnityEngine.Debug.Log("[RP-0] - Can't change rate with no commandChargeResource");
                return;
            }
            if (part.protoModuleCrew != null && part.protoModuleCrew.Count > 0)
            {
                currentlyEnabled = systemEnabled = true;
                commandChargeResource.rate = currentWatts = GetEnabledkW();
                ScreenMessages.PostScreenMessage("Cannot shut down avionics while crewed");
            }
            else
            {
                currentlyEnabled = !((TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > 1f) || !systemEnabled);
                commandChargeResource.rate = currentWatts = currentlyEnabled ? GetEnabledkW() : GetDisabledkW();
            }
            currentWatts *= 1000f;
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

        #region Overrides
        public override void OnAwake()
        {
            base.OnAwake();
            GameEvents.onStageActivate.Add(StageActivated);
        }

        public void Start()
        {
        // check then bind to ModuleCommand
            if (ResourceRate() <= 0f || !GetToggleable() || GetDisabledkW() <= 0f) {
                toggleable = false;
                currentlyEnabled = true; // just in case
            }
            //We want to call UpdateRate all the time to capture anything from proceduralAvionics
            UpdateRate();
            SetActionsAndGui();
        }

        protected void OnDestroy()
        {
            GameEvents.onStageActivate.Remove(StageActivated);
        }

        protected virtual string GetTonnageString()
        {
            string lim = (massLimit < float.MaxValue) ? $"up to {massLimit:N3} tons" : "any mass";
            return $"This part allows control of vessels of {lim}.";
        }

        public override string GetInfo()
        {
            string retStr = GetTonnageString();
            if(GetToggleable() && GetDisabledkW() >= 0f)
            {
                double resRate = ResourceRate();
                if(resRate >= 0)
                {
                    retStr += "\nCan be disabled, to lower command module wattage from " 
                        + $"{(GetEnabledkW() * 1000d):N1}\u2009W to {(GetDisabledkW() * 1000d):N1}\u2009W.";
                }
            }

            if (!string.IsNullOrEmpty(techRequired))
                retStr += "\nNote: requires technology unlock to function.";

            if (!interplanetary)
                retStr += "\n<color=red>Note: Only works near Earth!</color>";

            return retStr;
        }

        public void Update()
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
        }

        [KSPAction("Toggle Avionics")]
        public void ToggleAction(KSPActionParam param)
        {
            ToggleEvent();
        }

        [KSPAction("Shutdown Avionics")]
        public void ShutdownAction(KSPActionParam param)
        {
            systemEnabled = true;
            ToggleEvent();
        }

        [KSPAction("Activate Avionics")]
        public void ActivateAction(KSPActionParam param)
        {
            systemEnabled = false;
            ToggleEvent();
        }
        #endregion

    }
}
