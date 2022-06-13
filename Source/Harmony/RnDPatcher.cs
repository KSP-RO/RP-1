using HarmonyLib;
using KerbalConstructionTime;
using KSP.UI.Screens;
using KSP.UI.Screens.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
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
        [HarmonyPatch("UpdatePurchaseButton")]
        internal class PatchRnDUpdatePurchaseButton
        {
            internal static bool Prefix(RDController __instance)
            {
                if (KCTGameStates.TechList.Any(tech => tech.TechID == __instance.node_selected.tech.techID))
                {
                    __instance.actionButton.gameObject.SetActive(false);
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PartListTooltip))]
        internal class PatchPartListTooltipSetup
        {
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
                if (___requiresEntryPurchase && KCTGameStates.TechList.Any(tech => tech.TechID == (availablePart?.TechRequired ?? up.techRequired)))
                {
                    __instance.buttonPurchaseContainer.SetActive(false);
                    __instance.costPanel.SetActive(true);
                }
            }
        }
    }
}
