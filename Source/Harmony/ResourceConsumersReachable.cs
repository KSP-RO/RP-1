using HarmonyLib;
using System;
using PreFlightTests;
using System.Collections.Generic;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ResourceConsumersReachable))]
    internal class PatchResourceConsumersReachable
    {
        [HarmonyPostfix]
        [HarmonyPatch("TestCondition")]
        internal static void Postfix_TestCondition(ResourceConsumersReachable __instance, ref PartResourceDefinition ___resourceDefinition, ref List<Part> ___failedParts, ref bool __result)
        {
            for (int i = ___failedParts.Count; i-- > 0;)
            {
                Part p = ___failedParts[i];
                // by definition this part is a consumer of this resource
                if (p.Resources.Contains(___resourceDefinition.id))
                    ___failedParts.RemoveAt(i);
            }

            __result = ___failedParts.Count == 0;
        }
    }
}
