using ContractConfigurator.Parameters;
using Contracts;

namespace ContractConfigurator.RP0
{
    public class AvionicsCheckFactory : ParameterFactory
    {
        protected bool continuousControlRequired;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            // Load parameter options
            valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "continuousControlRequired", x => continuousControlRequired = x, this, false);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new AvionicsCheckParameter(title, continuousControlRequired);
        }
    }
}