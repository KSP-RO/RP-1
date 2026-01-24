using KSP.UI;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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

            foreach (Part p in parts)
            {
                // add up mass
                float partMass = p.mass + p.resourceMass;

                // get modules
                bool cmd = false, avionics = false, clamp = false;
                float partAvionicsMass = 0f;
                double ecResource = 0;
                foreach (PartModule m in p.Modules)
                {
                    if (m is ModuleCommand || m is ModuleAvionics)
                        p.GetConnectedResourceTotals(PartResourceLibrary.ElectricityHashcode, out ecResource, out double _);

                    if (m is ModuleCommand mC && (IsValidModuleCommand(mC) || HighLogic.LoadedSceneIsEditor))
                    {
                        cmd = true;
                    }

                    if (m is ModuleAvionics)
                    {
                        avionics = true;
                        ModuleAvionics mA = m as ModuleAvionics;
                        if (ecResource > 0 || HighLogic.LoadedSceneIsEditor)
                        {
                            partAvionicsMass += mA.CurrentMassLimit;
                            axial |= !mA.dead && mA.allowAxial;
                            isLimitedByNonInterplanetary |= mA.IsNearEarthAndLockedByInterplanetary;
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
                if ((countClamps || !clamp) && cmd && !avionics)
                    forceUnlock = true;
                if (avionics)
                    maxMass = Math.Max(maxMass, partAvionicsMass);
            }

            if (!forceUnlock && vesselMass > maxMass)  // Lock if vessel mass is greater than controlled mass.
                return axial ? LockLevel.Axial : LockLevel.Locked;

            return LockLevel.Unlocked;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidModuleCommand(ModuleCommand mC)
        {
            return mC.ModuleState == ModuleCommand.ModuleControlState.Nominal ||
                   mC.ModuleState == ModuleCommand.ModuleControlState.PartialProbe ||
                   mC.ModuleState == ModuleCommand.ModuleControlState.PartialManned;
        }
    }
}
