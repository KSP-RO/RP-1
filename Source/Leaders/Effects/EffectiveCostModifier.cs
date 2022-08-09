using Strategies;
using UnityEngine;
using System.Collections.Generic;
using RP0.DataTypes;
using KerbalConstructionTime;

namespace RP0.Leaders
{
    public class EffectiveCostModifier : BaseEffect
    {
        [Persistent]
        private PersistentHashSetValueType<string> tags = new PersistentHashSetValueType<string>();

        [Persistent]
        private PersistentHashSetValueType<string> resources = new PersistentHashSetValueType<string>();

        [Persistent]
        private bool appliesToParts = false;

        [Persistent]
        private bool appliesToVessel = false;

        public EffectiveCostModifier(Strategy parent)
            : base(parent)
        {
        }

        protected override bool IsPositive => multiplier < 1d ^ flipPositive;

        protected override string DescriptionString()
        {
            return KSP.Localization.Localizer.Format(string.IsNullOrEmpty(locStringOverride) ? "#rp0LeaderEffectIntegrationRateModifier" : locStringOverride,
                LocalizationHandler.FormatRatioAsPercent(1d/multiplier),
                effectDescription);
        }

        protected override void OnRegister()
        {
            if (appliesToParts)
                KCTEvents.ApplyPartEffectiveCostMultiplier.Add(OnEffectQueryParts);
            if (appliesToVessel)
                KCTEvents.ApplyGlobalEffectiveCostMultiplier.Add(OnEffectQuery);
        }

        protected override void OnUnregister()
        {
            if (appliesToParts)
                KCTEvents.ApplyPartEffectiveCostMultiplier.Remove(OnEffectQueryParts);
            if (appliesToVessel)
                KCTEvents.ApplyGlobalEffectiveCostMultiplier.Remove(OnEffectQuery);
        }

        protected void OnEffectQueryParts(Boxed<double> rate, IEnumerable<string> tags, Dictionary<string, double> resources, string partName) => OnEffectQuery(rate, tags, resources);

        protected void OnEffectQuery(Boxed<double> rate, IEnumerable<string> tags, Dictionary<string, double> resources)
        {
            bool found = false;
            foreach (var s in tags)
            {
                if (this.tags.Contains(s))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                foreach (string s in resources.Keys)
                {
                    if (this.resources.Contains(s))
                        break;
                }
            }

            if (found)
                rate.value *= multiplier;
        }
    }
}
