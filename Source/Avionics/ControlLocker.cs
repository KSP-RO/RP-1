using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI;
using System.Linq;
using System.Reflection;

namespace RP0
{
    class ControlLockerUtils
    {
        public static bool ShouldLock(List<Part> parts, bool countClamps, out float maxMass, out float vesselMass)
        {
            maxMass = vesselMass = 0f;
            bool forceUnlock = false;   // Defer return until maxMass and avionicsMass are fully calculated

            if (parts == null || parts.Count <= 0)
                return false;
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor) return false;

            int crewCount = (HighLogic.LoadedSceneIsFlight) ? parts[0].vessel.GetCrewCount() : CrewAssignmentDialog.Instance.GetManifest().GetAllCrew(false).Count;

            foreach (Part p in parts)
            {
                // add up mass
                float partMass = p.mass + p.GetResourceMass();

                // get modules
                bool cmd = false, science = false, avionics = false, clamp = false;
                float partAvionicsMass = 0f;
                ModuleCommand mC = null;
                foreach (PartModule m in p.Modules)
                {
                    if (m is KerbalEVA)
                        forceUnlock = true;

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

                // Do we have an unencumbered command module?
                // if we count clamps, they can give control. If we don't, this works only if the part isn't a clamp.
                if ((countClamps || !clamp) && cmd && !science && !avionics)
                    forceUnlock = true;
                if (cmd && avionics && mC.minimumCrew > crewCount) // check if need crew
                    avionics = false; // not operational
                if (avionics)
                    maxMass += partAvionicsMass;
            }
            return (!forceUnlock && vesselMass > maxMass);  // Lock if vessel mass is greater than controlled mass.
        }
    }

    /// <summary>
    /// This class will lock controls if and only if avionics requirements exist and are not met
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ControlLocker : MonoBehaviour
    {
        public Vessel vessel = null;
        private bool wasLocked = false;
        private bool requested = false;
        private const ControlTypes lockmask = ControlTypes.YAW | ControlTypes.PITCH | ControlTypes.ROLL | ControlTypes.SAS | 
                                              ControlTypes.THROTTLE | ControlTypes.WHEEL_STEER | ControlTypes.WHEEL_THROTTLE;
        private const string lockID = "RP0ControlLocker";
        private float maxMass, vesselMass;
        private double lastUT = -1d;

        private const double updateFrequency = 1d; // run a check every second, unless staging.

        private readonly ScreenMessage message = new ScreenMessage("", 8f, ScreenMessageStyle.UPPER_CENTER);
        private const string ModTag = "[RP-1 ControlLocker]";

        // For locking MJ.
        private static bool isFirstLoad = true;
        private static MethodInfo getMasterMechJeb = null;
        private static PropertyInfo mjDeactivateControl = null;
        private object masterMechJeb = null;

        private void Awake()
        {
            if (!isFirstLoad) return;
            isFirstLoad = false;

            if (AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "MechJeb2") is var mechJebAssembly &&
               Type.GetType("MuMech.MechJebCore, MechJeb2") is Type mechJebCore &&
               Type.GetType("MuMech.VesselExtensions, MechJeb2") is Type mechJebVesselExtensions)
            {
                mjDeactivateControl = mechJebCore.GetProperty("DeactivateControl", BindingFlags.Public | BindingFlags.Instance);
                getMasterMechJeb = mechJebVesselExtensions.GetMethod("GetMasterMechJeb", BindingFlags.Public | BindingFlags.Static);
            }
            if (mjDeactivateControl != null && getMasterMechJeb != null)
                Debug.Log($"{ModTag} MechJeb methods found");
            else
                Debug.Log($"{ModTag} MJ assembly or methods NOT found");
        }

        private void Start()
        {
            GameEvents.onVesselWasModified.Add(OnVesselModifiedHandler);
            GameEvents.onVesselSwitching.Add(OnVesselSwitchingHandler);
            vessel = FlightGlobals.ActiveVessel;
            if (vessel && vessel.loaded) vessel.OnPostAutopilotUpdate += FlightInputModifier;
        }

        protected void OnVesselSwitchingHandler(Vessel v1, Vessel v2)
        {
            // Apply only when switching does not result in new scene load.
            if (v1 && v2 && v2.loaded) v2.OnPostAutopilotUpdate += FlightInputModifier;
        }

        protected void OnVesselModifiedHandler(Vessel v)
        {
            requested = true;
        }

        void FlightInputModifier(FlightCtrlState state)
        {
            if (wasLocked)
            {
                state.X = state.Y = 0;                      // Disable X/Y translation, allow Z (fwd/reverse)
                state.yaw = state.pitch = state.roll = 0;   // Disable roll control
            }
        }

        public bool ShouldLock()
        {
            if (vessel != FlightGlobals.ActiveVessel)
            {
                vessel = FlightGlobals.ActiveVessel;
                masterMechJeb = null;
            }
            // if we have no active vessel, undo locks
            if (vessel is null)
                return false;

            // Do we need to update?
            double cTime = Planetarium.GetUniversalTime();
            if (requested || cTime > lastUT + updateFrequency)
            {
                lastUT = cTime;
                requested = false;
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
                    vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                    message.message = $"Insufficient Avionics, Locking Controls (supports {maxMass:N3}t, vessel {vesselMass:N3}t)";
                }
                ScreenMessages.PostScreenMessage(message);
                FlightLogger.fetch.LogEvent(message.message);
                wasLocked = doLock;
            }

            if (masterMechJeb == null && getMasterMechJeb != null)
            {
                masterMechJeb = getMasterMechJeb.Invoke(null, new object[] { FlightGlobals.ActiveVessel });
            }
            if (masterMechJeb != null && mjDeactivateControl != null)
            {
                // Update MJ every tick, to make sure it gets correct state after separation / docking.
                mjDeactivateControl.SetValue(masterMechJeb, doLock, index: null);
            }
        }

        public void OnDestroy()
        {
            InputLockManager.RemoveControlLock(lockID);
            GameEvents.onVesselWasModified.Remove(OnVesselModifiedHandler);
            GameEvents.onVesselSwitching.Remove(OnVesselSwitchingHandler);
        }
    }
}
