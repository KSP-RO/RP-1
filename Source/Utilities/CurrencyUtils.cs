using System.Collections.Generic;

namespace RP0
{
    public static class CurrencyUtils
    {
        public static TransactionReasons Stock(this TransactionReasonsRP0 reason) => (TransactionReasons)reason;
        public static TransactionReasonsRP0 RP0(this TransactionReasons reason) => (TransactionReasonsRP0)reason;

        public static Currency Stock(this CurrencyRP0 c) => (Currency)c;
        public static CurrencyRP0 RP0(this Currency c) => (CurrencyRP0)c;

        public static double Funds(TransactionReasonsRP0 reason, double funds) => CurrencyModifierQueryRP0.RunQuery(reason, funds, 0f, 0f).GetTotal(CurrencyRP0.Funds);

        public static void ProcessCurrency(TransactionReasonsRP0 reason, Dictionary<CurrencyRP0, double> dict, bool invert = false)
        {
            var stockReason = reason.Stock();
            double mul = invert ? -1d : 1d;
            foreach (var kvp in dict)
            {
                switch (kvp.Key)
                {
                    case CurrencyRP0.Funds:
                        Funding.Instance?.AddFunds(kvp.Value * mul, stockReason);
                        break;
                    case CurrencyRP0.Science:
                        ResearchAndDevelopment.Instance?.AddScience((float)(kvp.Value * mul), stockReason);
                        break;
                    case CurrencyRP0.Reputation:
                        Reputation.Instance?.AddReputation((float)(kvp.Value * mul), stockReason);
                        break;
                    case CurrencyRP0.Confidence:
                        Confidence.Instance?.AddConfidence((float)(kvp.Value * mul), stockReason);
                        break;
                }
            }
        }
    }
}
