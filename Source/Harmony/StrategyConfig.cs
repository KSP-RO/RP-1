using HarmonyLib;
using Strategies;
using System.Reflection.Emit;
using System.Collections;
using System.Collections.Generic;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(StrategyConfig))]
    internal class PatchStrategyConfig
    {
        [HarmonyTranspiler]
        [HarmonyPatch("Create")]
        internal static IEnumerable<CodeInstruction> Transpiler_Create(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            for (int i = 0; i < code.Count; ++i)
            {
                // Change what is constructed here
                if (code[i].opcode == OpCodes.Newobj)
                {
                    code[i].operand = AccessTools.Method(typeof(StrategyConfigRP0), "NewBaseConfig");
                    break;
                }
            }

            return code;
        }

        [HarmonyPostfix]
        [HarmonyPatch("Create")]
        internal static void Postfix_Create(ConfigNode node, ref StrategyConfig __result)
        {
            if (__result is StrategyConfigRP0 s)
                s.Load(node);
        }
    }
}