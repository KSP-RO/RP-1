using Contracts;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// ParameterFactory wrapper for HasResource ContractParameter.
    /// </summary>
    public class RP1FixedResourceConsumptionFactory : ParameterFactory
    {
        protected double minRate;
        protected double maxRate;
        protected PartResourceDefinition resource;

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minRate", x => minRate = x, this, double.MinValue);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "maxRate", x => maxRate = x, this, double.MaxValue);
            valid &= ConfigNodeUtil.ParseValue<PartResourceDefinition>(configNode, "resource", x => resource = x, this);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new RP1FixedResourceConsumption(minRate, maxRate, resource, title);
        }
    }
}
