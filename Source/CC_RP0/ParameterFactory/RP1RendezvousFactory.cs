using Contracts;

namespace ContractConfigurator.RP0
{
    public class RP1RendezvousFactory : ParameterFactory
    {
        protected double distance;
        protected double relativeSpeed;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "distance", x => distance = x, this, 100);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "relativeSpeed", x => relativeSpeed = x, this, 0);
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, RP1Rendezvous.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new RP1Rendezvous(distance, relativeSpeed, title, updateFrequency);
        }
    }
}
