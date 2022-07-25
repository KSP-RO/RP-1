using HarmonyLib;
using KSP.UI.Screens;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(DifficultyOptionsMenu))]
    internal class PatchDifficultyOptionsMenu
    {
        [HarmonyTranspiler]
        [HarmonyPatch("CreateDifficultWindow")]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool skipNext = false;
            foreach (var instruction in instructions)
            {
                if (skipNext)
                {
                    skipNext = false;
                    continue;
                }

                if (instruction.LoadsConstant(-100f))
                {
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_R4, 0f);
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_R4, 10f);
                    skipNext = true;
                }
                else
                    yield return instruction;
            }
        }
    }
}
