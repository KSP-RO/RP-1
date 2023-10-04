using Contracts;

namespace ContractConfigurator.RP0
{
    public class VesselBuiltAtFactory : ParameterFactory
    {
        public EditorFacility builtAt;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue(configNode, "builtAt", x => builtAt = x, this, EditorFacility.None, (v) => v > EditorFacility.None);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new VesselBuiltAtParameter(builtAt, title);
        }
    }
}
