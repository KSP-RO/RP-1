using Contracts;

namespace ContractConfigurator.RP0
{
    public class HorizontalLandingFactory : ParameterFactory
    {
        protected double descentAngle;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "descentAngle", x => descentAngle = x, this, 2, x => Validation.GT(x, 0) && Validation.LT(x, 90));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new HorizontalLanding(title, descentAngle);
        }
    }
}
