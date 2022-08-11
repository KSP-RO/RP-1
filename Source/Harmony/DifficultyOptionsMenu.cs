using HarmonyLib;
using KSP.UI.Screens;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(DifficultyOptionsMenu))]
    internal class PatchDifficultyOptionsMenu
    {
        [HarmonyTranspiler]
        [HarmonyPatch("CreateDifficultWindow")]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            for (int i = 0; i < code.Count; ++i)
            {
                if (code[i].LoadsConstant(-100f))
                {
                    code[i++] = new CodeInstruction(OpCodes.Ldc_R4, 0f);
                    code[i] = new CodeInstruction(OpCodes.Ldc_R4, 10f);
                    continue;
                }
            }
            return code;
        }
    }
}
