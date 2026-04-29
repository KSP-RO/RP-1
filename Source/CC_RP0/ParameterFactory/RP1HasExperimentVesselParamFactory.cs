using Contracts;

namespace ContractConfigurator.RP0
{
    public class RP1HasExperimentVesselParamFactory : ParameterFactory
    {
        protected string experiment;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "experiment", x => experiment = x, this);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new RP1HasExperimentVesselParam(experiment, title);
        }
    }
}
