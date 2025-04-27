using Contracts;

namespace ContractConfigurator.RP0
{
    public class RP1ReturnHomeFactory : ParameterFactory
    {
        protected string landAtFacility;
        protected double maxSpeed;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue(configNode, "landAtFacility", x => landAtFacility = x, this, string.Empty);
            valid &= ConfigNodeUtil.ParseValue(configNode, "maxSpeed", x => maxSpeed = x, this, RP1ReturnHome.DefaultMaxSpeed);
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, RP1ReturnHome.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new RP1ReturnHome(title, landAtFacility, maxSpeed, updateFrequency);
        }
    }
}
