using Contracts;

namespace ContractConfigurator.RP0
{
    public class ImpactCBFactory : ParameterFactory
    {
        protected double minSrfVel;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minSrfVel", x => minSrfVel = x, this, 0, x => Validation.GE(x, 0));
            valid &= ConfigNodeUtil.ParseValue(configNode, "targetBody", x => _targetBody = x, this, (CelestialBody)null);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new ImpactCB(title, minSrfVel, _targetBody);
        }
    }
}
