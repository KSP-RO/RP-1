using System.Collections.Generic;
using ContractConfigurator.Parameters;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class dVExpended : VesselParameter
    {
        protected double requiredDV { get; set; }
        protected float updateFrequency { get; set; }

        private double lastUpdate = 0f;
        private double lastLog = 0f;
        private double accumulatedDV = 0.0;
        private bool started = false;
        private bool met = false;

        // Engine cache to avoid walking part/module lists every update; rebuilt when vessel/part count changes
        private int lastPartCount = -1;
        private uint lastVesselPersistentId = 0;
        private readonly List<ModuleEngines> engineCache = new List<ModuleEngines>();

        internal const float DEFAULT_UPDATE_FREQUENCY = 1.0f;

        public dVExpended() : base(null) { }

        public dVExpended(string title, double requiredDV, float updateFrequency)
            : base(title)
        {
            this.requiredDV = requiredDV;
            this.updateFrequency = updateFrequency;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("requiredDV", requiredDV);
            node.AddValue("updateFrequency", updateFrequency);
            node.AddValue("accumulatedDV", accumulatedDV);
            node.AddValue("started", started);
            node.AddValue("met", met);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            requiredDV = ConfigNodeUtil.ParseValue<double>(node, "requiredDV");
            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
            accumulatedDV = ConfigNodeUtil.ParseValue<double>(node, "accumulatedDV", 0.0);
            started = ConfigNodeUtil.ParseValue<bool>(node, "started", false);
            met = ConfigNodeUtil.ParseValue<bool>(node, "met", false);
        }

        protected override string GetParameterTitle()
        {
            return $"dV expended >= {requiredDV:N0} m/s ({accumulatedDV:N1}/{requiredDV:N0} m/s)";
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return met;
        }

        protected override void OnUpdate()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            if (!CanCheckVesselMeetsCondition(v)) return;
            if (v.situation == Vessel.Situations.PRELAUNCH) return;

            base.OnUpdate();           
            
            double now = Time.fixedTime;
            double dt = now - lastUpdate;
            lastUpdate = now;
            
            bool gap = dt > updateFrequency * 4.0 || dt < 0;   // pause / warp / first tick
            // Rebuild engine cache when vessel or part count changes
            bool vesselChanged = v.persistentId != lastVesselPersistentId;
            if (vesselChanged || v.parts.Count != lastPartCount || gap)
            {
                engineCache.Clear();
                var enginesList = v.FindPartModulesImplementing<ModuleEngines>();
                if (enginesList != null)
                {
                    engineCache.AddRange(enginesList);
                }
                lastVesselPersistentId = v.persistentId;
                lastPartCount = v.parts.Count;
            }

            // Sum engine thrust using the engine's current thrust value when available
            double totalThrust = 0.0;
            foreach (var e in engineCache)
            {
                if (e == null) continue;
                double thrustForEngine = e.finalThrust; // finalThrust is kN

                totalThrust += thrustForEngine;
            }

            double mass = v.GetTotalMass(); // mass in tons
            if (mass <= 0.0)
            {
                Debug.Log($"CC_RP0 dVExpended: mass unexpectedly zero");
                return;
            }

            // acceleration (m/s^2) approximated as totalThrust(kN) / mass(t) -> m/s^2
            double accel = totalThrust / mass;

            // Start measuring when thrust is being produced, reset when thrust is zero (e.g., coast phase)
            if (!started && totalThrust > 1e-6)
            {
                started = true;
                accumulatedDV = 0.0;
                return;
            }

            // Integrate acceleration over dt to get delta-v increment
            if (dt > 0.0 && accel > 0.0)
            {
                accumulatedDV += accel * dt;
            }

            if (Time.fixedTime - lastLog < updateFrequency) return;
            lastLog = Time.fixedTime;

            // Log thrust, mass, acceleration and accumulated dV for debugging
            Debug.Log($"CC_RP0 dVExpended: thrust={totalThrust:N2} kN mass={mass:N3} t accel={accel:N4} m/s^2 accumulatedDV={accumulatedDV:N3} m/s dt={dt:N3} started={started}");

            if (accumulatedDV >= requiredDV)
            {
                met = true;
            }

            CheckVessel(v);
            GetTitle();
        }
    }
}
