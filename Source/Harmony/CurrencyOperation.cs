using HarmonyLib;
using Strategies.Effects;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(CurrencyOperation))]
    internal class PatchCurrencyOperation
    {
        [HarmonyPrefix]
        [HarmonyPatch("OnLoadFromConfig")]
        internal static void Prefix_OnLoadFromConfig(CurrencyOperation __instance, ref ConfigNode node, ref Currency ___currency, ref TransactionReasons ___AffectReasons)
        {
            // We need to copy the node so we can remove values from it
            node = node.CreateCopy();

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
                    if (!System.Enum.TryParse(array[i].Trim(), out TransactionReasonsRP0 reason))
                    {
                        UnityEngine.Debug.LogError($"[RP-0] Error parsing TransactionReasonsRP0 enum value {array[i].Trim()}");
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"$$$$ Parsed {array[i].Trim()} as {reason.ToString()} with stock version {reason.Stock().ToString()}");
                        ___AffectReasons |= reason.Stock();
                    }
                }
                // No throwing!
                node.RemoveValue("AffectReasons");
            }
        }
    }
}
