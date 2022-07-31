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
    [HarmonyPatch(typeof(Strategy))]
    internal class PatchStrategy
    {
        [HarmonyPostfix]
        [HarmonyPatch("SetupConfig")]
        internal static void Postfix_SetupConfig(Strategy __instance)
        {
            MethodInfo OnSetupConfigMethod = __instance.GetType().GetMethod("OnSetupConfig");
            if (OnSetupConfigMethod != null)
                OnSetupConfigMethod.Invoke(__instance, new object[] { });
        }

        [HarmonyPostfix]
        [HarmonyPatch("CanBeDeactivated")]
        internal static void Postfix_CanBeDeactivated(Strategy __instance, ref string reason, ref bool __result)
        {
            if (__instance is ProgramStrategy ps && __result)
            {
                reason = KSP.Localization.Localizer.GetStringByTag("#rp0CanCompleteProgram");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("LongestDuration", MethodType.Getter)]
        internal static bool Prefix_LongestDuration(Strategy __instance, ref double __result)
        {
            __result = Mathfx.Lerp(__instance.MinLongestDuration, __instance.MaxLongestDuration, __instance.Factor);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("LeastDuration", MethodType.Getter)]
        internal static bool Prefix_LeastDuration(Strategy __instance, ref double __result)
        {
            __result = Mathfx.Lerp(__instance.MinLeastDuration, __instance.MaxLeastDuration, __instance.Factor);
            return false;
        }

        // These are needed because GetEffectText uses the wrong date printing method when a strategy is inactive.
        // We also need to clean up some other things.
        [HarmonyPrefix]
        [HarmonyPatch("GetEffectText")]
        internal static bool Prefix_GetEffectText(Strategy __instance, ref string __result)
        {
            string text = "";
            // We don't need to prepend Description.
            //text += RichTextUtil.Title(Localizer.GetStringByTag("#autoLOC_304558");

            // Ditto - this is handled as part of the Admin window's strategy window.
            //text += Localizer.Format("#autoLOC_304559", __instance.Description);
            // We don't want the description in the mini item for active strats because the effect text
            // itself will cover it.
            text += RichTextUtil.Title(Localizer.GetStringByTag("#autoLOC_304560"));

            foreach (StrategyEffect strategyEffect in __instance.Effects)
            {
                text += "<b><color=#" + RUIutils.ColorToHex(RichTextUtil.colorParams) + ">* " + strategyEffect.Description + "</color></b>\n";
            }

            text += "\n";
            if (__instance.IsActive)
            {
                if (__instance.LeastDuration > 0)
                {
                    if (__instance.DateActivated + __instance.LeastDuration <= KSPUtils.GetUT())
                    {
                        text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderCanRemove"), Localizer.GetStringByTag("#autoLOC_6002417"));
                    }
                    else
                    {
                        if (GameSettings.SHOW_DEADLINES_AS_DATES)
                            text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderCanRemoveOn"), KSPUtil.PrintDate(__instance.LeastDuration + KSPUtils.GetUT(), false, false));
                        else
                            text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderCanRemoveIn"), KSPUtil.PrintDateDeltaCompact(__instance.LeastDuration, false, false));
                    }
                }
                if (__instance.LongestDuration > 0)
                {
                    if (GameSettings.SHOW_DEADLINES_AS_DATES)
                        text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderRetiresOn"), KSPUtil.PrintDate(__instance.LongestDuration + KSPUtils.GetUT(), false, false));
                    else
                        text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderRetiresIn"), KSPUtil.PrintDateDeltaCompact(__instance.LongestDuration, false, false));
                }
            }
            else
            {
                if (__instance.LeastDuration > 0)
                {
                    text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderCanRemoveAfter"), KSPUtil.PrintDateDeltaCompact(__instance.LeastDuration, false, false));
                }
                if (__instance.LongestDuration > 0)
                {
                    text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderRetiresAfter"), KSPUtil.PrintDateDeltaCompact(__instance.LongestDuration, false, false));
                }
            }


            string text2 = string.Empty;
            if (__instance.InitialCostFunds != 0f)
            {
                text2 = text2 + "<color=#B4D455><sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>  " + __instance.InitialCostFunds.ToString("F0") + "   </color>";
            }
            if (__instance.InitialCostScience != 0f)
            {
                text2 = text2 + "<color=#6DCFF6><sprite=\"CurrencySpriteAsset\" name=\"Science\" tint=1> " + __instance.InitialCostScience.ToString("F0") + "   </color>";
            }
            if (__instance.InitialCostReputation != 0f)
            {
                text2 = text2 + "<color=#E0D503><sprite=\"CurrencySpriteAsset\" name=\"Reputation\" tint=1> " + __instance.InitialCostReputation.ToString("F0") + "   </color>";
            }
            if (text2 != string.Empty)
            {
                text += RichTextUtil.TextAdvance(Localizer.GetStringByTag("#autoLOC_304612"), text2);
            }

            __result = text;
            return false;
        }

        [HarmonyTranspiler]
        [HarmonyPatch("CanBeDeactivated")]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            for (int i = 9; i < code.Count; ++i)
            {
                // We need to fix the inequality check for if ( dateActivated + LeastDuration < Planetarium.fetch.time )
                // because that should be a > check.
                if (code[i].opcode == OpCodes.Bge_Un_S)
                {
                    code[i].opcode = OpCodes.Ble_Un_S;
                    break;
                }
            }

            return code;
        }

        internal static FieldInfo curStrats = typeof(KSP.UI.Screens.Administration).GetField("activeStrategyCount", AccessTools.all);

        [HarmonyPrefix]
        [HarmonyPatch("CanBeActivated")]
        internal static void Prefix_CanBeActivated(Strategy __instance, ref string reason, ref bool __result, ref string ___cacheAutoLOC_304827, ref string ___cacheAutoLOC_304841)
        {
            if (__instance is ProgramStrategy)
                return;

            curStrats.SetValue(KSP.UI.Screens.Administration.Instance, 0);
        }

        [HarmonyPostfix]
        [HarmonyPatch("CanBeActivated")]
        internal static void Postfix_CanBeActivated(Strategy __instance, ref string reason, ref bool __result, ref string ___cacheAutoLOC_304827, ref string ___cacheAutoLOC_304841)
        {
            curStrats.SetValue(KSP.UI.Screens.Administration.Instance, ProgramHandler.Instance.ActivePrograms.Count);
        }
    }
}