using Contracts;

namespace ContractConfigurator.RP0
{
    public class RP1RendezvousFactory : ParameterFactory
    {
        protected double distance;
        protected double relativeSpeed;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "distance", x => distance = x, this, 100);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "relativeSpeed", x => relativeSpeed = x, this, 0);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new RP1Rendezvous(distance, relativeSpeed, title);
        }
    }
}
