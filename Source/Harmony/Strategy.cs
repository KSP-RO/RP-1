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
            strategy.SetupConfig(config);
            if (strategy.FactorSliderDefault != 0f)
            {
                if (strategy.Factor == 0f)
                {
                    strategy.factor = strategy.FactorSliderDefault;
                }
            }
            __result = strategy;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("CanBeActivated")]
        internal static void Prefix_CanBeActivated(Strategies.Strategy __instance, ref string reason, ref bool __result)
        {
            if (__instance is ProgramStrategy)
                return;

            KSP.UI.Screens.Administration.Instance.activeStrategyCount = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch("CanBeActivated")]
        internal static void Postfix_CanBeActivated(Strategies.Strategy __instance, ref string reason, ref bool __result)
        {
            KSP.UI.Screens.Administration.Instance.activeStrategyCount = ProgramHandler.Instance.ActivePrograms.Count;
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