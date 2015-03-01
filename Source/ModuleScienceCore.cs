using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ScienceCoreControlLocker
    {
        public int vParts = -1;
        public Vessel vessel = null;
        bool wasLocked = false;
        const ControlTypes lockmask = ControlTypes.YAW | ControlTypes.PITCH | ControlTypes.ROLL | ControlTypes.SAS | ControlTypes.RCS;
        const string lockID = "ModuleScienceCore";
        
        public bool ShouldLock()
        {
            if (vessel != FlightGlobals.ActiveVessel)
            {
                vParts = -1;
                vessel = FlightGlobals.ActiveVessel;
            }
            if ((object)vessel == null)
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
            bool doLock = ShouldLock();
            if (doLock != wasLocked)
            {
                if (wasLocked)
                    InputLockManager.RemoveControlLock(lockID);
                else
                    InputLockManager.SetControlLock(lockmask, lockID);
                wasLocked = doLock;
            }
        }
        public void OnDestroy()
        {
            InputLockManager.RemoveControlLock(lockID);
        }
    }
    class ModuleScienceCore : PartModule
    {
        public override string GetInfo()
        {
            return "This part alone only allows limited command interaction; you will not be allowed full attitude control unless another command part without this module is attached.";
        }
    }
}
