using Contracts;

namespace ContractConfigurator.RP0
{
    public class RP1ReturnHomeFactory : ParameterFactory
    {
        protected string landAtFacility;
        protected double maxSpeed;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue(configNode, "landAtFacility", x => landAtFacility = x, this, string.Empty);
            valid &= ConfigNodeUtil.ParseValue(configNode, "maxSpeed", x => maxSpeed = x, this, RP1ReturnHome.DefaultMaxSpeed);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new RP1ReturnHome(title, landAtFacility, maxSpeed);
        }
    }
}
