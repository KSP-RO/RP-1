using HarmonyLib;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace RP0.Harmony
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
    internal class PatchRnD
    {
        [HarmonyPrefix]
        [HarmonyPatch("AddScience")]
        internal static bool Prefix_AddScience(ResearchAndDevelopment __instance, float value, TransactionReasons reason, ref float ___science)
        {
            ___science += value;
            if (!HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().AllowNegativeCurrency && ___science < 0f)
                ___science = 0f;

            CurrencyModifierQueryRP0 data = new CurrencyModifierQueryRP0(reason, 0f, value, 0f);
            GameEvents.Modifiers.OnCurrencyModifierQuery.Fire(data);
            GameEvents.Modifiers.OnCurrencyModified.Fire(data);
            GameEvents.OnScienceChanged.Fire(___science, reason);

            return false;
        }

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
}
