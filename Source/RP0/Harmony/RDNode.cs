using HarmonyLib;
using KSP.UI.Screens;
using System.Collections.Generic;
using KSP.Localization;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(RDNode))]
    internal class PatchRDNode
    {
        internal static readonly string _ColorGreen = "<color=" + XKCDColors.HexFormat.KSPBadassGreen + ">";
        internal static readonly string _ColorOrange = "<color=" + XKCDColors.HexFormat.Orange + ">";
        internal static readonly string _ColorGray = "<color=" + XKCDColors.HexFormat.KSPNeutralUIGrey + ">";
        
        [HarmonyPrefix]
        [HarmonyPatch("GetTooltipCaption")]
        internal static bool Prefix_GetTooltipCaption(RDNode __instance, out string __result)
        {
            if (__instance.tech == null)
            {
                __result = string.Empty;
                return false;
            }

            CurrencyModifierQueryRP0 cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.RnDTechResearch, 0d, -__instance.tech.scienceCost, 0d);
            if (SpaceCenterManagement.Instance?.TechListHas(__instance.tech.techID) == true)
            {
                __result = cmq.GetCostLineOverride(true, true, false) + "\n" + _ColorGreen + Localizer.GetStringByTag("#rp0_RnD_TechResearching") + "</color>";
            }
            else
            {
                switch (__instance.state)
                {
                    case RDNode.State.RESEARCHED:
                        __result = cmq.GetCostLineOverride(true, true, false) + "\n" + _ColorGreen + Localizer.GetStringByTag("#autoLOC_469953") + "</color>";
                        break;
                    case RDNode.State.RESEARCHABLE:
                        if (cmq.CanAfford())
                            __result = cmq.GetCostLineOverride(true, true);
                        else
                            __result = _ColorOrange + cmq.GetCostLineOverride() + Localizer.Format("#autoLOC_469963") + "</color>";
                        break;
                    default:
                        __result = _ColorGray + cmq.GetCostLineOverride() + "</color>";
                        break;
                    case RDNode.State.FADED:
                        if (__instance.tech.scienceCost > __instance.controller.ScienceCap)
                            __result = _ColorOrange + cmq.GetCostLineOverride() + " " + Localizer.Format("#autoLOC_469970", __instance.controller.ScienceCap.ToString("N0")) + "</color>";
                        else
                            __result = _ColorOrange + cmq.GetCostLineOverride() + Localizer.Format("#autoLOC_469975");
                        break;
                }
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateGraphics")]
        internal static bool UpdateGraphics(RDNode __instance)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled
                || !PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes
                || !PresetManager.Instance.ActivePreset.GeneralSettings.BuildTimes
                || SpaceCenterManagement.Instance == null)
                return true;

            if (__instance.tech == null)
            {
                UnityEngine.Object.Destroy(__instance.tooltip);
                return false;
            }

            if (__instance.IsResearched)
            {
                __instance.SetButtonState(RDNode.State.RESEARCHED);
            }
            else if (SpaceCenterManagement.Instance.TechListHas(__instance.tech.techID))
            {
                __instance.SetButtonState(RDNode.State.RESEARCHED);
                __instance.graphics.SetIconColor(XKCDColors.KSPNotSoGoodOrange); // replaces the white
            }
            else
            {
                CurrencyModifierQueryRP0 cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.RnDTechResearch, 0d, -__instance.tech.scienceCost, 0d);
                if (-cmq.GetTotal(CurrencyRP0.Science) < __instance.controller.ScienceCap)
                {
                    __instance.SetButtonState(RDNode.State.RESEARCHABLE);
                }
                else
                {
                    __instance.SetButtonState(RDNode.State.FADED);
                }
            }

            //bool allResearched = true;
            //bool anyResearched = false;
            bool allQueuedOrResearched = true;
            bool anyResearchedOrQueued = false;
            List<RDNode.Parent> listReasearchedOrQueued = new List<RDNode.Parent>();
            List<RDNode.Parent> listLocked = new List<RDNode.Parent>();
            for (int i = 0, iC = __instance.parents.Length; i < iC; i++)
            {
                RDNode.Parent parent = __instance.parents[i];
                if (parent.parent.node.IsResearched)
                {
                    listReasearchedOrQueued.Add(parent);
                    //anyResearched = true;
                    anyResearchedOrQueued = true;
                }
                else
                {
                    //allResearched = false;
                    if (SpaceCenterManagement.Instance.TechListHas(parent.parent.node.tech.techID))
                    {
                        listReasearchedOrQueued.Add(parent);
                        anyResearchedOrQueued = true;
                    }
                    else
                    {
                        allQueuedOrResearched = false;
                        listLocked.Add(parent);
                    }
                }
            }
            if (allQueuedOrResearched)
            {
                __instance.ShowArrows(show: true, __instance.controller.gridArea.LineMaterial, null);
                //if (allResearched)
                    __instance.graphics.SetAvailablePartsCircle(__instance.PartsNotUnlocked());
                //else
                    //__instance.graphics.HideAvailablePartsCircle();
            }
            else if (anyResearchedOrQueued && __instance.AnyParentToUnlock)
            {
                __instance.ShowArrows(show: true, __instance.controller.gridArea.LineMaterial, listReasearchedOrQueued);
                __instance.ShowArrows(show: true, __instance.controller.gridArea.LineMaterialGray, listLocked);
                //if (anyResearched)
                    __instance.graphics.SetAvailablePartsCircle(__instance.PartsNotUnlocked());
                //else
                    //__instance.graphics.HideAvailablePartsCircle();
            }
            else
            {
                __instance.ShowArrows(show: true, __instance.controller.gridArea.LineMaterialGray, null);
                __instance.SetButtonState(RDNode.State.FADED);
                __instance.graphics.HideAvailablePartsCircle();
            }

            if (RnDDebugUtil.showPartsInNodeTooltips)
            {
                if (__instance.tooltip != null)
                {
                    __instance.tooltip.titleString = __instance.name;
                    __instance.tooltip.textString = __instance.GetTooltipCaption() + "\n" + KSPUtil.PrintCollection(__instance.tech.partsAssigned, "\n", (AvailablePart a) => a.title);
                }
            }
            else if (__instance.tooltip != null)
            {
                __instance.tooltip.titleString = __instance.name;
                __instance.tooltip.textString = __instance.GetTooltipCaption();
            }
            return false;
        }
    }
}
