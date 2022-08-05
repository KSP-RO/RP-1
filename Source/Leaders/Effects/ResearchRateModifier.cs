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
        private double value = 0d;

        [Persistent]
        private PersistentListValueType<NodeType> nodeTypes = new PersistentListValueType<NodeType>();

        private NodeType nodeType;

        public ResearchRateModifier(Strategy parent)
            : base(parent)
        {
        }

        protected override string GetDescription()
        {
            return KSP.Localization.Localizer.Format("#rp0LeaderEffectResearchRateModifier", LocalizationHandler.FormatRatioAsPercent(1d + value), effectDescription);
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
                rate.value *= value;
        }
    }
}
