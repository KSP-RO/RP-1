using HarmonyLib;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace RP0.Harmony
{
   [HarmonyPatch(typeof(ResearchAndDevelopment))]
    internal class PatchRnD
    {
        [HarmonyPrefix]
        [HarmonyPatch("reverseEngineerPartsFrom")]
        internal static bool Prefix_reverseEngineerPartsFrom(ResearchAndDevelopment __instance, List<string> fromCBs, List<string> ignoreCBs, ref float subValue, string idVerb, string returnedFrom)
        {
            bool isHome = fromCBs.Count == 1 && fromCBs[0] == FlightGlobals.GetHomeBodyName();
            if (isHome)
            {
                switch (idVerb)
                {
                    case "Orbited":     subValue = 10f; break; // stock: 10f
                    case "SubOrbited":  subValue = 8f; break; // stock: 8f
                    case "Flew":
                        // TODO: Do something where we check if you went above the Karman line
                        // stock: 5f
                        return false;
                }
            }
            else
            {
                switch (idVerb)
                {
                    case "Surfaced":    subValue = 15f; break; // was 15f;
                    case "Flew":        subValue = 12f; break; // was 12f;
                    case "SubOrbited":  subValue = 10f; break; // was 10f;
                    case "Orbited":     subValue = 8f; break; // was 8f;
                    case "FlewBy":      subValue = 6f; break; // was 6f;
                }
            }
            return true;
        }

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
