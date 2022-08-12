using System;
using System.Collections.Generic;
using UniLinq;

namespace ContractConfigurator.RP0
{
    public class AcceptContractBehaviourFactory : BehaviourFactory
    {
        protected string ccType;
        protected ContractEventType eventType;

        public override bool Load(ConfigNode node)
        {
            bool valid = base.Load(node);

            valid &= ConfigNodeUtil.ParseValue<string>(node, "contractType", x => ccType = x, this);
            valid &= ConfigNodeUtil.ParseValue<ContractEventType>(node, "eventType", x => eventType = x, this);

            return valid;
        }

        public override ContractBehaviour Generate(ConfiguredContract contract)
        {
            return new AcceptContractBehaviour(ccType, eventType);
        }
    }
}
