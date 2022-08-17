using HarmonyLib;
using KSP.UI.Screens.SpaceCenter.MissionSummaryDialog;
using UnityEngine.UI;
using KSP.UI.Util;
using System.Reflection;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ScienceSubjectWidget))]
    internal class PatchScienceSubjectWidget
    {
        internal static FieldInfo image = typeof(ImgText).GetField("imgComponent", AccessTools.all);

        [HarmonyPostfix]
        [HarmonyPatch("UpdateFields")]
        internal static void Postfix_UpdateFields(ScienceSubjectWidget __instance)
        {
            (image.GetValue(__instance.scienceWidgetScienceContent) as Image).gameObject.SetActive(false);
            if (!__instance.scienceWidgetScienceContent.text.StartsWith("<sprite"))
                __instance.scienceWidgetScienceContent.text = "<sprite=\"CurrencySpriteAsset\" name=\"Science\" tint=1> " + __instance.scienceWidgetScienceContent.text;
        }
    }
}