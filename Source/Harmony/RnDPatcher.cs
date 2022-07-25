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

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
        [HarmonyPatch]
        internal class PatchRnDVesselRecovery
        {
            static MethodBase TargetMethod() => AccessTools.Method(typeof(ResearchAndDevelopment), "reverseEngineerRecoveredVessel", new Type[] { typeof(ProtoVessel), typeof(MissionRecoveryDialog) });

            [HarmonyPrefix]
            internal static void Prefix(ref MissionRecoveryDialog mrDialog, out float __state)
            {
                mrDialog = null; // will prevent the widget being added.

                // store old science gain mult, then set to 0 so no actual science gain
                __state = HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
                HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier = 0;
            }

            [HarmonyPostfix]
            internal static void Postfix(float __state)
            {
                // restore old science gain mult
                HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier = __state;
            }
        }

        [HarmonyPatch(typeof(ResearchAndDevelopment))]
        internal class PatchRnDPartAvailability
        {
            [HarmonyPrefix]
            [HarmonyPatch("PartTechAvailable")]
            internal static bool Prefix(AvailablePart ap, ref bool __result)
            {
                if (ResearchAndDevelopment.Instance == null)
                {
                    __result = true;
                    return false;
                }

                Dictionary<string, ProtoTechNode> protoTechNodes = GetProtoTechNodes();
                __result = PartTechAvailable(ap, protoTechNodes);

                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch("PartModelPurchased")]
            internal static bool Prefix_PartModelPurchased(AvailablePart ap, ref bool __result)
            {
                if (ResearchAndDevelopment.Instance == null)
                {
                    __result = true;
                    return false;
                }

                Dictionary<string, ProtoTechNode> protoTechNodes = GetProtoTechNodes();

                if (PartTechAvailable(ap, protoTechNodes))
                {
                    if (protoTechNodes.TryGetValue(ap.TechRequired, out ProtoTechNode ptn) &&
                        ptn.partsPurchased.Contains(ap))
                    {
                        __result = true;
                        return false;
                    }

                    __result = false;
                    return false;
                }

                __result = false;
                return false;
            }

            private static Dictionary<string, ProtoTechNode> GetProtoTechNodes()
            {
                return Traverse.Create(ResearchAndDevelopment.Instance)
                               .Field("protoTechNodes")
                               .GetValue<Dictionary<string, ProtoTechNode>>();
            }

            private static bool PartTechAvailable(AvailablePart ap, Dictionary<string, ProtoTechNode> protoTechNodes)
            {
                if (string.IsNullOrEmpty(ap.TechRequired))
                {
                    return false;
                }

                if (protoTechNodes.TryGetValue(ap.TechRequired, out ProtoTechNode ptn))
                {
                    return ptn.state == RDTech.State.Available;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(RDController))]
        internal class PatchRDController
        {
            [HarmonyPrefix]
            [HarmonyPatch("UpdatePurchaseButton")]
            internal static bool Prefix_UpdatePurchaseButton(RDController __instance)
            {
                if (KCTGameStates.TechList.Any(tech => tech.TechID == __instance.node_selected.tech.techID))
                {
                    __instance.actionButton.gameObject.SetActive(false);
                    return false;
                }

                return true;
            }

            [HarmonyPostfix]
            [HarmonyPatch("ShowNodePanel")]
            internal static void Postfix_ShowNodePanel(RDController __instance, ref RDNode node)
            {
                string techID = node.tech.techID;
                if (node.tech.state == RDTech.State.Available || KCTGameStates.TechList.Any(tech => tech.TechID == techID))
                {
                    __instance.node_description.text = Localizer.Format("#rp0UnlockSubsidyDesc",
                        UnlockSubsidyHandler.Instance.GetLocalSubsidyAmount(node.tech.techID).ToString("N0"),
                        UnlockSubsidyHandler.Instance.GetSubsidyAmount(node.tech.techID).ToString("N0"))
                        + "\n\n" + __instance.node_description.text;
                }
                else
                {
                    __instance.node_description.text = "\n\n\n" + __instance.node_description.text;
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch("Start")]
            internal static void Postfix_Start(RDController __instance)
            {
                __instance.node_description.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.75f);
            }
        }

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
}
