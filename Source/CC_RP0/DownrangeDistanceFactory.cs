using Contracts;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class DownrangeDistanceFactory : ParameterFactory
    {
        protected double distance;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "distance", x => distance = x, this, 1, x => Validation.GE(x, 1));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new DownrangeDistance(title, distance);
        }
    }
}
