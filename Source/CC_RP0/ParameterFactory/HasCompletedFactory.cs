using System.Collections.Generic;
using Contracts;


namespace ContractConfigurator.RP0
{
    /// <summary>
    /// ParameterFactory wrapper for HasCompletedFactory ContractParameter. 
    /// </summary>
    public class HasCompletedFactory : ParameterFactory
    {
        protected List<string> contractTags = new List<string>();
        protected bool invertParameter;

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<List<string>>(configNode, "contractTag", x => contractTags = x, this);
            valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "invertParameter", x => invertParameter = x, this, false);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new HasCompleted(contractTags, invertParameter, title);
        }
    }
}
