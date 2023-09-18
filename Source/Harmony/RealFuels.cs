using HarmonyLib;
using KerbalConstructionTime;
using KSP.UI.Screens;
using KSP.UI.Screens.Editor;
using System;
using System.Collections.Generic;
using UniLinq;
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
        internal static void Prefix_DrawSelectButton(RealFuels.ModuleEngineConfigsBase __instance, ConfigNode node)
        {
            RFECMPatcher.techNode = node.GetValue("techRequired");
        }

        [HarmonyPostfix]
        [HarmonyPatch("DrawSelectButton")]
        internal static void Postfix_DrawSelectButton(RealFuels.ModuleEngineConfigsBase __instance, ConfigNode node)
        {
            RFECMPatcher.techNode = null;
        }

        [HarmonyPostfix]
        [HarmonyPatch("SetConfiguration", new Type[] { typeof(ConfigNode), typeof(bool) })]
        internal static void Postfix_SetConfiguration()
        {
            if (HighLogic.LoadedSceneIsEditor && KerbalConstructionTime.KerbalConstructionTime.Instance != null)
            {
                KerbalConstructionTime.KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
            }
        }
    }

    [HarmonyPatch(typeof(RealFuels.Tanks.ModuleFuelTanks))]
    internal class RFMFTPatcher
    {
        [HarmonyPostfix]
        [HarmonyPatch("RaiseTankDefinitionChanged")]
        internal static void Postfix_RaiseTankDefinitionChanged()
        {
            if (HighLogic.LoadedSceneIsEditor && KerbalConstructionTime.KerbalConstructionTime.Instance != null)
            {
                KerbalConstructionTime.KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
            }
        }
    }

    [HarmonyPatch(typeof(RealFuels.EntryCostManager))]
    internal class RFECMPatcher
    {
        internal static string techNode = null;
        internal static MethodInfo updateAll = typeof(RealFuels.EntryCostDatabase).GetMethod("UpdateEntryCosts", AccessTools.all);

        internal static bool PatchedPurchaseConfig(RealFuels.EntryCostManager __instance, string cfgName)
        {
            if (__instance.ConfigUnlocked(cfgName))
            {
                return false;
            }

            double cfgCost = __instance.ConfigEntryCost(cfgName);

            if (!HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
            {
                var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.PartOrUpgradeUnlock, -cfgCost, 0d, 0d);
                double postCMQcost = -cmq.GetTotal(CurrencyRP0.Funds, false);
                double invertCMQop = cfgCost / postCMQcost;
                double credit = UnlockCreditHandler.Instance.GetCreditAmount(techNode);
                // we don't bother with Min() because we're never touching this cmq again.
                cmq.AddPostDelta(CurrencyRP0.Funds, credit, true);
                if (!cmq.CanAfford())
                {
                    return false;
                }

                double excessCost = UnlockCreditHandler.Instance.SpendCredit(techNode, postCMQcost);
                if (excessCost > 0d)
                {
                    Funding.Instance.AddFunds(-excessCost * invertCMQop, TransactionReasonsRP0.PartOrUpgradeUnlock.Stock());
                }
            }

            RealFuels.EntryCostDatabase.SetUnlocked(cfgName);
            updateAll.Invoke(null, new object[] { });
            techNode = null;

            if (HighLogic.LoadedSceneIsEditor)
                KerbalConstructionTime.KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("PurchaseConfig")]
        [HarmonyPatch(new Type[] { typeof(string), typeof(string) })]
        internal static bool Prefix_PurchaseConfigNew(RealFuels.EntryCostManager __instance, string cfgName, ref bool __result)
        {
            if (techNode == null)
                return true;

            __result = PatchedPurchaseConfig(__instance, cfgName);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("PurchaseConfig")]
        [HarmonyPatch(new Type[] { typeof(string), })]
        internal static bool Prefix_PurchaseConfigOld(RealFuels.EntryCostManager __instance, string cfgName, ref bool __result)
        {
            if (techNode == null)
                return true;

            __result = PatchedPurchaseConfig(__instance, cfgName);
            return false;
        }
    }
}
