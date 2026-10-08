using Contracts;
using ContractConfigurator;

namespace ContractConfigurator.RP0
{
    public class BoostTrainingRateFactory : BehaviourFactory
    {
        protected double multiplier;

        public override bool Load(ConfigNode node)
        {
            bool valid = base.Load(node);

            valid &= ConfigNodeUtil.ParseValue<double>(node, "multiplier", x => multiplier = x, this, 1.5, x => Validation.GT(x, 0.0));

            return valid;
        }

        public override ContractBehaviour Generate(ConfiguredContract contract)
        {
            return new BoostTrainingRate(multiplier);
        }
    }
}
