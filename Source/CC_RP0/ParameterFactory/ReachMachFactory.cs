using Contracts;

namespace ContractConfigurator.RP0
{
    public class ReachMachFactory : ParameterFactory
    {
        protected double mach;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "mach", x => mach = x, this);
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, ReachMach.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new ReachMach(title, mach, updateFrequency);
        }
    }
}
