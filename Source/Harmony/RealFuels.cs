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
    [HarmonyPatch(typeof(RealFuels.ModuleEngineConfigsBase))]
    internal class RFMECBPatcher
    {
        [HarmonyPrefix]
        [HarmonyPatch("ResolveValidationError")]
        internal static void Prefix_ResolveValidationError(RealFuels.ModuleEngineConfigsBase __instance)
        {
            ConfigNode node = __instance.configs.FirstOrDefault(cn => cn.GetValue("name") == __instance.configuration);
            if (node != null)
            {
                string techID = node.GetValue("techRequired");
                if (techID != null)
                {
                    RFECMPatcher.techNode = techID;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("ResolveValidationError")]
        internal static void Postfix_ResolveValidationError(RealFuels.ModuleEngineConfigsBase __instance)
        {
            RFECMPatcher.techNode = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch("DrawSelectButton")]
        internal static void Prefix_DrawSelectButton(RealFuels.ModuleEngineConfigsBase __instance, ref ConfigNode node)
        {
            RFECMPatcher.techNode = node.GetValue("techRequired");
        }

        [HarmonyPostfix]
        [HarmonyPatch("DrawSelectButton")]
        internal static void Postfix_DrawSelectButton(RealFuels.ModuleEngineConfigsBase __instance, ref ConfigNode node)
        {
            RFECMPatcher.techNode = null;
        }
    }

    [HarmonyPatch(typeof(RealFuels.EntryCostManager))]
    internal class RFECMPatcher
    {
        internal static string techNode = null;
        internal static MethodInfo updateParts = typeof(RealFuels.EntryCostDatabase).GetMethod("UpdatePartEntryCosts", AccessTools.all);
        internal static MethodInfo updateUpgrades = typeof(RealFuels.EntryCostDatabase).GetMethod("UpdateUpgradeEntryCosts", AccessTools.all);
        internal static MethodInfo updateAll = typeof(RealFuels.EntryCostDatabase).GetMethod("UpdateEntryCosts", AccessTools.all);
        [HarmonyPrefix]
        [HarmonyPatch("PurchaseConfig")]
        internal static bool Prefix_PurchaseConfig(RealFuels.EntryCostManager __instance, ref string cfgName, ref bool __result)
        {
            if (techNode == null)
                return true;

            if (__instance.ConfigUnlocked(cfgName))
            {
                __result = false;
                return false;
            }

            double cfgCost = __instance.ConfigEntryCost(cfgName);

            if (!HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
            {
                if(CurrencyModifierQuery.RunQuery(TransactionReasons.RnDPartPurchase, (float)(UnlockSubsidyHandler.Instance.GetSubsidyAmount(techNode) - cfgCost), 0f, 0f).CanAfford())
                {
                    double excessCost = UnlockSubsidyHandler.Instance.SpendSubsidy(techNode, cfgCost);
                    if (excessCost > 0d)
                        Funding.Instance.AddFunds(-excessCost, TransactionReasons.RnDPartPurchase);
                }
                else
                {
                    __result = false;
                    return false;
                }
            }

            RealFuels.EntryCostDatabase.SetUnlocked(cfgName);

            if (updateAll == null)
            {
                updateParts.Invoke(null, new object[] { });
                updateUpgrades.Invoke(null, new object[] { });
            }
            else
            {
                updateAll.Invoke(null, new object[] { });
            }

            __result = true;
            return false;
        }
    }
}
