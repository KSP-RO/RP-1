using HarmonyLib;
using KSP.UI.Screens;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using KSP.UI.TooltipTypes;

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
        //[HarmonyPatch(typeof(FundsWidget))]
        //internal class PatchFundsWidget
        //{
        //    [HarmonyPostfix]
        //    [HarmonyPatch("DelayedStart")]
        //    internal static void Postfix_DelayedStart(FundsWidget __instance)
        //    {
        //        // Get tumblers, clone it.
        //        var tumblers = __instance.transform.Find("Tumblers");
        //        GameObject go = GameObject.Instantiate(tumblers.gameObject, tumblers.position, tumblers.rotation, __instance.gameObject.transform);
        //        go.name = "FundsWidgetTooltipController";
        //        // Kill Tumbler script and chidlren.
        //        GameObject.DestroyImmediate(go.GetComponent<KSP.UI.Screens.Tumbler>());
        //        GameObject[] children = new GameObject[go.transform.childCount];
        //        int i = 0;
        //        foreach (Transform child in go.transform)
        //            children[i++] = child.gameObject;
        //        foreach (var child in children)
        //            GameObject.DestroyImmediate(child);

        //        // Copy the rect transform
        //        var rt = go.GetComponent<RectTransform>();
        //        var tumblerRT = tumblers.GetComponent<RectTransform>();
        //        rt.anchoredPosition = tumblerRT.anchoredPosition;
        //        rt.anchorMin = tumblerRT.anchorMin;
        //        rt.anchorMax = tumblerRT.anchorMax;
        //        rt.offsetMin = tumblerRT.offsetMin;
        //        rt.offsetMin = tumblerRT.offsetMax;
        //        rt.sizeDelta = tumblerRT.sizeDelta;
        //        rt.anchoredPosition3D = tumblerRT.anchoredPosition3D;
        //        // but offset in -Z so it's above.
        //        rt.localPosition = new Vector3(tumblerRT.localPosition.x, tumblerRT.localPosition.y, tumblerRT.localPosition.z - 1f);
                
        //        // Add tooltip
        //        var tooltip = go.AddComponent<TooltipController_Text>();
        //        var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
        //        tooltip.prefab = prefab;
        //        tooltip.RequireInteractable = false;
        //        tooltip.textString = "blah?";
        //        Debug.Log("$$$$");
        //        __instance.gameObject.Dump();
        //    }
        //}

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

                var frameImage = (Image)__instance.gameObject.GetComponentInChildren(typeof(Image));

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

                var tooltip = __instance.gameObject.AddComponent<ReputationWidgetTooltip>();
                var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
                tooltip.prefab = prefab;
                tooltip.RequireInteractable = false;

                return true;
            }
        }

        [HarmonyPatch(typeof(ScienceWidget))]
        internal class PatchScienceWidget
        {
            [HarmonyPostfix]
            [HarmonyPatch("DelayedStart")]
            internal static void Postfix_DelayedStart(FundsWidget __instance)
            {
                var tooltip = __instance.gameObject.AddComponent<KerbalConstructionTime.ScienceWidgetTooltip>();
                var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
                tooltip.prefab = prefab;
                tooltip.RequireInteractable = false;
            }
        }
    }
}
