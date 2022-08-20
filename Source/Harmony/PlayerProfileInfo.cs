using HarmonyLib;
using KSP.UI.Screens;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(LoadGameDialog.PlayerProfileInfo))]
    internal class PatchPlayerProfileInfo
    {
        [HarmonyTranspiler]
        [HarmonyPatch("LoadDetailsFromGame")]
        internal static IEnumerable<CodeInstruction> Transpiler_LoadDetailsFromGame(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.LoadsConstant(10f))
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_R4, 1f);
                else
                    yield return instruction;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("LoadDetailsFromGame")]
        internal static void Postfix_LoadDetailsFromGame(LoadGameDialog.PlayerProfileInfo __instance, Game game)
        {
            if (game.Mode != Game.Modes.CAREER)
                return;

            for (int i = game.scenarios.Count; i-- > 0;)
            {
                ProtoScenarioModule proto = game.scenarios[i];
                if (proto.moduleName == "Confidence")
                {
                    proto.GetData().TryGetValue("confidence", ref __instance.missionCurrentScore);
                    return;
                }
            }
        }
    }
}
