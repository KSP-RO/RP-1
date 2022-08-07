using HarmonyLib;
using KSP.UI.Screens;
using KSP.Localization;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(RDNode))]
    internal class PatchRDNode
    {
        [HarmonyPostfix]
        [HarmonyPatch("GetTooltipCaption")]
        internal static void Prefix_GetTooltipCaption(RDNode __instance, ref string __result)
        {
            if (__instance.state == RDNode.State.RESEARCHED)
            {
                __result = $"{CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.RnDTechResearch, 0f, -__instance.tech.scienceCost, 0f).GetCostLineOverride(true, true)}\n{__result}";
            }
        }
    }
}
