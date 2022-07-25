using HarmonyLib;
using KerbalConstructionTime;
using KSP.UI.Screens;
using KSP.UI.Screens.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using KSP.Localization;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(PartListTooltip))]
    internal class PatchPartListTooltipSetup
    {
        internal static string InsufficientCurrencyColorText = $"<color={XKCDColors.HexFormat.BrightOrange}>";

        [HarmonyPostfix]
        [HarmonyPatch("Setup")]
        [HarmonyPatch(new Type[] { typeof(AvailablePart), typeof(Callback<PartListTooltip>), typeof(RenderTexture) })]
        internal static void Postfix_Setup1(PartListTooltip __instance, AvailablePart availablePart, bool ___requiresEntryPurchase)
        {
            PatchButtons(__instance, availablePart, null, ___requiresEntryPurchase);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Setup")]
        [HarmonyPatch(new Type[] { typeof(AvailablePart), typeof(PartUpgradeHandler.Upgrade), typeof(Callback<PartListTooltip>), typeof(RenderTexture) })]
        internal static void Postfix_Setup2(PartListTooltip __instance, AvailablePart availablePart, PartUpgradeHandler.Upgrade up, bool ___requiresEntryPurchase)
        {
            PatchButtons(__instance, null, up, ___requiresEntryPurchase);
        }

        private static void PatchButtons(PartListTooltip __instance, AvailablePart availablePart, PartUpgradeHandler.Upgrade up, bool ___requiresEntryPurchase)
        {
            if (___requiresEntryPurchase)
            {
                string techID = availablePart?.TechRequired ?? up.techRequired;
                if (KCTGameStates.TechList.Any(tech => tech.TechID == techID))
                {
                    __instance.buttonPurchaseContainer.SetActive(false);
                    __instance.costPanel.SetActive(true);
                }
                else if (__instance.buttonPurchase.gameObject.activeSelf || __instance.buttonPurchaseRed.gameObject.activeSelf)
                {
                    // First check if we can already afford; if so, bail.
                    if (__instance.buttonPurchase.gameObject.activeSelf)
                        return;

                    double eCost = availablePart?.entryCost ?? up.entryCost;
                    double funds = Funding.Instance.Funds;
                    double subsidy = UnlockSubsidyHandler.Instance.GetSubsidyAmount(techID);

                    // If we still can't afford, bail
                    if (eCost > subsidy + funds)
                        return;

                    // We need to fix state.
                    __instance.buttonPurchase.gameObject.SetActive(true);
                    __instance.buttonPurchaseCaption.gameObject.SetActive(true);
                    __instance.buttonPurchaseCaption.text = __instance.buttonPurchaseCaption.text.Replace(InsufficientCurrencyColorText, string.Empty).Replace("</color>", string.Empty);
                    __instance.buttonPurchaseRed.gameObject.SetActive(false);
                    __instance.buttonPurchaseCaptionRed.gameObject.SetActive(false);
                }
            }
        }
    }
}
