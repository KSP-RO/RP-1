using HarmonyLib;
using UnityEngine;
using System;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(CurrencyModifierQuery))]
    internal class PatchCMQ
    {
        [HarmonyPrefix]
        [HarmonyPatch("RunQuery")]
        internal static bool Prefix_RunQuery(TransactionReasons reason, float f0, float s0, float r0, ref CurrencyModifierQuery __result)
        {
            __result = CurrencyModifierQueryRP0.RunQuery((TransactionReasonsRP0)reason, f0, s0, r0, 0d, 0d);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("AddDelta")]
        internal static bool Prefix_AddDelta(CurrencyModifierQuery __instance, Currency c, float val)
        {
            if (__instance is CurrencyModifierQueryRP0 cmq)
            {
                cmq.AddDelta((CurrencyRP0)c, val);
                return false;
            }
            return true;
        }
        
        [HarmonyPrefix]
        [HarmonyPatch("CanAfford")]
        [HarmonyPatch(new Type[] { typeof(Action<Currency>) })]
        internal static bool Prefix_CanAfford(CurrencyModifierQuery __instance, ref Action<Currency> onInsufficientCurrency, ref bool __result)
        {
            if (__instance is CurrencyModifierQueryRP0 cmq)
            {
                __result = cmq.CanAffordOverride(onInsufficientCurrency);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("CanAfford")]
        [HarmonyPatch(new Type[] { typeof(Currency) })]
        internal static bool Prefix_CanAfford2(CurrencyModifierQuery __instance, ref Currency c, ref bool __result)
        {
            if (__instance is CurrencyModifierQueryRP0 cmq)
            {
                __result = cmq.CanAfford((CurrencyRP0)c);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetCostLine")]
        internal static bool Prefix_GetCostLine(CurrencyModifierQuery __instance, ref string __result, bool displayInverted, bool useCurrencyColors, bool useInsufficientCurrencyColors, bool includePercentage, string seperator)
        {
            if (__instance is CurrencyModifierQueryRP0 cmq)
            {
                __result = cmq.GetCostLineOverride(displayInverted, useCurrencyColors, useInsufficientCurrencyColors, includePercentage, seperator);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetEffectDelta")]
        internal static bool Prefix_GetEffectDelta(CurrencyModifierQuery __instance, Currency c, ref float __result)
        {
            if (__instance is CurrencyModifierQueryRP0 cmq)
            {
                __result = (float)cmq.GetEffectDelta((CurrencyRP0)c);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetEffectDeltaText")]
        internal static bool Prefix_GetEffectDeltaText(CurrencyModifierQuery __instance, ref string __result, Currency c, string format, CurrencyModifierQuery.TextStyling textStyle)
        {
            if (__instance is CurrencyModifierQueryRP0 cmq)
            {
                __result = cmq.GetEffectDeltaText((CurrencyRP0)c, format, textStyle);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetEffectPercentageText")]
        internal static bool Prefix_GetEffectPercentageText(CurrencyModifierQuery __instance, ref string __result, Currency c, string format, CurrencyModifierQuery.TextStyling textStyle)
        {
            if (__instance is CurrencyModifierQueryRP0 cmq)
            {
                __result = cmq.GetEffectPercentageText((CurrencyRP0)c, format, textStyle);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetInput")]
        internal static bool Prefix_GetInput(CurrencyModifierQuery __instance, Currency c, ref float __result)
        {
            if (__instance is CurrencyModifierQueryRP0 cmq)
            {
                __result = (float)cmq.GetInput((CurrencyRP0)c);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetTotal")]
        internal static bool Prefix_GetTotal(CurrencyModifierQuery __instance, Currency c, ref float __result)
        {
            if (__instance is CurrencyModifierQueryRP0 cmq)
            {
                __result = (float)cmq.GetTotal((CurrencyRP0)c);
                return false;
            }
            return true;
        }
    }
}
