using ContractConfigurator.Parameters;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class ReachMach : VesselParameter
    {
        protected double mach;

        private float lastUpdate = 0.0f;
        private const float UPDATE_FREQUENCY = 0.25f;

        public ReachMach() : base(null) { }

        public ReachMach(string title, double mach) : base(title)
        {
            this.mach = mach;
            this.title = GetParameterTitle();
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("mach", mach);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            node.TryGetValue("mach", ref mach);
        }

        protected override string GetParameterTitle()
        {
            return $"Reach Mach {mach:0.##}";
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (Time.fixedTime - lastUpdate > UPDATE_FREQUENCY)
            {
                lastUpdate = Time.fixedTime;
                CheckVessel(FlightGlobals.ActiveVessel);
            }
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return vessel?.mach > mach;
        }
    }
}
