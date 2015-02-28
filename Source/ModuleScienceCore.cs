using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleScienceCore : PartModule
    {
        public int vParts = -1;
        bool wasLocked = false;
        const ControlTypes lockmask = ControlTypes.YAW | ControlTypes.PITCH | ControlTypes.ROLL | ControlTypes.SAS | ControlTypes.RCS;
        const string lockID = "ModuleScienceCore";

        public bool ShouldLock()
        {
            if (!HighLogic.LoadedSceneIsFlight || (object)vessel == null || FlightGlobals.ActiveVessel != vessel)
                return false;

            if (vessel.Parts.Count != vParts)
            {
                vParts = vessel.Parts.Count;
                for (int i = 0; i < vessel.Parts.Count; i++)
                {
                    if (vessel.Parts[i].Modules.Contains("ModuleCommand") && !vessel.Parts[i].Modules.Contains("ModuleScienceCore"))
                        return false;
                }
                return true;
            }
            return wasLocked;
        }
        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            bool doLock = ShouldLock();
            if (doLock != wasLocked)
            {
                if (wasLocked)
                    InputLockManager.RemoveControlLock(lockID);
                else
                {
                    InputLockManager.RemoveControlLock(lockID); // in case another module had already set this.
                    InputLockManager.SetControlLock(lockmask, lockID);
                }
                wasLocked = doLock;
            }
        }
        public void OnDestroy()
        {
            InputLockManager.RemoveControlLock(lockID);
        }
    }
}
