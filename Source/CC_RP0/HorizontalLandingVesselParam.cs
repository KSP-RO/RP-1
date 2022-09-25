using ContractConfigurator.Parameters;
using System;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class HorizontalLanding : VesselParameter
    {
        protected double glideRatio;
        protected bool wasPreviouslyMet = false;

        private float lastUpdate = 0.0f;
        private const float UPDATE_FREQUENCY = 0.5f;

        public HorizontalLanding() : base(null)
        {
        }

        public HorizontalLanding(string title, double descentAngle) : base(title)
        {
            this.title = title ?? $"Land horizontally with a descent angle below {descentAngle}°";
            this.glideRatio = 1 / Math.Tan(Mathf.Deg2Rad * descentAngle);
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("glideRatio", glideRatio);
            node.AddValue("wasPreviouslyMet", wasPreviouslyMet);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            node.TryGetValue("glideRatio", ref glideRatio);
            node.TryGetValue("wasPreviouslyMet", ref wasPreviouslyMet);
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

            if (Time.fixedTime - lastUpdate > UPDATE_FREQUENCY)
            {
                lastUpdate = Time.fixedTime;

                CheckVessel(v);
            }
        }
    }
}
