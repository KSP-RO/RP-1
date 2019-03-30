using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP;

namespace RP0
{
    public class ModuleNonReentryRated : PartModule
    {
        public override string GetInfo()
        {
            return "Part is not rated for reentry!";
        }
    }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class RP0ThermoChanger : MonoBehaviour
    {
        public void Start()
        {
            print("Registering RP-0 overrides with ModularFlightIntegrator");
            ModularFI.ModularFlightIntegrator.RegisterUpdateThermodynamicsPre(UpdateThermodynamicsPre);
        }

        protected static HashSet<Part> parts = new HashSet<Part>();
        protected static Vessel lastVessel = null;

        public static void UpdateThermodynamicsPre(ModularFI.ModularFlightIntegrator fi)
        {
            if (lastVessel != fi.Vessel || parts.Count != fi.partThermalDataList.Count)
            {
                parts.Clear();
                lastVessel = fi.Vessel;

                for (int i = fi.partThermalDataList.Count; i-- > 0;)
                {
                    var ptd = fi.partThermalDataList[i];
                    var part = ptd.part;
                    if (part.GetComponent<ModuleNonReentryRated>())
                        parts.Add(part);
                }
            }

            for (int i = fi.partThermalDataList.Count; i-- > 0;)
            {
                PartThermalData ptd = fi.partThermalDataList[i];
                var part = ptd.part;
                if(parts.Contains(part))
                {
                    ptd.convectionTempMultiplier = Math.Max(ptd.convectionTempMultiplier, 0.75d);
                    ptd.convectionCoeffMultiplier = Math.Max(ptd.convectionCoeffMultiplier, 0.75d);
                    ptd.convectionAreaMultiplier = Math.Max(ptd.convectionAreaMultiplier, 0.75d);

                    ptd.postShockExtTemp = UtilMath.LerpUnclamped(part.vessel.atmosphericTemperature, part.vessel.externalTemperature, ptd.convectionTempMultiplier);
                    ptd.finalCoeff = part.vessel.convectiveCoefficient * ptd.convectionArea * 0.001d * part.heatConvectiveConstant * ptd.convectionCoeffMultiplier;
                }
            }
        }
    }
}

