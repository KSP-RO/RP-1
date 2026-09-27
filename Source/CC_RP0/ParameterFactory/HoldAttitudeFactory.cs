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
        protected bool ignorePitch;
        protected bool ignoreHeading;
        protected bool ignoreRoll;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "targetPitch", x => targetPitch = x, this);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "targetHeading", x => targetHeading = x, this);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "targetRoll", x => targetRoll = x, this);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "pitchTolerance", x => pitchTolerance = x, this, HoldAttitude.DEFAULT_TOLERANCE);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "headingTolerance", x => headingTolerance = x, this, HoldAttitude.DEFAULT_TOLERANCE);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "rollTolerance", x => rollTolerance = x, this, HoldAttitude.DEFAULT_TOLERANCE);
            valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "ignorePitch", x => ignorePitch = x, this, HoldAttitude.DEFAULT_IGNORE);
            valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "ignoreHeading", x => ignoreHeading = x, this, HoldAttitude.DEFAULT_IGNORE);
            valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "ignoreRoll", x => ignoreRoll = x, this, HoldAttitude.DEFAULT_IGNORE);
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, HoldAttitude.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new HoldAttitude(title, targetPitch, targetHeading, targetRoll,
                pitchTolerance, headingTolerance, rollTolerance,
                ignorePitch, ignoreHeading, ignoreRoll,
                updateFrequency);
        }
    }
}
