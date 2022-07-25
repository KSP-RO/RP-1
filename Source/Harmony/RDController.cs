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
    [HarmonyPatch(typeof(RDController))]
    internal class PatchRDController
    {
        [HarmonyPrefix]
        [HarmonyPatch("UpdatePurchaseButton")]
        internal static bool Prefix_UpdatePurchaseButton(RDController __instance)
        {
            __instance.actionButton.gameObject.SetActive(false);
            return false;
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
}
