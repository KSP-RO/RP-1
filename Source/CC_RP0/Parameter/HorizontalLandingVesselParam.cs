using ContractConfigurator.Parameters;
using System;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class HorizontalLanding : VesselParameter
    {
        protected double glideRatio { get; set; }
        protected bool wasPreviouslyMet { get; set; }
        protected float updateFrequency { get; set; }

        private float lastUpdate = 0.0f;
        internal const float DEFAULT_UPDATE_FREQUENCY = 0.5f;

        public HorizontalLanding() : base(null)
        {
        }

        public HorizontalLanding(string title, double descentAngle, float updateFrequency)
            : base(title)
        {
            this.title = title ?? $"Land horizontally with a descent angle below {descentAngle}°";
            this.glideRatio = 1 / Math.Tan(Mathf.Deg2Rad * descentAngle);
            this.updateFrequency = updateFrequency;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("updateFrequency", updateFrequency);
            node.AddValue("glideRatio", glideRatio);
            node.AddValue("wasPreviouslyMet", wasPreviouslyMet);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
            glideRatio = ConfigNodeUtil.ParseValue<double>(node, "glideRatio");
            wasPreviouslyMet = ConfigNodeUtil.ParseValue<bool>(node, "wasPreviouslyMet");
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (!vessel.LandedOrSplashed && !vessel.packed)
            {
                wasPreviouslyMet = vessel.horizontalSrfSpeed > Math.Abs(vessel.verticalSpeed) * glideRatio;
            }

            return wasPreviouslyMet;
        }

        protected override void OnUpdate()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            base.OnUpdate();

            if (Time.fixedTime - lastUpdate > updateFrequency)
            {
                lastUpdate = Time.fixedTime;

                CheckVessel(v);
            }
        }
    }
}
