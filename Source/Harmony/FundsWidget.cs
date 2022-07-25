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
    }
}
