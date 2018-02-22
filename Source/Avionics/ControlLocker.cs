using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;
using KSP.UI;


namespace RP0
{
    class ControlLockerUtils
    {
        public static bool ShouldLock(List<Part> parts, bool countClamps, out float maxMass, out float vesselMass)
        {
            int crewCount = -1;
            maxMass = vesselMass = 0f;
            Part p;

            if (parts == null || parts.Count <= 0)
                return false;

            for (int i = parts.Count - 1; i >= 0; --i)
            {
                p = parts[i];
                // add up mass
                float partMass = p.mass + p.GetResourceMass();

                // get modules
                bool cmd = false, science = false, avionics = false, clamp = false;
                float partAvionicsMass = 0f;
                ModuleCommand mC = null;
                PartModule m;
                for (int j = p.Modules.Count - 1; j >= 0; --j)
                {
                    m = p.Modules[j];

                    if (m is KerbalEVA)
                        return false;

                    if (m is ModuleCommand)
                    {
                        cmd = true;
                        mC = m as ModuleCommand;
                    }
                    science |= m is ModuleScienceCore;
                    if (m is ModuleAvionics)
                    {
                        avionics = true;
                        partAvionicsMass += (m as ModuleAvionics).CurrentMassLimit;
                    }
                    if (m is LaunchClamp)
                    {
                        clamp = true;
                        partMass = 0f;
                    }
                    if (m is ModuleAvionicsModifier)
                    {
                        partMass *= (m as ModuleAvionicsModifier).multiplier;
                    }
                }
                vesselMass += partMass; // done after the clamp check

                // switch based on modules

                // Do we have an unencumbered command module?
                // if we count clamps, they can give control. If we don't, this works only if the part isn't a clamp.
                if ((countClamps || !clamp) && cmd && !science && !avionics)
                    return false;
                if (cmd && avionics) // check if need crew
                {
                    if (mC.minimumCrew > 0) // if we need crew
                    {
                        if (crewCount < 0) // see if we cached crew
                        {
                            if (HighLogic.LoadedSceneIsFlight) // get from vessel
                                crewCount = p.vessel.GetCrewCount();
                            else if (HighLogic.LoadedSceneIsEditor) // or crew manifest
                                crewCount = CrewAssignmentDialog.Instance.GetManifest().GetAllCrew(false).Count;
                            else crewCount = 0; // or assume no crew (should never trip this)
                        }
                        if (mC.minimumCrew > crewCount)
                            avionics = false; // not operational
                    }
                }
                if (avionics)
                    maxMass += partAvionicsMass;
            }
            if (maxMass > vesselMass) // will only be reached if the best we have is avionics.
                return false; // unlock if our max avionics mass is >= vessel mass

            // Otherwise, we lock yaw/pitch/roll.
            return true;
        }
    }

    /// <summary>
    /// This class will lock controls if and only if avionics requirements exist and are not met
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ControlLocker : MonoBehaviour
    {
        public int vParts = -1;
        public Vessel vessel = null;
        bool wasLocked = false;
        const ControlTypes lockmask = ControlTypes.YAW | ControlTypes.PITCH | ControlTypes.ROLL | ControlTypes.SAS | 
            ControlTypes.RCS | ControlTypes.THROTTLE | ControlTypes.WHEEL_STEER | ControlTypes.WHEEL_THROTTLE;
        const string lockID = "RP0ControlLocker";
        float maxMass, vesselMass;
        double lastUT = -1d;

        const double updateFrequency = 1d; // run a check every second, unless staging.

        ScreenMessage message = new ScreenMessage("", 8f, ScreenMessageStyle.UPPER_CENTER);

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
            double cTime = Planetarium.GetUniversalTime();
            if (vessel.Parts.Count != vParts || cTime > lastUT + updateFrequency)
            {
                lastUT = cTime;
                vParts = vessel.Parts.Count;
                return ControlLockerUtils.ShouldLock(vessel.Parts, true, out maxMass, out vesselMass);
            }
            return wasLocked;
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready)
            {
                InputLockManager.RemoveControlLock(lockID);
                return;
            }
            bool doLock = ShouldLock();
            if (doLock != wasLocked)
            {
                if (wasLocked)
                {
                    InputLockManager.RemoveControlLock(lockID);
                    message.message = "Avionics: Unlocking Controls";
                }
                else
                {
                    InputLockManager.SetControlLock(lockmask, lockID);
                    vessel.Autopilot.Disable();
                    message.message = "Insufficient Avionics, Locking Controls (supports "
                        + maxMass.ToString("N3") + "t, vessel " + vesselMass.ToString("N3") + "t)";
                }
                ScreenMessages.PostScreenMessage(message);
                FlightLogger.fetch.LogEvent(message.message);
                wasLocked = doLock;
            }
        }
        public void OnDestroy()
        {
            InputLockManager.RemoveControlLock(lockID);
        }
    }
}
