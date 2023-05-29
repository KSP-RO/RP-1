using System.Collections.Generic;

namespace RP0
{
    public static class CurrencyUtils
    {
        public static TransactionReasons Stock(this TransactionReasonsRP0 reason) => (long)reason < int.MaxValue ? (TransactionReasons)reason : TransactionReasons.None;
        public static TransactionReasonsRP0 RP0(this TransactionReasons reason) => (TransactionReasonsRP0)reason;

        public static Currency Stock(this CurrencyRP0 c) => (Currency)c;
        public static CurrencyRP0 RP0(this Currency c) => (CurrencyRP0)c;

        public static double Funds(TransactionReasonsRP0 reason, double funds) => CurrencyModifierQueryRP0.RunQuery(reason, funds, 0d, 0d, 0d, 0d).GetTotal(CurrencyRP0.Funds);
        public static double Science(TransactionReasonsRP0 reason, double sci) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, sci, 0d, 0d, 0d).GetTotal(CurrencyRP0.Science);
        public static double Rep(TransactionReasonsRP0 reason, double rep) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, 0d, rep, 0d, 0d).GetTotal(CurrencyRP0.Reputation);
        public static double Conf(TransactionReasonsRP0 reason, double conf) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, 0d, 0d, conf, 0d).GetTotal(CurrencyRP0.Confidence);
        public static double Rate(TransactionReasonsRP0 reason) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, 0d, 0d, 0d, 0d).GetTotal(CurrencyRP0.Rate);
        public static double Time(TransactionReasonsRP0 reason, double time) => CurrencyModifierQueryRP0.RunQuery(reason, 0d, 0d, 0d, 0d, time).GetTotal(CurrencyRP0.Time);

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
