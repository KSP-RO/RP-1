using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(CostWidget))]
    internal class PatchCostWidget
    {
        [HarmonyPrefix]
        [HarmonyPatch("onCostChange")]
        internal static bool Prefix_onCostChange(CostWidget __instance, float vCost)
        {
            CurrencyModifierQueryRP0 cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.VesselPurchase, -vCost, 0f, 0f);

            double delta = cmq.GetEffectDelta(CurrencyRP0.Funds, false);
            if (delta == 0d)
            {
                __instance.text.transform.localScale = __instance.textSizeDefault;
                __instance.text.text = vCost.ToString("N0");
            }
            else
            {
                vCost -= (float)delta;
                __instance.text.transform.localScale = new UnityEngine.Vector3(__instance.textSizeDefault.x * 0.625f, __instance.textSizeDefault.y * 0.75f, __instance.textSizeDefault.z);
                __instance.text.text = vCost.ToString("N0") + " " + cmq.GetEffectPercentageText(Currency.Funds, "N0", CurrencyModifierQuery.TextStyling.OnGUI_LessIsGood);
            }
            __instance.text.color = cmq.CanAfford() ? __instance.affordableColor : __instance.unaffordableColor;

            return false;
        }
    }
}
