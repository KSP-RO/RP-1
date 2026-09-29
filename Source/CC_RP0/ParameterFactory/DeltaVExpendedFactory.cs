using Contracts;

namespace ContractConfigurator.RP0
{
    public class DeltaVExpendedFactory : ParameterFactory
    {
        protected double requiredDV;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "requiredDV", x => requiredDV = x, this, 0.0, x => Validation.GT(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, dVExpended.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new dVExpended(title, requiredDV, updateFrequency);
        }
    }
}
