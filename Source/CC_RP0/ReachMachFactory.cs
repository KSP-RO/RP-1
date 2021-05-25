using Contracts;

namespace ContractConfigurator.RP0
{
    public class ReachMachFactory : ParameterFactory
    {
        protected double mach;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "mach", x => mach = x, this);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new ReachMach(title, mach);
        }
    }
}
