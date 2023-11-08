using Strategies;
using ROUtils.DataTypes;

namespace RP0.Leaders
{
    /// <summary>
    /// Leader effect to add multiplier to research rate for given node type(s)
    /// </summary>
    public class ResearchRateModifier : BaseEffect
    {
        [Persistent]
        private PersistentListValueType<NodeType> nodeTypes = new PersistentListValueType<NodeType>();

        private NodeType nodeType;

        public ResearchRateModifier(Strategy parent)
            : base(parent)
        {
        }

        protected override string DescriptionString()
        {
            return KSP.Localization.Localizer.Format(string.IsNullOrEmpty(locStringOverride) ? "#rp0_Leaders_Effect_ResearchRateModifier" : locStringOverride,
                LocalizationHandler.FormatRatioAsPercent(multiplier), 
                effectDescription);
        }

        public override void OnLoadFromConfig(ConfigNode node)
        {
            base.OnLoadFromConfig(node);

            nodeType = NodeType.None;
            foreach (var n in nodeTypes)
                nodeType |= n;
        }

        public override void OnRegister()
        {
            SCMEvents.ApplyResearchRateMultiplier.Add(OnEffectQuery);
        }

        public override void OnUnregister()
        {
            SCMEvents.ApplyResearchRateMultiplier.Remove(OnEffectQuery);
        }

        protected void OnEffectQuery(Boxed<double> rate, NodeType type, string nodeID)
        {
            if ((nodeType & type) != 0)
                rate.value *= multiplier;
        }
    }
}
