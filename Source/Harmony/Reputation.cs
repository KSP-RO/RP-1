﻿using HarmonyLib;
using KSP.UI.Screens;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
        [HarmonyPatch(typeof(Reputation))]
        internal class PatchReputation
        {
            [HarmonyPrefix]
            [HarmonyPatch("addReputation_granular")]
            internal static bool Prefix_addReputation_granular(Reputation __instance, ref float value, ref float __result, ref float ___rep)
            {
                float oldRep = ___rep;
                ___rep = ___rep + value;
                __result = value;
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch("OnCrewKilled")]
            internal static bool Prefix_OnCrewKilled(Reputation __instance, ref EventReport evt)
            {
                if (evt.eventType == FlightEvents.CREW_KILLED)
                {
                    float repFixed = HighLogic.CurrentGame?.Parameters.CustomParams<RP0Settings>()?.RepLossKerbalDeathFixed ?? 0f;
                    float repPct = HighLogic.CurrentGame?.Parameters.CustomParams<RP0Settings>()?.RepLossKerbalDeathPercent ?? 0f;
                    __instance.AddReputation(-1f * (repFixed + repPct * __instance.reputation), TransactionReasons.VesselLoss);
                }
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch("onvesselRecoveryProcessing")]
            internal static bool Prefix_onvesselRecoveryProcessing(Reputation __instance, ref ProtoVessel pv, ref MissionRecoveryDialog mrDialog, ref float recoveryScore)
            {
                if (mrDialog != null)
                    mrDialog.reputationEarned = 0f;

                return false;
            }
        }

        [HarmonyPatch]
        internal class PatchPlayerProfileInfo_LoadDetailsFromGame
        {
            static MethodBase TargetMethod() => typeof(LoadGameDialog).GetNestedType("PlayerProfileInfo", AccessTools.all).GetMethod("LoadDetailsFromGame", AccessTools.all);

            [HarmonyTranspiler]
            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    if (instruction.LoadsConstant(10f))
                        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_R4, 1f);
                    else
                        yield return instruction;
                }
            }
        }
    }
}
