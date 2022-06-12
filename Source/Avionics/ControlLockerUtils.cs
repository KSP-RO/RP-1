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

        private static PartResourceDefinition _electricChargeDef = null;

        public static LockLevel ShouldLock(List<Part> parts, bool countClamps, out float maxMass, out float vesselMass, out bool isLimitedByNonInterplanetary)
        {
            maxMass = vesselMass = 0f;
            isLimitedByNonInterplanetary = false;
            bool forceUnlock = false;   // Defer return until maxMass and avionicsMass are fully calculated
            bool axial = false;

            if (parts == null || parts.Count <= 0)
                return LockLevel.Unlocked;
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor) return LockLevel.Unlocked;

            int crewCount = (HighLogic.LoadedSceneIsFlight) ? parts[0].vessel.GetCrewCount() : CrewAssignmentDialog.Instance.GetManifest().GetAllCrew(false).Count;
            _electricChargeDef ??= PartResourceLibrary.Instance.GetDefinition("ElectricCharge");

            foreach (Part p in parts)
            {
                // add up mass
                float partMass = p.mass + p.GetResourceMass();

                // get modules
                bool cmd = false, science = false, avionics = false, clamp = false;
                float partAvionicsMass = 0f;
                double ecResource = 0;
                ModuleCommand mC = null;
                foreach (PartModule m in p.Modules)
                {
                    if (m is KerbalEVA)
                        forceUnlock = true;
                    if (m is ModuleCommand || m is ModuleAvionics)
                        p.GetConnectedResourceTotals(_electricChargeDef.id, out ecResource, out double _);
                    if (m is ModuleCommand && (ecResource > 0 || HighLogic.LoadedSceneIsEditor))
                    {
                        cmd = true;
                        mC = m as ModuleCommand;
                    }
                    if (m is ModuleScienceCore)
                    {
                        science = true;
                        if (ecResource > 0 || HighLogic.LoadedSceneIsEditor)
                        {
                            ModuleScienceCore mSC = m as ModuleScienceCore;
                            axial |= mSC.allowAxial;
                        }
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
                    if (m is LaunchClamp)
                    {
                        clamp = true;
                        partMass = 0f;
                    }
                    if (m is KerbalConstructionTime.ModuleTagList t && t.tags.Contains("PadInfrastructure"))
                    {
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
                    maxMass = Math.Max(maxMass, partAvionicsMass);
            }

            if (!forceUnlock && vesselMass > maxMass)  // Lock if vessel mass is greater than controlled mass.
                return axial ? LockLevel.Axial : LockLevel.Locked;

            return LockLevel.Unlocked;
        }
    }
}
