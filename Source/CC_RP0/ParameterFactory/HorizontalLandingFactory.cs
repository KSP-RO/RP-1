using Contracts;

namespace ContractConfigurator.RP0
{
    public class HorizontalLandingFactory : ParameterFactory
    {
        protected double descentAngle;
        protected double? maxSrfVel;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "descentAngle", x => descentAngle = x, this, 2, x => Validation.GT(x, 0) && Validation.LT(x, 90));
            valid &= ConfigNodeUtil.ParseValue(configNode, "maxSrfVel", x => maxSrfVel = x, this, (double?)null);
            valid &= ConfigNodeUtil.ParseValue(configNode, "updateFrequency", x => updateFrequency = x, this, HorizontalLanding.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new HorizontalLanding(title, descentAngle, maxSrfVel, updateFrequency);
        }
    }
}
