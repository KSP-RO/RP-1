using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Funding))]
    internal class PatchFunding
    {
        [HarmonyPrefix]
        [HarmonyPatch("AddFunds")]
        internal static bool Prefix_AddFunds(Funding __instance, double value, TransactionReasons reason, ref double ___funds)
        {
            ___funds += value;
            CurrencyModifierQueryRP0 data = new CurrencyModifierQueryRP0(reason, value, 0f, 0f);
            GameEvents.Modifiers.OnCurrencyModifierQuery.Fire(data);
            GameEvents.Modifiers.OnCurrencyModified.Fire(data);
            if (!HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().AllowNegativeCurrency && ___funds < 0d)
                ___funds = 0d;

            GameEvents.OnFundsChanged.Fire(___funds, reason);

            return false;
        }
    }
}
