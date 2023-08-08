using System.Collections.Generic;

namespace RP0
{
    public static class CurrencyUtils
    {
        public static TransactionReasons Stock(this TransactionReasonsRP0 reason) => (long)reason <= uint.MaxValue ? (TransactionReasons)reason : TransactionReasons.None;
        public static TransactionReasonsRP0 RP0(this TransactionReasons reason) => (TransactionReasonsRP0)reason;

        public static Currency Stock(this CurrencyRP0 c) => (Currency)c;
        public static CurrencyRP0 RP0(this Currency c) => (CurrencyRP0)c;

        public static double Funds(TransactionReasonsRP0 reason, double funds, bool includeHidden = false) => CurrencyModifierQueryRP0.RunQuery(reason, funds, 0d, 0d, 0d, 0d).GetTotal(CurrencyRP0.Funds, includeHidden);
        public static double Science(TransactionReasonsRP0 reason, double sci, bool includeHidden = false) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, sci, 0d, 0d, 0d).GetTotal(CurrencyRP0.Science, includeHidden);
        public static double Rep(TransactionReasonsRP0 reason, double rep, bool includeHidden = false) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, 0d, rep, 0d, 0d).GetTotal(CurrencyRP0.Reputation, includeHidden);
        public static double Conf(TransactionReasonsRP0 reason, double conf, bool includeHidden = false) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, 0d, 0d, conf, 0d).GetTotal(CurrencyRP0.Confidence, includeHidden);
        public static double Rate(TransactionReasonsRP0 reason, bool includeHidden = false) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, 0d, 0d, 0d, 0d).GetTotal(CurrencyRP0.Rate, includeHidden);
        public static double Time(TransactionReasonsRP0 reason, double time, bool includeHidden = false) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, 0d, 0d, 0d, time).GetTotal(CurrencyRP0.Time, includeHidden);

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
