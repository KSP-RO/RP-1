using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleAvionics : PartModule
    {
        #region Members
        [KSPField]
        public float massLimit = float.MaxValue; // default is unlimited

        [KSPField]
        public float enabledkW = -1f;

        [KSPField]
        public float disabledkW = -1f;

        [KSPField(guiActive = false, guiName = "Watts used: ", guiFormat = "N1", guiUnits = " W")]
        public float currentWatts = 0f;

        [KSPField]
        public bool toggleable = false;

        [KSPField(isPersistant = true)]
        public bool systemEnabled = true;

        protected ModuleResource commandChargeResource = null;
        protected bool wasWarping = false;
        protected bool currentlyEnabled = true;

        // returns current limit, based on enabled/disabled
        public float CurrentMassLimit
        {
            get
            {
                if (currentlyEnabled)
                    return massLimit;
                else
                    return 0f;
            }
        }
        #endregion

        #region Utility methods
        protected double ResourceRate()
        {
            if (part.Modules.Contains("ModuleCommand"))
            {
                ModuleCommand mC = (ModuleCommand)part.Modules["ModuleCommand"];
                foreach(ModuleResource r in mC.inputResources)
                {
                    if(r.name.Equals("ElectricCharge"))
                    {
                        commandChargeResource = r;
                        if (enabledkW < 0)
                            enabledkW = (float)r.rate;
                        return r.rate;
                    }
                }
            }
            return -1;
        }
        protected void UpdateRate()
        {
            if ((TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > 1f) || !systemEnabled)
            {
                currentlyEnabled = false;
                commandChargeResource.rate = currentWatts = disabledkW;
            }
            else
            {
                currentlyEnabled = true;
                commandChargeResource.rate = currentWatts = enabledkW;
            }
            currentWatts *= 1000f;
        }
        #endregion

        #region Overrides
        public void Start()
        {
            // check then bind to ModuleCommand
            if (toggleable && disabledkW >= 0f && ResourceRate() >= 0)
                UpdateRate();
            else
            {
                toggleable = false;
                currentlyEnabled = true; // just in case
            }

            Fields["currentWatts"].guiActive = 
                Events["ToggleEvent"].guiActive =
                Events["ToggleEvent"].guiActiveEditor = 
                Actions["ToggleAction"].active =
                Actions["ActivateAction"].active =
                Actions["ShutdownAction"].active = toggleable;

            if(systemEnabled)
                Events["ToggleEvent"].guiName = "Shutdown Avionics";
            else
                Events["ToggleEvent"].guiName = "Activate Avionics";
        }

        public override string GetInfo()
        {
            string retStr = "This part allows control of vessels of ";
            if (massLimit < float.MaxValue)
                retStr += "up to " + massLimit.ToString("N3") + " tons.";
            else
                retStr += "any mass.";
            if(toggleable && disabledkW >= 0f)
            {
                double resRate = ResourceRate();
                if(resRate >= 0)
                {
                    retStr += "\nCan be disabled, to lower command module wattage from " 
                        + (enabledkW * 1000d).ToString("N1") + " W to " + (disabledkW * 1000d).ToString("N1") + " W.";
                }
            }
            return retStr;
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            // Automatic mode switch
            bool isWarping = (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > 1f);
            if (toggleable && isWarping != wasWarping)
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
            if (systemEnabled)
            {
                Events["ToggleEvent"].guiName = "Activate Avionics";
                Actions["ActivateAction"].active = true;
                Actions["ShutdownAction"].active = false;
                systemEnabled = false;
            }
            else
            {
                Events["ToggleEvent"].guiName = "Shutdown Avionics";
                Actions["ShutdownAction"].active = true;
                Actions["ActivateAction"].active = false;
                systemEnabled = true;
            }
            UpdateRate();
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
