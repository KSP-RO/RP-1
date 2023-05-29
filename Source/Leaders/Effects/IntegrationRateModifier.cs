using Strategies;
using UnityEngine;
using System.Collections.Generic;
using RP0.DataTypes;
using KerbalConstructionTime;

namespace RP0.Leaders
{
    /// <summary>
    /// Leader effect to add multiplier to integration rate for parts or whole-vessel
    /// </summary>
    public class IntegrationRateModifier : BaseEffect
    {
        /// <summary>
        /// The KCT Module Tags to look for
        /// </summary>
        [Persistent]
        private PersistentHashSetValueType<string> tags = new PersistentHashSetValueType<string>();

        /// <summary>
        /// The resources to look for
        /// </summary>
        [Persistent]
        private PersistentDictionaryValueTypes<string, double> resources = new PersistentDictionaryValueTypes<string, double>();

        // DO NOT set both of these.

        [Persistent]
        private bool appliesToParts = false;

        [Persistent]
        private bool appliesToVessel = false;

        public IntegrationRateModifier(Strategy parent)
            : base(parent)
        {
        }

        protected override string DescriptionString()
        {
            return KSP.Localization.Localizer.Format(string.IsNullOrEmpty(locStringOverride) ? "#rp0_Leaders_Effect_IntegrationRateModifier" : locStringOverride,
                LocalizationHandler.FormatRatioAsPercent(multiplier),
                effectDescription);
        }

        public override void OnRegister()
        {
            if (appliesToParts)
                KCTEvents.ApplyPartEffectiveCostMultiplier.Add(OnEffectQueryParts);
            if (appliesToVessel)
                KCTEvents.ApplyGlobalEffectiveCostMultiplier.Add(OnEffectQuery);
        }

        public override void OnUnregister()
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
                foreach (var kvp in resources)
                {
                    if (this.resources.TryGetValue(kvp.Key, out double amount) && kvp.Value >= amount)
                    {
                        found = true;
                        break;
                    }
                }
            }

            // We store the multiplier as a rate, but what we're returning is the multiplier
            // to the _cost_ in BP
            if (found)
                rate.value /= multiplier;
        }
    }
}
