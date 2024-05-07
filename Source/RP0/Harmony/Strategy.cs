using HarmonyLib;
using Strategies;
using RP0.Programs;
using KSP.Localization;
using KSP.UI.Screens;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Strategy))]
    internal class PatchStrategy
    {
        // Fix SetupConfig to fire an OnSetupConfig after completion
        // if the StrategyConfig is a StrategyConfigRP0
        [HarmonyPostfix]
        [HarmonyPatch("SetupConfig")]
        internal static void Postfix_SetupConfig(Strategy __instance)
        {
            if (__instance is StrategyRP0 s)
                s.OnSetupConfig();
        }

        // Add one more test and reason string to CanBeDeactivated to support Programs
        [HarmonyPostfix]
        [HarmonyPatch("CanBeDeactivated")]
        internal static void Postfix_CanBeDeactivated(Strategy __instance, ref string reason, bool __result)
        {
            if (__instance is ProgramStrategy && __result)
            {
                reason = Localizer.GetStringByTag("#rp0_Admin_CanCompleteProgram");
            }
        }

        // Replace Create entirely so that the strategy created is a StrategyRP0 instead of stock Strategy
        // (in cases where a stock Strategy would have been created)
        [HarmonyPrefix]
        [HarmonyPatch("Create")]
        internal static bool Prefix_Create(System.Type type, StrategyConfig config, out Strategy __result)
        {
            if (config == null)
            {
                RP0Debug.LogError("Strategy: Config cannot be null");
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

        // Some hijinks here. Stock KSP has a strategy limit. Part of stock KSP's CanBeActivated checks
        // is a check that the current number of active strategies is < this limit. But we use
        // this limit to control programs only, not leaders. So we need to temporarily
        // set the number of active strategies to 0, and then reset it after CanBeActivated finishes.
        [HarmonyPrefix]
        [HarmonyPatch("CanBeActivated")]
        internal static void Prefix_CanBeActivated(Strategy __instance)
        {
            if (KSP.UI.Screens.Administration.Instance != null)
                KSP.UI.Screens.Administration.Instance.activeStrategyCount = 0;
        }

        // Now that we're done, reset the active strategy count.
        [HarmonyPostfix]
        [HarmonyPatch("CanBeActivated")]
        internal static void Postfix_CanBeActivated()
        {
            if (KSP.UI.Screens.Administration.Instance != null)
                KSP.UI.Screens.Administration.Instance.activeStrategyCount = ProgramHandler.Instance.ActiveProgramSlots;
        }

        // For our Strategy class, replace the basic Activate with a virtual one.
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

        // For our Strategy class, replace the basic Deactivate with a virtual one.
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

        // Fix strategies getting deactivated in Editor due to Planetarium time not getting updated on reverts
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        internal static bool Prefix_Update(Strategy __instance)
        {
            if (__instance.LongestDuration != 0d &&
                __instance.dateActivated + __instance.LongestDuration <= Planetarium.GetUniversalTime())
            {
                __instance.SendStateMessage(Strategy.cacheAutoLOC_304909, __instance.MessageDeactivatedMaxTime(),
                                            MessageSystemButton.MessageButtonColor.ORANGE, MessageSystemButton.ButtonIcons.ALERT);
                __instance.Deactivate();
                return false;
            }

            __instance.OnUpdate();

            int i = 0;
            for (int count = __instance.effects.Count; i < count; i++)
            {
                __instance.effects[i].Update();
            }

            return false;
        }
    }
}