using HarmonyLib;
using KSP.UI.Screens.SpaceCenter.MissionSummaryDialog;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ScienceSubjectWidget))]
    internal class PatchScienceSubjectWidget
    {
        [HarmonyPostfix]
        [HarmonyPatch("UpdateFields")]
        internal static void Postfix_UpdateFields(ScienceSubjectWidget __instance)
        {
            __instance.scienceWidgetDataContent.imgComponent.gameObject.SetActive(false);
            if (!__instance.scienceWidgetScienceContent.text.StartsWith("<sprite"))
                __instance.scienceWidgetScienceContent.text = "<sprite=\"CurrencySpriteAsset\" name=\"Science\" tint=1> " + __instance.scienceWidgetScienceContent.text;
        }
    }
}