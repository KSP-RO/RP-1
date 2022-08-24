using Strategies;
using UnityEngine;
using System.Collections.Generic;
using RP0.DataTypes;
using KerbalConstructionTime;

namespace RP0.Leaders
{
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
            KCTEvents.ApplyResearchRateMultiplier.Add(OnEffectQuery);
        }

        public override void OnUnregister()
        {
            KCTEvents.ApplyResearchRateMultiplier.Remove(OnEffectQuery);
        }

        protected void OnEffectQuery(Boxed<double> rate, NodeType type, string nodeID)
        {
            if ((nodeType & type) != 0)
                rate.value *= multiplier;
        }
    }
}
