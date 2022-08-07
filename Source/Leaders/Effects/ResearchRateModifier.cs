using Strategies;
using UnityEngine;
using System.Collections.Generic;
using RP0.DataTypes;
using KerbalConstructionTime;

namespace RP0.Leaders
{
    public class ResearchRateModifier : StrategyEffect
    {
        [Persistent]
        private string effectDescription = string.Empty;

        [Persistent]
        private string locStringOverride = string.Empty;

        [Persistent]
        private double multiplier = 1d;

        [Persistent]
        private PersistentListValueType<NodeType> nodeTypes = new PersistentListValueType<NodeType>();

        private NodeType nodeType;

        public ResearchRateModifier(Strategy parent)
            : base(parent)
        {
        }

        protected override string GetDescription()
        {
            return KSP.Localization.Localizer.Format(string.IsNullOrEmpty(locStringOverride) ? "#rp0LeaderEffectResearchRateModifier" : locStringOverride,
                LocalizationHandler.FormatRatioAsPercent(multiplier), 
                effectDescription);
        }

        protected override void OnLoadFromConfig(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            nodeType = NodeType.None;
            foreach (var n in nodeTypes)
                nodeType |= n;
        }

        protected override void OnRegister()
        {
            KCTEvents.ApplyResearchRateMultiplier.Add(OnEffectQuery);
        }

        protected override void OnUnregister()
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
