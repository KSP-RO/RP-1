using HarmonyLib;
using KerbalConstructionTime;
using KSP.UI.TooltipTypes;
using KSP.UI.Screens.Editor;
using System;
using System.Linq;
using UnityEngine;
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
            SetTooltip(null, null);

            if (___requiresEntryPurchase)
            {
                string techID;
                float eCost;
                if (up != null)
                {
                    techID = up.techRequired;
                    eCost = up.entryCost;
                }
                else
                {
                    techID = availablePart.TechRequired;
                    eCost = availablePart.entryCost;
                }
                if (KCTGameStates.TechList.Any(tech => tech.TechID == techID))
                {
                    __instance.buttonPurchaseContainer.SetActive(false);
                    __instance.costPanel.SetActive(true);
                }
                else if (__instance.buttonPurchase.gameObject.activeSelf || __instance.buttonPurchaseRed.gameObject.activeSelf)
                {
                    var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.RnDPartPurchase, -eCost, 0d, 0d);
                    cmq.AddDeltaAuthorized(CurrencyRP0.Funds, Math.Min(-cmq.GetTotal(CurrencyRP0.Funds), UnlockSubsidyHandler.Instance.GetSubsidyAmount(techID)));
                    // If we still can't afford, bail without setting tooltip
                    if (!cmq.CanAfford())
                        return;

                    // We might need to fix state
                    if (__instance.buttonPurchaseRed.gameObject.activeSelf)
                    {
                        __instance.buttonPurchase.gameObject.SetActive(true);
                        __instance.buttonPurchaseCaption.gameObject.SetActive(true);
                        __instance.buttonPurchaseCaption.text = __instance.buttonPurchaseCaption.text.Replace(InsufficientCurrencyColorText, string.Empty).Replace("</color>", string.Empty);
                        __instance.buttonPurchaseRed.gameObject.SetActive(false);
                        __instance.buttonPurchaseCaptionRed.gameObject.SetActive(false);
                    }

                    SetTooltip(__instance.buttonPurchase, cmq);
                }
            }
        }

        private static void SetTooltip(UnityEngine.UI.Button button, CurrencyModifierQuery cmq)
        {
            UnlockSubsidyUtility.Button = button;
            if(button != null)
                UnlockSubsidyUtility.TooltipText = Localizer.Format("#rp0UnlockSubsidyCostAfter", -cmq.GetTotal(Currency.Funds));
        }
    }
}
