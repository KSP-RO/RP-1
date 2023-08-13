using Contracts;
using System.Collections.Generic;
using System.Linq;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// ParameterFactory wrapper for NoDocking ContractParameter. 
    /// </summary>
    public class RP1NoDockingFactory : ParameterFactory
    {
        protected bool failContract;
        protected List<VesselIdentifier> vessels;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "failContract", x => failContract = x, this, true);
            valid &= ConfigNodeUtil.ParseValue(configNode, "vessel", x => vessels = x, this, new List<VesselIdentifier>());

            // Validate using the config node instead of the list as it may undergo deferred loading
            if (parent is VesselParameterGroupFactory)
            {
                if (configNode.GetValues("vessel").Count() > 1)
                {
                    LoggingUtil.LogError(this, "{0}: When used under a VesselParameterGroup, no more than one vessel may be specified for the NoDocking parameter.", ErrorPrefix());
                    valid = false;
                }
            }
            else
            {
                if (configNode.GetValues("vessel").Count() == 0)
                {
                    LoggingUtil.LogError(this, "{0}: Need at least one vessel specified for the NoDocking parameter.", ErrorPrefix());
                    valid = false;
                }
                if (configNode.GetValues("vessel").Count() > 2)
                {
                    LoggingUtil.LogError(this, "{0}: Cannot specify more than two vessels for the NoDocking parameter.", ErrorPrefix());
                    valid = false;
                }
            }

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new RP1NoDocking(failContract, vessels.Select(vi => vi.identifier), title);
        }
    }
}
