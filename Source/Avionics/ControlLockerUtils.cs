using KerbalConstructionTime;
using KSP.UI;
using System;
using System.Collections.Generic;

namespace RP0
{
    public class ControlLockerUtils
    {
        public enum LockLevel
        {
            Locked,
            Axial,
            Unlocked
        }

        public static LockLevel ShouldLock(List<Part> parts, bool countClamps, out float maxMass, out float vesselMass, out bool isLimitedByNonInterplanetary)
        {
            maxMass = vesselMass = 0f;
            isLimitedByNonInterplanetary = false;
            bool forceUnlock = false;   // Defer return until maxMass and avionicsMass are fully calculated
            bool axial = false;

            if (parts == null || parts.Count <= 0)
                return LockLevel.Unlocked;
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
                return LockLevel.Unlocked;

            Vessel vessel = parts[0].vessel;
            if (vessel != null && vessel.isEVA)
            {
                vesselMass = (float)vessel.totalMass;
                return LockLevel.Unlocked;
            }

            int crewCount = (HighLogic.LoadedSceneIsFlight) ? vessel.GetCrewCount() : CrewAssignmentDialog.Instance.GetManifest().GetAllCrew(false).Count;

            foreach (Part p in parts)
            {
                // add up mass
                float partMass = p.mass + p.resourceMass;

                // get modules
                bool cmd = false, science = false, avionics = false, clamp = false;
                float partAvionicsMass = 0f;
                double ecResource = 0;
                ModuleCommand mC = null;
                foreach (PartModule m in p.Modules)
                {
                    if (m is ModuleCommand || m is ModuleAvionics)
                        p.GetConnectedResourceTotals(PartResourceLibrary.ElectricityHashcode, out ecResource, out double _);

                    if (m is ModuleCommand && (ecResource > 0 || HighLogic.LoadedSceneIsEditor))
                    {
                        cmd = true;
                        mC = m as ModuleCommand;
                    }

                    if (m is ModuleAvionics)
                    {
                        avionics = true;
                        ModuleAvionics mA = m as ModuleAvionics;
                        if (ecResource > 0 || HighLogic.LoadedSceneIsEditor)
                        {
                            partAvionicsMass += mA.CurrentMassLimit;
                            axial |= mA.allowAxial;
                            isLimitedByNonInterplanetary |= mA.IsNearEarthAndLockedByInterplanetary;
                        }
                    }
                    else if (m is ModuleScienceCore mSC)
                    {
                        science = true;
                        if (ecResource > 0 || HighLogic.LoadedSceneIsEditor)
                        {
                            axial |= mSC.allowAxial;
                        }
                    }

                    // Assume no clamps or other launch infrastructure can be attached if vessel has left Prelaunch state
                    if (!HighLogic.LoadedSceneIsFlight || vessel.situation == Vessel.Situations.PRELAUNCH)
                    {
                        if (m is LaunchClamp)
                        {
                            clamp = true;
                            partMass = 0f;
                        }
                        else if (m is ModuleTagList t && t.HasPadInfrastructure)
                        {
                            partMass = 0f;
                        }
                        else if (p.parent != null &&
                                 p.parent.FindModuleImplementing<ModuleTagList>() is ModuleTagList parentMod &&
                                 parentMod.HasPadInfrastructure)
                        {
                            partMass = 0f;
                        }
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
                    maxMass = Math.Max(maxMass, partAvionicsMass);
            }

            if (!forceUnlock && vesselMass > maxMass)  // Lock if vessel mass is greater than controlled mass.
                return axial ? LockLevel.Axial : LockLevel.Locked;

            return LockLevel.Unlocked;
        }
    }
}
