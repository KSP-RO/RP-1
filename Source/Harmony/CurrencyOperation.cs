using HarmonyLib;
using Strategies.Effects;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(CurrencyOperation))]
    internal class PatchCurrencyOperation
    {
        [HarmonyPrefix]
        [HarmonyPatch("OnLoadFromConfig")]
        internal static void Prefix_OnLoadFromConfig(CurrencyOperation __instance, ConfigNode node, ref Currency ___currency, ref TransactionReasons ___AffectReasons)
        {
            CurrencyRP0 cur = CurrencyRP0.Funds;
            node.TryGetEnum<CurrencyRP0>("currency", ref cur, CurrencyRP0.Funds);
            ___currency = (Currency)cur;

            // So the actual method doesn't throw
            node.RemoveValue("currency");

            string reasonsStr = node.GetValue("AffectReasons");
            if(!string.IsNullOrEmpty(reasonsStr))
            {
                string[] array = reasonsStr.Split(',');
                int num = array.Length;
                for (int i = 0; i < num; i++)
                {
                    ___AffectReasons |= (TransactionReasons)System.Enum.Parse(typeof(TransactionReasonsRP0), array[i].Trim());
                }
                // No throwing!
                node.RemoveValue("AffectReasons");
            }
        }
    }
}
