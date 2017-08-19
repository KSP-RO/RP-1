using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP;

namespace RP0
{
    public class ModuleNonReentryRated : PartModule
    {
        protected PartThermalData ptd;
        protected bool recalc = true;

        public override string GetInfo()
        {
            return "Part is not rated for reentry!";
        }

        public override void OnAwake()
        {
            base.OnAwake();
            part.maximum_drag = float.MinValue;
        }
    }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class RP0ThermoChanger : MonoBehaviour
    {
        static CelestialBody body = null;
        public void Start()
        {
            print("Registering RP-0 overrides with ModularFlightIntegrator");
            ModularFI.ModularFlightIntegrator.RegisterUpdateThermodynamicsPre(UpdateThermodynamicsPre);
        }

        public static void UpdateThermodynamicsPre(ModularFI.ModularFlightIntegrator fi)
        {
            for (int i = fi.partThermalDataList.Count; i-- > 0;)
            {
                PartThermalData ptd = fi.partThermalDataList[i];
                if (ptd.part.maximum_drag == float.MinValue)
                {
                    ptd.convectionTempMultiplier = Math.Max(ptd.convectionTempMultiplier, 0.5d);
                    ptd.convectionTempMultiplier = Math.Max(ptd.convectionCoeffMultiplier, 0.5d);
                }
            }
        }
    }
}

