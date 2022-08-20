using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Funding))]
    internal class PatchFunding
    {
        [HarmonyPrefix]
        [HarmonyPatch("AddFunds")]
        internal static bool Prefix_AddFunds(Funding __instance, double value, TransactionReasons reason)
        {
            if (value == 0d)
                return false;

            __instance.funds += value;
            CurrencyModifierQueryRP0 data = new CurrencyModifierQueryRP0(reason, value, 0f, 0f);
            GameEvents.Modifiers.OnCurrencyModifierQuery.Fire(data);
            GameEvents.Modifiers.OnCurrencyModified.Fire(data);
            if (!HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().AllowNegativeCurrency && __instance.funds < 0d)
                __instance.funds = 0d;

            GameEvents.OnFundsChanged.Fire(__instance.funds, reason);

            return false;
        }
    }
}
