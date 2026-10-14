using Contracts;

namespace ContractConfigurator.RP0
{
    public class HorizontalLandingFactory : ParameterFactory
    {
        protected double descentAngle;
        protected double? maxSrfVel;
        protected float updateFrequency;
        protected double timeWindow;
        protected double maxAirborneTime;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "descentAngle", x => descentAngle = x, this, 2, x => Validation.GT(x, 0) && Validation.LT(x, 90));
            valid &= ConfigNodeUtil.ParseValue(configNode, "maxSrfVel", x => maxSrfVel = x, this, (double?)null);
            valid &= ConfigNodeUtil.ParseValue(configNode, "updateFrequency", x => updateFrequency = x, this, HorizontalLanding.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));
            valid &= ConfigNodeUtil.ParseValue(configNode, "timeWindow", x => timeWindow = x, this, HorizontalLanding.DEFAULT_TIME_WINDOW, x => Validation.GT(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue(configNode, "maxAirborneTime", x => maxAirborneTime = x, this, HorizontalLanding.DEFAULT_MAX_AIRBORNE_TIME, x => Validation.GT(x, 0.0));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new HorizontalLanding(title, descentAngle, maxSrfVel, updateFrequency, timeWindow, maxAirborneTime);
        }
    }
}
