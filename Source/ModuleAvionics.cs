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

        [KSPField]
        public string techRequired = "";

        protected bool wasWarping = false;
        protected bool currentlyEnabled = true;

        // returns current limit, based on enabled/disabled
        public float CurrentMassLimit
        {
            get
            {
                if (currentlyEnabled && (string.IsNullOrEmpty(techRequired) || HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX || ResearchAndDevelopment.GetTechnologyState(techRequired) == RDTech.State.Available))
                    return massLimit;
                else
                    return 0f;
            }
        }
        #endregion

        public override string GetInfo()
        {
            string retStr = "This part allows control of vessels of ";
            if (massLimit < float.MaxValue)
                retStr += "up to " + massLimit.ToString("N3") + " tons.";
            else
                retStr += "any mass.";
            if (!string.IsNullOrEmpty(techRequired))
                retStr += "\nNote: requires technology unlock to function.";
            return retStr;
        }

    }
}
