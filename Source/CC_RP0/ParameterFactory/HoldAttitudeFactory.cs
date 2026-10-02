using ContractConfigurator.Parameters;
using Contracts;

namespace ContractConfigurator.RP0
{
    public class HoldAttitudeFactory : ParameterFactory
    {
        protected double targetPitch;
        protected double targetHeading;
        protected double targetRoll;
        protected double pitchTolerance;
        protected double headingTolerance;
        protected double rollTolerance;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "targetPitch", x => targetPitch = x, this, HoldAttitude.DEFAULT_ANGLE, x => Validation.Between(x, -90.0, 90.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "targetHeading", x => targetHeading = x, this, HoldAttitude.DEFAULT_ANGLE, x => Validation.Between(x, 0.0, 360.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "targetRoll", x => targetRoll = x, this, HoldAttitude.DEFAULT_ANGLE, x => Validation.Between(x, -180.0, 180.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "pitchTolerance", x => pitchTolerance = x, this, HoldAttitude.DEFAULT_TOLERANCE, x => Validation.GT(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "headingTolerance", x => headingTolerance = x, this, HoldAttitude.DEFAULT_TOLERANCE, x => Validation.GT(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "rollTolerance", x => rollTolerance = x, this, HoldAttitude.DEFAULT_TOLERANCE, x => Validation.GT(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, HoldAttitude.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            // Validation minimum set
            valid &= ConfigNodeUtil.AtLeastOne(configNode, new string[] { "targetPitch", "targetHeading", "targetRoll" }, this);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            HoldAttitude param = new HoldAttitude(title, targetPitch, targetHeading, targetRoll,
                pitchTolerance, headingTolerance, rollTolerance,
                updateFrequency);
            return param;
        }
    }
}
