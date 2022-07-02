using System;
using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public class ModuleNonReentryRated : PartModule
    {
        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
                RP0ThermoChanger.Instance?.RegisterPart(part);
        }

        public override string GetInfo()
        {
            return "Part is not rated for reentry!";
        }

        protected void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsFlight)
                RP0ThermoChanger.Instance?.UnregisterPart(part);
        }
    }

    [KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]
    public class RP0ThermoChanger : MonoBehaviour
    {
        private static bool _isRegisteredWithMFI = false;
        private static readonly List<Part> _registeredNonReentryParts = new List<Part>();

        public static RP0ThermoChanger Instance { get; private set; } = null;

        public void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
        }

        public void Start()
        {
            if (!_isRegisteredWithMFI)
            {
                Debug.Log("[RP-0] Registering overrides with ModularFlightIntegrator");
                ModularFI.ModularFlightIntegrator.RegisterUpdateThermodynamicsPre(UpdateThermodynamicsPre);
                _isRegisteredWithMFI = true;
            }
        }

        public void OnDestroy()
        {
            _registeredNonReentryParts.Clear();
        }

        public static void UpdateThermodynamicsPre(ModularFI.ModularFlightIntegrator fi)
        {
            foreach (Part part in _registeredNonReentryParts)
            {
                if (part.vessel != fi.Vessel) continue;

                PartThermalData ptd = part.ptd;
                ptd.convectionTempMultiplier = Math.Max(ptd.convectionTempMultiplier, 0.75d);
                ptd.convectionCoeffMultiplier = Math.Max(ptd.convectionCoeffMultiplier, 0.75d);
                ptd.convectionAreaMultiplier = Math.Max(ptd.convectionAreaMultiplier, 0.75d);

                ptd.postShockExtTemp = UtilMath.LerpUnclamped(part.vessel.atmosphericTemperature, part.vessel.externalTemperature, ptd.convectionTempMultiplier);
                ptd.finalCoeff = part.vessel.convectiveCoefficient * ptd.convectionArea * 0.001d * part.heatConvectiveConstant * ptd.convectionCoeffMultiplier;
            }
        }

        public void RegisterPart(Part part)
        {
            _registeredNonReentryParts.Add(part);
        }

        public void UnregisterPart(Part part)
        {
            _registeredNonReentryParts.Remove(part);
        }
    }
}

