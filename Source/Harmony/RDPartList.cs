using HarmonyLib;
using KSP.UI.Screens;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(RDPartList))]
    internal class PatchRDParlist
    {
        internal static string InsufficientCurrencyColorText = $"<color={XKCDColors.HexFormat.BrightOrange}>";

        internal static void SetPart(RDPartListItem item, bool unlocked, string label, AvailablePart part, PartUpgradeHandler.Upgrade upgrade)
        {
            item.Setup(label, unlocked ? "unlocked" : "purchaseable", part, upgrade);
        }

        [HarmonyPrefix]
        [HarmonyPatch("AddPartListItem")]
        private static bool Prefix_AddPartListItem(RDPartList __instance, ref AvailablePart part, ref bool purchased, ref RDNode ___selected_node, ref KSP.UI.UIList ___scrollList, ref List<RDPartListItem> ___partListItems)
        {
            RDPartListItem listItem = GameObject.Instantiate(__instance.partListItem).GetComponentInChildren<RDPartListItem>();
            if (purchased)
            {
                SetPart(listItem, true, Localizer.GetStringByTag("#autoLOC_470883"), part, null);
                return false;
            }

            string text;
            if (Funding.Instance != null)
            {
                // standard stuff: get the cost line
                var cmq = CurrencyModifierQuery.RunQuery(TransactionReasons.RnDPartPurchase, -part.entryCost, 0f, 0f);
                text = cmq.GetCostLine(displayInverted: true, useCurrencyColors: false, useInsufficientCurrencyColors: true, includePercentage: true);

                // BUT if we can't afford normally, but can with subsidy, let's fix the coloring.

                if (!cmq.CanAfford())
                {
                    var cmqSubsidized = CurrencyModifierQuery.RunQuery(TransactionReasons.RnDPartPurchase, Mathf.Min(0, -part.entryCost + (float)UnlockSubsidyHandler.Instance.GetSubsidyAmount(part.TechRequired)), 0f, 0f);
                    if (cmqSubsidized.CanAfford())
                    {
                        text = text.Replace(InsufficientCurrencyColorText, string.Empty).Replace("</color>", string.Empty);
                    }
                }
                if (___selected_node.tech.state != RDTech.State.Available)
                {
                    text = $"<color={XKCDColors.HexFormat.LightBlueGrey}>{text}</color>";
                }
            }
            else
            {
                text = string.Empty;
            }
            SetPart(listItem, false, text, part, null);

            ___scrollList.AddItem(listItem.GetComponentInParent<KSP.UI.UIListItem>());
            ___partListItems.Add(listItem);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("AddUpgradeListItem")]
        private static bool Prefix_AddUpgradeListItem(RDPartList __instance, ref PartUpgradeHandler.Upgrade upgrade, ref bool purchased, ref RDNode ___selected_node, ref KSP.UI.UIList ___scrollList, ref List<RDPartListItem> ___partListItems)
        {
            RDPartListItem listItem = GameObject.Instantiate(__instance.partListItem).GetComponentInChildren<RDPartListItem>();
            AvailablePart part = PartLoader.getPartInfoByName(upgrade.partIcon);
            if (purchased)
            {
                SetPart(listItem, true, Localizer.GetStringByTag("#autoLOC_470834"), part, upgrade);
                return false;
            }

            string text;
            if (Funding.Instance != null)
            {
                // standard stuff: get the cost line
                var cmq = CurrencyModifierQuery.RunQuery(TransactionReasons.RnDPartPurchase, -upgrade.entryCost, 0f, 0f);
                text = cmq.GetCostLine(displayInverted: true, useCurrencyColors: false, useInsufficientCurrencyColors: true, includePercentage: true);

                // BUT if we can't afford normally, but can with subsidy, let's fix the coloring.
                double excessCost = Funding.Instance.Funds - upgrade.entryCost;
                if (excessCost < 0 && UnlockSubsidyHandler.Instance.GetSubsidyAmount(upgrade.techRequired) + excessCost >= 0)
                    text = text.Replace(InsufficientCurrencyColorText, string.Empty).Replace("</color>", string.Empty);
                if (___selected_node.tech.state != RDTech.State.Available)
                {
                    text = $"<color={XKCDColors.HexFormat.LightBlueGrey}>{text}</color>";
                }
            }
            else
            {
                text = string.Empty;
            }
            SetPart(listItem, false, text, part, upgrade);

            ___scrollList.AddItem(listItem.GetComponentInParent<KSP.UI.UIListItem>());
            ___partListItems.Add(listItem);

            return false;
        }
    }
}
