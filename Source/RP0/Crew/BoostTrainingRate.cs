using Contracts;
using ContractConfigurator;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// On contract completion, permanently multiplies the astronaut training rate (both proficiency and
    /// mission training) by <c>multiplier</c>. The bonus is stored on CrewHandler and persists with the save;
    /// it stacks multiplicatively if more than one such contract is completed.
    /// </summary>
    public class BoostTrainingRate : ContractBehaviour
    {
        protected double multiplier = 1d;

        public BoostTrainingRate() { }

        public BoostTrainingRate(double multiplier)
        {
            this.multiplier = multiplier;
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            node.TryGetValue("multiplier", ref multiplier);
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("multiplier", multiplier);
        }

        protected override void OnCompleted()
        {
            base.OnCompleted();
            global::RP0.Crew.CrewHandler.Instance?.ApplyPermanentTrainingRateBonus(multiplier);
        }
    }
}
