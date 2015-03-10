using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;


namespace RP0
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ControlLocker : MonoBehaviour
    {
        public int vParts = -1;
        public float maxMass = -1f, vesselMass = 0f;
        public Vessel vessel = null;
        bool wasLocked = false;
        const ControlTypes lockmask = ControlTypes.YAW | ControlTypes.PITCH | ControlTypes.ROLL | ControlTypes.SAS | 
            ControlTypes.RCS | ControlTypes.THROTTLE | ControlTypes.WHEEL_STEER | ControlTypes.WHEEL_THROTTLE;
        const string lockID = "RP0ControlLocker";

        public bool ShouldLock()
        {
            if (vessel != FlightGlobals.ActiveVessel)
            {
                vParts = -1;
                vessel = FlightGlobals.ActiveVessel;
            }
            // if we have no active vessel, undo locks
            if ((object)vessel == null)
                return false;

            // Do we need to update?
            if (vessel.Parts.Count != vParts)
            {
                maxMass = 0f;
                vesselMass = 0f;
                vParts = vessel.Parts.Count;
                for (int i = 0; i < vessel.Parts.Count; i++)
                {
                    // add up mass
                    if ((object)(vessel.Parts[i].rb) != null)
                        vesselMass += vessel.Parts[i].rb.mass;
                    else
                        vesselMass += vessel.Parts[i].mass + vessel.Parts[i].GetResourceMass();

                    // get modules
                    bool cmd = false, science = false, avionics = false;
                    for(int j = 0; j < vessel.Parts[i].Modules.Count; j++)
                    {
                        cmd = cmd || ((vessel.Parts[i].Modules[j]) is ModuleCommand);
                        science = science || ((vessel.Parts[i].Modules[j]) is ModuleScienceCore);
                        bool avionicsHere = (vessel.Parts[i].Modules[j]) is ModuleAvionics;
                        avionics = avionics || avionicsHere;
                        if (avionicsHere)
                            maxMass += ((ModuleAvionics)(vessel.Parts[i].Modules[j])).massLimit;
                    }
                    // switch based on modules
                    if (cmd && !science && !avionics) // unencumbered command module
                        return false;
                }
                if (maxMass >= vesselMass) // will only be reached if the best we have is avionics.
                    return false; // unlock if our max avionics mass is >= vessel mass
                // NOTE: we don't update for fuel burnt, because avionics needs to be able to handle the size
                // as well as the fuel.

                // Otherwise, we lock yaw/pitch/roll.
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
}
