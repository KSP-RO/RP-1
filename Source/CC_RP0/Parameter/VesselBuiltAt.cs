using ContractConfigurator.Parameters;
using RP0;

namespace ContractConfigurator.RP0
{
    public class VesselBuiltAtParameter : VesselParameter
    {
        private EditorFacility builtAt {  get; set; }

        public VesselBuiltAtParameter()
            : base(null)
        {
        }

        public VesselBuiltAtParameter(EditorFacility builtAt, string title)
            : base(title)
        {
            this.builtAt = builtAt;
            disableOnStateChange = true;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("builtAt", builtAt);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            builtAt = ConfigNodeUtil.ParseValue<EditorFacility>(node, "builtAt");
        }

        protected override string GetParameterTitle()
        {
            return $"Vessel is built at {builtAt}";
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (PresetManager.Instance?.ActivePreset?.GeneralSettings.Enabled != true) return true; // If build times are disabled, we cannot tell where the vessel was built, so we'll be nice.

            EditorFacility? curBuiltAt = vessel.GetVesselBuiltAt();
            return curBuiltAt.HasValue && curBuiltAt.Value == builtAt;
        }
    }
}
