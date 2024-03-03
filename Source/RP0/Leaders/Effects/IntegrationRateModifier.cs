using Strategies;
using UnityEngine;
using System.Collections.Generic;
using ROUtils.DataTypes;

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

        // DO NOT set both of these!
        // And resources can be used only in the Vessel case!

        [Persistent]
        private bool appliesToParts = false;

        [Persistent]
        private bool appliesToVessel = false;

        public IntegrationRateModifier(Strategy parent)
            : base(parent)
        {
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (appliesToParts && appliesToVessel)
                RP0Debug.LogError("Tried to load IntegrationRateModifier but it applies to both parts and the vessel. Node:\n" + node.ToString());

            if(appliesToParts && resources.Count > 0)
                RP0Debug.LogError("Tried to load IntegrationRateModifier but it applies to parts and has resources defined. Node:\n" + node.ToString());
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
                SCMEvents.ApplyPartEffectiveCostMultiplier.Add(OnEffectQueryParts);
            if (appliesToVessel)
                SCMEvents.ApplyGlobalEffectiveCostMultiplier.Add(OnEffectQuery);
        }

        public override void OnUnregister()
        {
            if (appliesToParts)
                SCMEvents.ApplyPartEffectiveCostMultiplier.Remove(OnEffectQueryParts);
            if (appliesToVessel)
                SCMEvents.ApplyGlobalEffectiveCostMultiplier.Remove(OnEffectQuery);
        }

        protected void OnEffectQueryParts(Boxed<double> rate, IEnumerable<string> tags) => OnEffectQuery(rate, tags, null);

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

            if (!found && resources != null)
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
