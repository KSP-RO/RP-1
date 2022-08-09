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
                var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.RnDPartPurchase, -cfgCost, 0d, 0d);
                double postCMQcost = -cmq.GetTotal(CurrencyRP0.Funds);
                double invertCMQop = cfgCost / postCMQcost;
                double subsidy = UnlockSubsidyHandler.Instance.GetSubsidyAmount(techNode);
                cmq.AddDeltaAuthorized(CurrencyRP0.Funds, subsidy);
                if (!cmq.CanAfford())
                {
                    __result = false;
                    return false;
                }

                double excessCost = UnlockSubsidyHandler.Instance.SpendSubsidy(techNode, postCMQcost);
                if (excessCost > 0d)
                {
                    Funding.Instance.AddFunds(-excessCost * invertCMQop, TransactionReasons.RnDPartPurchase);
                }
            }

            RealFuels.EntryCostDatabase.SetUnlocked(cfgName);
            updateAll.Invoke(null, new object[] { });

            __result = true;
            return false;
        }
    }
}
