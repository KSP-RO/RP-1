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
                GameObject.DestroyImmediate(__instance.gauge);
                __instance.gauge = null;
                GameObject.DestroyImmediate(__instance.gameObject.transform.Find("circularGauge").gameObject);

                // Create the Confidence widget
                ConfidenceWidget.CreateConfidenceWidget(GameObject.Instantiate(__instance.gameObject, __instance.transform.parent, worldPositionStays: false));

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
    }
}
