using HarmonyLib;
using Strategies.Effects;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(CurrencyConverter))]
    internal class PatchCurrencyConverter
    {
        [HarmonyPrefix]
        [HarmonyPatch("OnLoadFromConfig")]
        internal static void Prefix_OnLoadFromConfig(CurrencyConverter __instance, ConfigNode node, ref Currency ___input, ref Currency ___output, ref TransactionReasons ___AffectReasons)
        {
            CurrencyRP0 cur = CurrencyRP0.Funds;
            node.TryGetEnum<CurrencyRP0>("input", ref cur, CurrencyRP0.Funds);
            ___input = (Currency)cur;

            cur = CurrencyRP0.Funds;
            node.TryGetEnum<CurrencyRP0>("output", ref cur, CurrencyRP0.Funds);
            ___output = (Currency)cur;

            // So the actual method doesn't throw
            node.RemoveValue("input");
            node.RemoveValue("output");

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
