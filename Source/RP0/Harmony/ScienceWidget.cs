using HarmonyLib;
using KSP.Localization;
using KSP.UI.TooltipTypes;
using RP0.UI;

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
            double pts = SpaceCenterManagement.Instance.SciPointsTotal;
            if (pts < 0d)
                pts = 0d;
            return Localizer.Format("#rp0_Widgets_Science_Tooltip",
                                    pts.ToString("N1"),
                                    LocalizationHandler.FormatRatioAsPercent(Formula.GetScienceResearchEfficiencyMult(pts)),
                                    LocalizationHandler.FormatRatioAsPercent(Database.SettingsSC.ResearcherEfficiencyUpgrades.GetMultiplier()),
                                    UnlockCreditHandler.Instance.TotalCredit.ToString("N0"));
        }
    }
}
