using ContractConfigurator.Parameters;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class ReachMach : VesselParameter
    {
        protected double mach { get; set; }
        protected float updateFrequency { get; set; }

        private float lastUpdate = 0.0f;
        internal const float DEFAULT_UPDATE_FREQUENCY = 0.25f;

        public ReachMach() : base(null) { }

        public ReachMach(string title, double mach, float updateFrequency)
            : base(title)
        {
            this.mach = mach;
            this.title = GetParameterTitle();
            this.updateFrequency = updateFrequency;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("updateFrequency", updateFrequency);
            node.AddValue("mach", mach);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
            mach = ConfigNodeUtil.ParseValue<double>(node, "mach");
        }

        protected override string GetParameterTitle()
        {
            return $"Reach Mach {mach:0.##}";
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (Time.fixedTime - lastUpdate > updateFrequency)
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
