using HarmonyLib;
using Strategies;
using System.Reflection;
using RP0.Programs;
using UniLinq;
using System.Collections;
using System.Collections.Generic;
using KSP.Localization;
using System.Reflection.Emit;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Strategies.Strategy))]
    internal class PatchStrategy
    {
        [HarmonyPostfix]
        [HarmonyPatch("SetupConfig")]
        internal static void Postfix_SetupConfig(Strategies.Strategy __instance)
        {
            if (__instance is StrategyRP0 s)
                s.OnSetupConfig();
        }

        [HarmonyPostfix]
        [HarmonyPatch("CanBeDeactivated")]
        internal static void Postfix_CanBeDeactivated(Strategies.Strategy __instance, ref string reason, ref bool __result)
        {
            if (__instance is ProgramStrategy ps && __result)
            {
                reason = KSP.Localization.Localizer.GetStringByTag("#rp0CanCompleteProgram");
            }
        }

        internal static MethodInfo setupConfigMethod = typeof(Strategy).GetMethod("SetupConfig", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static FieldInfo factorField = typeof(Strategy).GetField("factor", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPrefix]
        [HarmonyPatch("Create")]
        internal static bool Prefix_Create(System.Type type, StrategyConfig config, ref Strategy __result)
        {
            if (config == null)
            {
                UnityEngine.Debug.LogError("Strategy: Config cannot be null");
                __result = null;
                return false;
            }
            if (type == null || type == typeof(Strategy))
            {
                type = typeof(StrategyRP0);
            }
            Strategy strategy = (Strategy)System.Activator.CreateInstance(type);
            setupConfigMethod.Invoke(strategy, new object[] { config });
            if (strategy.FactorSliderDefault != 0f)
            {
                if (strategy.Factor == 0f)
                {
                    factorField.SetValue(strategy, strategy.FactorSliderDefault);
                }
            }
            __result = strategy;
            return false;
        }

        internal static FieldInfo curStrats = typeof(KSP.UI.Screens.Administration).GetField("activeStrategyCount", AccessTools.all);

        [HarmonyPrefix]
        [HarmonyPatch("CanBeActivated")]
        internal static void Prefix_CanBeActivated(Strategies.Strategy __instance, ref string reason, ref bool __result, ref string ___cacheAutoLOC_304827, ref string ___cacheAutoLOC_304841)
        {
            if (__instance is ProgramStrategy)
                return;

            curStrats.SetValue(KSP.UI.Screens.Administration.Instance, 0);
        }

        [HarmonyPostfix]
        [HarmonyPatch("CanBeActivated")]
        internal static void Postfix_CanBeActivated(Strategies.Strategy __instance, ref string reason, ref bool __result, ref string ___cacheAutoLOC_304827, ref string ___cacheAutoLOC_304841)
        {
            curStrats.SetValue(KSP.UI.Screens.Administration.Instance, ProgramHandler.Instance.ActivePrograms.Count);
        }

        [HarmonyPrefix]
        [HarmonyPatch("Activate")]
        internal static bool Prefix_Activate(Strategy __instance, ref bool __result)
        {
            if (__instance is StrategyRP0 s)
            {
                __result = s.ActivateOverride();
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Deactivate")]
        internal static bool Prefix_Deactivate(Strategy __instance, ref bool __result)
        {
            if (__instance is StrategyRP0 s)
            {
                __result = s.DeactivateOverride();
                return false;
            }
            return true;
        }
    }
}