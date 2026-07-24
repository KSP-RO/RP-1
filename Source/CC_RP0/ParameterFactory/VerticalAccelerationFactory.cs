using Contracts;

namespace ContractConfigurator.RP0
{
    public class VerticalAccelerationFactory : ParameterFactory
    {
        protected double minVerticalAccel;
        protected double maxVerticalAccel;
        protected double minSpeed;
        protected double maxSpeed;
        protected float updateFrequency;
        protected bool debug;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minVerticalAccel", x => minVerticalAccel = x, this, double.MinValue);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "maxVerticalAccel", x => maxVerticalAccel = x, this, double.MaxValue);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minSpeed", x => minSpeed = x, this, 0.0, x => Validation.GE(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "maxSpeed", x => maxSpeed = x, this, double.MaxValue, x => Validation.GT(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, VerticalAcceleration.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));
            valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "debug", x => debug = x, this, false);
            valid &= ConfigNodeUtil.AtLeastOne(configNode, new string[] { "minVerticalAccel", "maxVerticalAccel" }, this);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new VerticalAcceleration(title, minVerticalAccel, maxVerticalAccel, minSpeed, maxSpeed,
                                            updateFrequency, debug);
        }
    }
}
