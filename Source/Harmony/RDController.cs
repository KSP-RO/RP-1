using HarmonyLib;
using KerbalConstructionTime;
using KSP.UI.Screens;
using UnityEngine;
using KSP.Localization;
using static RP0.MiscUtils;

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
        internal static void Postfix_ShowNodePanel(RDController __instance, RDNode node)
        {
            string techID = node.tech.techID;
            bool showCredit = node.tech.state == RDTech.State.Available;
            int techItemIndex = -1;
            bool showProgress = !showCredit && (techItemIndex = KerbalConstructionTimeData.Instance.TechListIndex(techID)) != -1;
            showCredit |= showProgress;
            if (!KerbalConstructionTime.KerbalConstructionTime.NodeTypes.TryGetValue(node.tech.techID, out NodeType type))
                type = NodeType.None;
            string extraText = Localizer.Format("#rp0_RnD_NodeType", Localizer.Format("#rp0_RnD_NodeType_" + type.ToStringCached())) + "\n";

            if (showCredit)
            {
                extraText += Localizer.Format("#rp0_UnlockCredit_NodeInfo",
                    UnlockCreditHandler.Instance.GetCreditAmount(node.tech.techID).ToString("N0")) + "\n";
            }
            else
            {
                extraText += "\n\n";
            }

            if (showProgress)
            {
                TechItem item = KerbalConstructionTimeData.Instance.TechList[techItemIndex];
                double prevTime = 0d;
                if (KCTGameStates.Settings.UseDates)
                {
                    for (int i = 0; i < techItemIndex; ++i)
                        prevTime += KerbalConstructionTimeData.Instance.TechList[i].GetTimeLeftEst(prevTime);
                }
                extraText += Localizer.Format(item.BuildRate > 0 ? "#rp0_RnD_Progress" : "#rp0_RnD_ProgressEst",
                    (item.GetFractionComplete() * 100d).ToString("N0"),
                    DTUtils.GetColonFormattedTime(item.GetTimeLeftEst(prevTime), prevTime, flip: false, showSeconds: false)) + "\n";
            }
            if (showCredit || showProgress)
                extraText += "\n";

            __instance.node_description.text = extraText + __instance.node_description.text;

            // we could patch RDNodeList...or we could just handle this as a postfix.
            if (KerbalConstructionTimeData.Instance == null)
                return;
            for (int i = 0; i < __instance.requiresList.scrollList.list.Count; ++i)
            {
                // Items are added to this list in order from the parents so these will be in sync.
                var nodeItem = __instance.requiresList.scrollList.list[i].listItem.GetComponent<RDNodeListItem>();
                var parent = node.parents[i];
                if (parent.parent.node.state != RDNode.State.RESEARCHED && KerbalConstructionTimeData.Instance.TechListHas(parent.parent.node.tech.techID))
                {
                    nodeItem.node.SetButtonState(RDNode.State.RESEARCHED);
                    nodeItem.node.graphics.SetIconColor(XKCDColors.KSPNotSoGoodOrange); // replaces the white
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        internal static void Postfix_Start(RDController __instance)
        {
            __instance.node_description.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.75f);
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdatePanel")]
        internal static bool Prefix_UpdatePanel(RDController __instance)
        {
            if (KerbalConstructionTimeData.Instance.TechListHas(__instance.node_selected.tech.techID))
            {
                __instance.node_inPanel.SetButtonState(RDNode.State.RESEARCHED);
                __instance.node_inPanel.SetIconState(__instance.node_selected.icon);
                __instance.node_inPanel.graphics.SetIconColor(XKCDColors.KSPNotSoGoodOrange); // replaces the white
                __instance.actionButtonText.text = Localizer.GetStringByTag("#rp0_RnD_CancelResearch");
                __instance.actionButton.gameObject.SetActive(value: true);
                __instance.actionButton.Enable(enable: true);
                __instance.actionButton.SetState("purchase");
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ActionButtonClick")]
        internal static bool Prefix_ActionButtonClick(RDController __instance)
        {
            if (KerbalConstructionTimeData.Instance.TechListHas(__instance.node_selected.tech.techID))
            {
                KCT_GUI.CancelTechNode(KerbalConstructionTimeData.Instance.TechList.FindIndex(t => t.techID == __instance.node_selected.tech.techID));
                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        internal static void Postfix_Awake(RDController __instance)
        {
            foreach (var state in __instance.actionButton.states)
            {
                if (state.name != "purchase")
                    continue;

                state.normal = ReplaceSprite(state.normal, "RP-1/Resources/research_cancel_normal", SpriteMeshType.FullRect);
                state.pressed = ReplaceSprite(state.pressed, "RP-1/Resources/research_cancel_pressed", SpriteMeshType.FullRect);
                state.highlight = ReplaceSprite(state.highlight, "RP-1/Resources/research_cancel_highlight", SpriteMeshType.FullRect);
            }
        }
    }
}
