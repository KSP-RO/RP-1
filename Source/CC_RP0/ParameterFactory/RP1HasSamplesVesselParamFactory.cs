using Contracts;

namespace ContractConfigurator.RP0
{
    public class RP1HasSamplesVesselParamFactory : ParameterFactory
    {
        protected string experiment;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "experiment", x => experiment = x, this, "RP0telemetry1");

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new RP1HasSamplesVesselParam(experiment, title);
        }
    }
}
