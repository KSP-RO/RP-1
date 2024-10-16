using Contracts;

namespace ContractConfigurator.RP0
{
    public class DownrangeDistanceFactory : ParameterFactory
    {
        protected double distance;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "distance", x => distance = x, this, 1, x => Validation.GE(x, 1));
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, DownrangeDistance.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new DownrangeDistance(title, distance, updateFrequency);
        }
    }
}
