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
        [HarmonyPrefix]
        [HarmonyPatch("AddPartListItem")]
        private static bool Prefix_AddPartListItem(RDPartList __instance, AvailablePart part, bool purchased)
        {
            RDPartListItem listItem = Object.Instantiate(__instance.partListItem).GetComponentInChildren<RDPartListItem>();
            if (purchased)
            {
                __instance.SetPart(listItem, true, Localizer.GetStringByTag("#autoLOC_470883"), part, null);
            }
            else
            {
                string text;
                if (Funding.Instance != null)
                {
                    var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.PartOrUpgradeUnlock, -part.entryCost, 0d, 0d);
                    text = cmq.GetCostLineOverride(true, false, false, true, false);

                    if (!cmq.CanAfford())
                    {
                        // try again, with credit
                        cmq.AddPostDelta(CurrencyRP0.Funds, System.Math.Min(-cmq.GetTotal(CurrencyRP0.Funds, false), UnlockCreditHandler.Instance.GetCreditAmount(part.TechRequired)), true);
                        if (!cmq.CanAfford())
                        {
                            // still can't afford, so use the can't afford color
                            text = cmq.GetCostLineOverride(true, false, true, true, false);
                        }
                    }

                    if (__instance.selected_node.tech.state != RDTech.State.Available)
                        text = $"<color={XKCDColors.HexFormat.LightBlueGrey}>{text}</color>";
                }
                else
                {
                    text = string.Empty;
                }
                __instance.SetPart(listItem, false, text, part, null);
            }

            __instance.scrollList.AddItem(listItem.GetComponentInParent<KSP.UI.UIListItem>());
            __instance.partListItems.Add(listItem);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("AddUpgradeListItem")]
        private static bool Prefix_AddUpgradeListItem(RDPartList __instance, PartUpgradeHandler.Upgrade upgrade, bool purchased)
        {
            try
            {
                RDPartListItem listItem = Object.Instantiate(__instance.partListItem).GetComponentInChildren<RDPartListItem>();
                AvailablePart part = PartLoader.getPartInfoByName(upgrade.partIcon);
                if (purchased)
                {
                    __instance.SetPart(listItem, true, Localizer.GetStringByTag("#autoLOC_470834"), part, upgrade);
                }
                else
                {
                    string text;
                    if (Funding.Instance != null)
                    {
                        var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.PartOrUpgradeUnlock, -upgrade.entryCost, 0d, 0d);
                        text = cmq.GetCostLineOverride(true, false, false, true, false);

                        // BUT if we can't afford normally, but can with credit let's fix the coloring.
                        if (!cmq.CanAfford())
                        {
                            cmq.AddPostDelta(CurrencyRP0.Funds, System.Math.Min(-cmq.GetTotal(CurrencyRP0.Funds, false), UnlockCreditHandler.Instance.GetCreditAmount(upgrade.techRequired)), true);
                            if (!cmq.CanAfford())
                            {
                                text = cmq.GetCostLineOverride(true, false, true, true, false);
                            }
                        }

                        if (__instance.selected_node.tech.state != RDTech.State.Available)
                            text = $"<color={XKCDColors.HexFormat.LightBlueGrey}>{text}</color>";
                    }
                    else
                    {
                        text = string.Empty;
                    }
                    __instance.SetPart(listItem, false, text, part, upgrade);
                }

                __instance.scrollList.AddItem(listItem.GetComponentInParent<KSP.UI.UIListItem>());
                __instance.partListItems.Add(listItem);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[RP-0] AddUpgradeListItem: Error adding Upgrade item " + upgrade.name + ": " + e);
            }

            return false;
        }
    }
}
