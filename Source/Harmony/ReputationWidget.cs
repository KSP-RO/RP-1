using HarmonyLib;
using KSP.Localization;
using KSP.UI.TooltipTypes;
using RP0.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ReputationWidget))]
    internal class PatchReputationWidget
    {
        public static TextMeshProUGUI RepLabel;

        [HarmonyPrefix]
        [HarmonyPatch("onReputationChanged")]
        internal static bool Prefix_onReputationChanged(float rep, TransactionReasons reason)
        {
            if (RepLabel != null)
            {
                RepLabel.text = KSPUtil.LocalizeNumber(rep, "0.0");
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("DelayedStart")]
        internal static bool Prefix_DelayedStart(ReputationWidget __instance)
        {
            Object.DestroyImmediate(__instance.gauge);
            __instance.gauge = null;
            Object.DestroyImmediate(__instance.gameObject.transform.Find("circularGauge").gameObject);

            // Create the Confidence widget
            ConfidenceWidget.CreateConfidenceWidget(Object.Instantiate(__instance.gameObject, __instance.transform.parent, worldPositionStays: false));

            var frameImage = (Image)__instance.gameObject.GetComponentInChildren(typeof(Image));
            frameImage.sprite = Sprite.Create(GameDatabase.Instance.GetTexture("RP-1/Resources/rep_background", false), frameImage.sprite.rect, frameImage.sprite.pivot);

            var img = Object.Instantiate(new GameObject("repBackground"), __instance.transform, worldPositionStays: false).AddComponent<Image>();
            img.color = new Color32(58, 58, 63, 255);
            img.rectTransform.anchorMin = frameImage.rectTransform.anchorMin;
            img.rectTransform.anchorMax = frameImage.rectTransform.anchorMax;
            img.rectTransform.anchoredPosition = frameImage.rectTransform.anchoredPosition;
            img.rectTransform.sizeDelta = ((RectTransform)__instance.gameObject.transform).sizeDelta;    // No idea why the frame image transform is larger than the component itself

            RepLabel = Object.Instantiate(new GameObject("repLabel"), __instance.transform, worldPositionStays: false).AddComponent<TextMeshProUGUI>();
            RepLabel.alignment = TextAlignmentOptions.Right;
            RepLabel.color = XKCDColors.Mustard;
            RepLabel.fontSize = 22;
            RepLabel.rectTransform.localPosition = new Vector3(-9, -1, 0);
            RepLabel.fontStyle = FontStyles.Bold;

            // Add rep tooltip
            var tooltip = __instance.gameObject.AddComponent<TooltipController_TextFunc>();
            var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
            tooltip.prefab = prefab;
            tooltip.getStringAction = GetTooltipTextRep;
            tooltip.continuousUpdate = false;

            return true;
        }

        private static string GetTooltipTextRep()
        {
            MaintenanceHandler.SubsidyDetails details = new MaintenanceHandler.SubsidyDetails();
            MaintenanceHandler.FillSubsidyDetails(ref details, Planetarium.GetUniversalTime(), Reputation.Instance.reputation);

            double repLostPerDay = -CurrencyUtils.Rep(TransactionReasonsRP0.DailyRepDecline, -Reputation.Instance.reputation * Database.SettingsSC.repPortionLostPerDay);
            double repLostPerYear = repLostPerDay * 5.25d;
            double runningRep = Reputation.Instance.reputation - repLostPerYear;
            for (int i = 0; i < 12; ++i)
            {
                double lossAmt = -CurrencyUtils.Rep(TransactionReasonsRP0.DailyRepDecline, -runningRep * Database.SettingsSC.repPortionLostPerDay) * 30d;
                runningRep -= lossAmt;
                repLostPerYear += lossAmt;
            }

            return Localizer.Format("#rp0_Widgets_Reputation_Tooltip",
                                CurrencyUtils.Funds(TransactionReasonsRP0.Subsidy, details.minSubsidy).ToString("N0"),
                                CurrencyUtils.Funds(TransactionReasonsRP0.Subsidy, details.maxSubsidy).ToString("N0"),
                                details.maxRep.ToString("N0"),
                                CurrencyUtils.Funds(TransactionReasonsRP0.Subsidy, details.subsidy).ToString("N0"),
                                repLostPerDay.ToString("N1"),
                                repLostPerYear.ToString("N0"));
        }
    }
}
