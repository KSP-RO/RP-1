using Strategies;
using UnityEngine;
using System.Collections.Generic;
using RP0.DataTypes;
using KerbalConstructionTime;

namespace RP0.Leaders
{
    public class CurrencyModifier : StrategyEffect
    {
        [Persistent]
        private string effectDescription = string.Empty;

        [Persistent]
        private string locStringOverride = string.Empty;

        [Persistent]
        private double multiplier = 1d;

        [Persistent]
        private PersistentListValueType<TransactionReasonsRP0> transactionReasons = new PersistentListValueType<TransactionReasonsRP0>();

        private TransactionReasonsRP0 listeningReasons = TransactionReasonsRP0.None;

        [Persistent]
        private CurrencyRP0 currency = CurrencyRP0.Invalid;

        public CurrencyModifier(Strategy parent)
            : base(parent)
        {
        }

        protected override string GetDescription()
        {
            return KSP.Localization.Localizer.Format(string.IsNullOrEmpty(locStringOverride) ? "#rp0LeaderEffectCurrencyModifier" : locStringOverride, 
                LocalizationHandler.FormatRatioAsPercent(multiplier), 
                currency.displayDescription(), 
                effectDescription);
        }

        protected override void OnLoadFromConfig(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);

            if (currency == CurrencyRP0.Invalid)
                return;

            listeningReasons = TransactionReasonsRP0.None;
            foreach (var r in transactionReasons)
                listeningReasons |= r;
        }

        protected override void OnRegister()
        {
            GameEvents.Modifiers.OnCurrencyModifierQuery.Add(OnEffectQuery);
        }

        protected override void OnUnregister()
        {
            GameEvents.Modifiers.OnCurrencyModifierQuery.Remove(OnEffectQuery);
        }

        protected void OnEffectQuery(CurrencyModifierQuery qry)
        {
            if (qry is CurrencyModifierQueryRP0 qryRP0)
            {
                if ((listeningReasons & qryRP0.reasonRP0) != 0)
                    qryRP0.Multiply(currency, multiplier);
            }
            else
            {
                if (currency <= CurrencyRP0.Reputation && (listeningReasons & qry.reason.RP0()) != 0)
                {
                    qry.AddDelta(currency.Stock(), (float)(qry.GetInput(currency.Stock()) * (multiplier - 1d)));
                }
            }
                
        }
    }
}
