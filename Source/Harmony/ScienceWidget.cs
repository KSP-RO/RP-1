using HarmonyLib;
using KerbalConstructionTime;
using KSP.Localization;
using KSP.UI.TooltipTypes;
using RP0.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ScienceWidget))]
    internal class PatchScienceWidget
    {
        [HarmonyPostfix]
        [HarmonyPatch("DelayedStart")]
        internal static void Postfix_DelayedStart(FundsWidget __instance)
        {
            var tooltip = __instance.gameObject.AddComponent<TooltipController_TextFunc>();
            var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
            tooltip.prefab = prefab;
            tooltip.getStringAction = GetTooltipText;
            tooltip.continuousUpdate = true;
        }

        private static string GetTooltipText()
        {
            double pts = KerbalConstructionTimeData.Instance.SciPointsTotal;
            if (pts < 0d)
                pts = 0d;
            return Localizer.Format("#rp0_Widgets_Science_Tooltip",
                                    pts.ToString("N1"),
                                    LocalizationHandler.FormatRatioAsPercent(Formula.GetScienceResearchEfficiencyMult(pts)),
                                    LocalizationHandler.FormatRatioAsPercent(PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherEfficiencyUpgrades.GetMultiplier()),
                                    UnlockCreditHandler.Instance.TotalCredit.ToString("N0"));
        }
    }
}
