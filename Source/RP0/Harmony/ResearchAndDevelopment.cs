using HarmonyLib;
using KSP.UI.Screens;
using System.Collections.Generic;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ResearchAndDevelopment))]
    internal class PatchRnD
    {
        internal static ProtoVessel recoverVessel;

        [HarmonyPrefix]
        [HarmonyPatch("reverseEngineerRecoveredVessel")]
        internal static void Prefix_reverseEngineerRecoveredVessel(ResearchAndDevelopment __instance, ProtoVessel pv, MissionRecoveryDialog mrDialog)
        {
            recoverVessel = pv;
        }

        [HarmonyPostfix]
        [HarmonyPatch("reverseEngineerRecoveredVessel")]
        internal static void Postfix_reverseEngineerRecoveredVessel(ResearchAndDevelopment __instance, ProtoVessel pv, MissionRecoveryDialog mrDialog)
        {
            pv = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch("reverseEngineerPartsFrom")]
        internal static bool Prefix_reverseEngineerPartsFrom(ResearchAndDevelopment __instance, List<string> fromCBs, List<string> ignoreCBs, ref float subValue, string idVerb, string returnedFrom, ref List<ScienceSubject> __result)
        {
            bool isHome = fromCBs.Count == 1 && fromCBs[0] == FlightGlobals.GetHomeBodyName();
            if (isHome)
            {
                switch (idVerb)
                {
                    case "Orbited": subValue = 8f; break; // stock: 10f
                    case "SubOrbited": subValue = 5f; break; // stock: 8f
                    case "Flew":
                        VesselTripLog tripLog = VesselTripLog.FromProtoVessel(recoverVessel);
                        if (tripLog.Log.HasEntry(Crew.CrewHandler.Situation_FlightHigh, FlightGlobals.GetHomeBodyName()))
                        {
                            subValue = 2.5f;
                        }
                        else
                        {
                            __result = new List<ScienceSubject>();
                            return false;
                        }
                        break;
                }
            }
            else
            {
                switch (idVerb)
                {
                    case "Surfaced": subValue = 12f; break; // was 15f;
                    case "Flew": subValue = 10f; break; // was 12f;
                    case "SubOrbited": //subValue = 10f; break; // was 10f;
                        __result = new List<ScienceSubject>();
                        return false;
                    case "Orbited": subValue = 8f; break; // was 8f;
                    case "FlewBy": subValue = 6f; break; // was 6f;
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("AddScience")]
        internal static bool Prefix_AddScience(ResearchAndDevelopment __instance, float value, TransactionReasons reason)
        {
            __instance.science += value;
            if (!HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().AllowNegativeCurrency && __instance.science < 0f)
                __instance.science = 0f;

            CurrencyModifierQueryRP0 data = new CurrencyModifierQueryRP0(reason, 0f, value, 0f);
            GameEvents.Modifiers.OnCurrencyModifierQuery.Fire(data);
            GameEvents.Modifiers.OnCurrencyModified.Fire(data);
            GameEvents.OnScienceChanged.Fire(__instance.science, reason);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("PartTechAvailable")]
        internal static bool Prefix_PartTechAvailable(AvailablePart ap, out bool __result)
        {
            if (ResearchAndDevelopment.Instance == null)
            {
                __result = true;
                return false;
            }

            __result = PartTechAvailable(ap);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("PartModelPurchased")]
        internal static bool Prefix_PartModelPurchased(AvailablePart ap, out bool __result)
        {
            if (ResearchAndDevelopment.Instance == null)
            {
                __result = true;
                return false;
            }

            if (PartTechAvailable(ap))
            {
                if (ResearchAndDevelopment.Instance.protoTechNodes.TryGetValue(ap.TechRequired, out ProtoTechNode ptn) &&
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

        private static bool PartTechAvailable(AvailablePart ap)
        {
            if (string.IsNullOrEmpty(ap.TechRequired))
            {
                return false;
            }

            if (ResearchAndDevelopment.Instance.protoTechNodes.TryGetValue(ap.TechRequired, out ProtoTechNode ptn))
            {
                return ptn.state == RDTech.State.Available;
            }

            return false;
        }
    }
}
