using HarmonyLib;
using Strategies;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(StrategyConfig))]
    internal class PatchStrategyConfig
    {
        // Do a transpiler patch to make sure all StrategyConfigs created are instead StrategyConfigRP0.
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

        // Once Create finishes, call the StrategyConfigRP0's Load as well as (as stock's Create does) the base one.
        [HarmonyPostfix]
        [HarmonyPatch("Create")]
        internal static void Postfix_Create(ConfigNode node, StrategyConfig __result)
        {
            if (__result is StrategyConfigRP0 s)
                s.Load(node);
        }
    }
}