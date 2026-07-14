using Contracts;

namespace ContractConfigurator.RP0
{
    public class SustainedCruiseFactory : ParameterFactory
    {
        protected double requiredRange;
        protected double holdSeconds;
        protected double minSpeed;
        protected double maxSpeed;
        protected double minVerticalSpeed;
        protected double maxVerticalSpeed;
        protected double rateWindowSeconds;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "requiredRange", x => requiredRange = x, this, 0.0, x => Validation.GT(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "holdSeconds", x => holdSeconds = x, this, SustainedCruise.DEFAULT_HOLD, x => Validation.GT(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minSpeed", x => minSpeed = x, this, 0.0, x => Validation.GE(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "maxSpeed", x => maxSpeed = x, this, double.MaxValue);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minVerticalSpeed", x => minVerticalSpeed = x, this, double.MinValue);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "maxVerticalSpeed", x => maxVerticalSpeed = x, this, double.MaxValue);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "rateWindowSeconds", x => rateWindowSeconds = x, this, SustainedCruise.DEFAULT_RATE_WINDOW, x => Validation.GT(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, SustainedCruise.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new SustainedCruise(title, requiredRange, holdSeconds, minSpeed, maxSpeed,
                                       minVerticalSpeed, maxVerticalSpeed, rateWindowSeconds, updateFrequency);
        }
    }
}
