using Strategies;
using ROUtils.DataTypes;

namespace RP0.Leaders
{
    /// <summary>
    /// Generic leader multiplier to currencies
    /// </summary>
    public class CurrencyModifier : BaseEffect
    {
        /// <summary>
        /// List of transaction reasons this effect applies to
        /// </summary>
        [Persistent]
        private PersistentListValueType<TransactionReasonsRP0> transactionReasons = new PersistentListValueType<TransactionReasonsRP0>();

        /// <summary>
        /// On load, the various reasons will be composited into this flags enum
        /// </summary>
        private TransactionReasonsRP0 listeningReasons = TransactionReasonsRP0.None;

        /// <summary>
        /// The currency affected
        /// </summary>
        [Persistent]
        private CurrencyRP0 currency = CurrencyRP0.Invalid;

        /// <summary>
        /// In some circumstances, we want to handle positive and negative currency inputs differently.
        /// In that case, when the input is negative and this is true, we'll use 2 - (multiplier)
        /// as the multiplier to use.
        /// </summary>
        [Persistent]
        private bool invertIfNegative = false;

        public CurrencyModifier(Strategy parent)
            : base(parent)
        {
        }

        protected override string DescriptionString()
        {
            return KSP.Localization.Localizer.Format(string.IsNullOrEmpty(locStringOverride) ? "#rp0_Leaders_Effect_CurrencyModifier" : locStringOverride,
                LocalizationHandler.FormatRatioAsPercent(multiplier),
                currency.displayDescription(),
                effectDescription);
        }

        public override void OnLoadFromConfig(ConfigNode node)
        {
            base.OnLoadFromConfig(node);

            if (currency == CurrencyRP0.Invalid)
                return;

            listeningReasons = TransactionReasonsRP0.None;
            foreach (var r in transactionReasons)
                listeningReasons |= r;
        }

        public override void OnRegister()
        {
            GameEvents.Modifiers.OnCurrencyModifierQuery.Add(OnEffectQuery);
        }

        public override void OnUnregister()
        {
            GameEvents.Modifiers.OnCurrencyModifierQuery.Remove(OnEffectQuery);
        }

        protected void OnEffectQuery(CurrencyModifierQuery qry)
        {
            // this is a stock event, so the CMQ passed in has a stock TransactionReasons and stock Currency.
            // So we need to call .Stock() on our currency and TR, and call .RP0() on its, as needed.

            double multToUse = multiplier;
            if (invertIfNegative && qry.GetInput(currency.Stock()) < 0d)
            {
                multToUse = 2d - multToUse;
            }

            if (qry is CurrencyModifierQueryRP0 qryRP0)
            {
                var reason = qryRP0.reasonRP0;
                // Work around a Kerbalism issue: it uses reason None for science transmission
                if (reason == TransactionReasonsRP0.None && qry.GetInput(Currency.Science) > 0f && !SpaceCenterManagement.IsRefundingScience)
                    reason = TransactionReasonsRP0.ScienceTransmission;
                if ((listeningReasons & reason) != 0)
                    qryRP0.Multiply(currency, multToUse);
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
