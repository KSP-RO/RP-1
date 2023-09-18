using HarmonyLib;
using System.Collections.Generic;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(LoadGameDialog.PlayerProfileInfo))]
    internal class PatchPlayerProfileInfo
    {
        [HarmonyTranspiler]
        [HarmonyPatch("LoadDetailsFromGame")]
        internal static IEnumerable<CodeInstruction> Transpiler_LoadDetailsFromGame(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            for (int i = 0; i < code.Count; ++i)
            {
                if (code[i].LoadsConstant(10f))
                {
                    code[i] = new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_R4, 1f);
                    break;
                }
            }

            return code;
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
