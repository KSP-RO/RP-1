using Contracts;

namespace ContractConfigurator.RP0
{
    public class RP1HasSamplesFactory : ParameterFactory
    {
        protected string experiment;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "experiment", x => experiment = x, this);
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, RP1HasSamples.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new RP1HasSamples(experiment, title);
        }
    }
}
