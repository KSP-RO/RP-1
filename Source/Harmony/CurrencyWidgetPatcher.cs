using HarmonyLib;
using KerbalConstructionTime;
using KSP.Localization;
using KSP.UI.TooltipTypes;
using RP0.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
        [HarmonyPatch(typeof(FundsWidget))]
        internal class PatchFundsWidget
        {
            [HarmonyPostfix]
            [HarmonyPatch("DelayedStart")]
            internal static void Postfix_DelayedStart(FundsWidget __instance)
            {
                // Get foreground element
                var foreground = __instance.transform.Find("Foreground");

                // The top level object for the widget has a Canvas component but no
                // GraphicRaycaster, we need one so OnMouseEnter/Exit events handled
                // by the tooltip are triggered.
                foreground.parent.gameObject.AddComponent<GraphicRaycaster>();

                // Add tooltip
                var tooltip = foreground.gameObject.AddComponent<TooltipController_TextFunc>();
                var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
                tooltip.RequireInteractable = false;
                tooltip.prefab = prefab;
                tooltip.getStringAction = GetTooltipText;
                tooltip.continuousUpdate = false;
            }

            private static string GetTooltipText()
            {
                return Localizer.Format("#rp0FundsWidgetTooltip",
                                        LocalizationHandler.FormatValuePositiveNegative(KCTGameStates.GetBudgetDelta(86400d), "N1"),
                                        LocalizationHandler.FormatValuePositiveNegative(KCTGameStates.GetBudgetDelta(86400d * 30d), "N0"),
                                        LocalizationHandler.FormatValuePositiveNegative(KCTGameStates.GetBudgetDelta(86400d * 365.25d), "N0"));
            }
        }

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

            internal static void CreateConfidenceWidget(GameObject confidenceWidgetObj)
            {
                confidenceWidgetObj.name = "ConfidenceWidget";

                GameObject.Destroy(confidenceWidgetObj.GetComponent<ReputationWidget>());

                var frameImage = (Image)confidenceWidgetObj.GetComponentInChildren(typeof(Image));
                frameImage.sprite = Sprite.Create(GameDatabase.Instance.GetTexture("RP-0/Resources/confidence_background", false), frameImage.sprite.rect, frameImage.sprite.pivot);

                var img = GameObject.Instantiate(new GameObject("Background"), confidenceWidgetObj.transform, worldPositionStays: false).AddComponent<Image>();
                img.color = new Color32(58, 58, 63, 255);
                img.rectTransform.anchorMin = frameImage.rectTransform.anchorMin;
                img.rectTransform.anchorMax = frameImage.rectTransform.anchorMax;
                img.rectTransform.anchoredPosition = frameImage.rectTransform.anchoredPosition;
                img.rectTransform.sizeDelta = ((RectTransform)confidenceWidgetObj.transform).sizeDelta;    // No idea why the frame image transform is larger than the component itself

                var textComp = GameObject.Instantiate(new GameObject("Text"), confidenceWidgetObj.transform, worldPositionStays: false).AddComponent<TextMeshProUGUI>();
                textComp.alignment = TextAlignmentOptions.Right;
                textComp.color = XKCDColors.KSPBadassGreen;
                textComp.fontSize = 22;
                textComp.rectTransform.localPosition = new Vector3(-9, -1, 0);
                textComp.fontStyle = FontStyles.Bold;

                var confidenceWidget = confidenceWidgetObj.AddComponent<ConfidenceWidget>();
                confidenceWidget.text = textComp;
                confidenceWidget.DelayedStart();

                // Add tooltip
                var tooltip = confidenceWidgetObj.AddComponent<TooltipController_TextFunc>();
                var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
                tooltip.prefab = prefab;
                tooltip.getStringAction = GetTooltipTextConf;
                tooltip.continuousUpdate = true;
            }

            [HarmonyPrefix]
            [HarmonyPatch("DelayedStart")]
            internal static bool Prefix_DelayedStart(ReputationWidget __instance)
            {
                GameObject.DestroyImmediate(__instance.gauge);
                __instance.gauge = null;
                GameObject.DestroyImmediate(__instance.gameObject.transform.Find("circularGauge").gameObject);

                // Create the Confidence widget
                CreateConfidenceWidget(GameObject.Instantiate(__instance.gameObject, __instance.transform.parent, worldPositionStays: false));

                var frameImage = (Image)__instance.gameObject.GetComponentInChildren(typeof(Image));
                frameImage.sprite = Sprite.Create(GameDatabase.Instance.GetTexture("RP-0/Resources/rep_background", false), frameImage.sprite.rect, frameImage.sprite.pivot);

                var img = GameObject.Instantiate(new GameObject("repBackground"), __instance.transform, worldPositionStays: false).AddComponent<Image>();
                img.color = new Color32(58, 58, 63, 255);
                img.rectTransform.anchorMin = frameImage.rectTransform.anchorMin;
                img.rectTransform.anchorMax = frameImage.rectTransform.anchorMax;
                img.rectTransform.anchoredPosition = frameImage.rectTransform.anchoredPosition;
                img.rectTransform.sizeDelta = ((RectTransform)__instance.gameObject.transform).sizeDelta;    // No idea why the frame image transform is larger than the component itself

                RepLabel = GameObject.Instantiate(new GameObject("repLabel"), __instance.transform, worldPositionStays: false).AddComponent<TextMeshProUGUI>();
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

            private static string GetTooltipTextConf()
            {
                return Localizer.Format("#rp0ConfidenceWidgetTooltip", Confidence.AllConfidenceEarned.ToString("N0"));
            }

            private static string GetTooltipTextRep()
            {
                MaintenanceHandler.SubsidyDetails details = MaintenanceHandler.Instance.GetSubsidyDetails();
                double repLostPerDay = Reputation.Instance.reputation * MaintenanceHandler.Settings.repPortionLostPerDay;
                return Localizer.Format("#rp0RepWidgetTooltip",
                                        details.minSubsidy.ToString("N0"),
                                        details.maxSubsidy.ToString("N0"),
                                        details.maxRep.ToString("N0"),
                                        details.subsidy.ToString("N0"),
                                        repLostPerDay.ToString("N1"),
                                        (repLostPerDay * 365.25d).ToString("N0"));
            }
        }

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
                return Localizer.Format("#rp0ScienceWidgetTooltip",
                                        System.Math.Max(0, KCTGameStates.SciPointsTotal).ToString("N1"),
                                        UnlockSubsidyHandler.Instance.TotalSubsidy.ToString("N0"));
            }
        }
    }
}
