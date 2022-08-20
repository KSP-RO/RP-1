using HarmonyLib;
using KSP.UI.Screens;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RP0.Harmony
{
    // Patching this with a transpiler to show confidence is gonna be HARD.
    // Punting for now.

    //[HarmonyPatch(typeof(LoadGameDialog))]
    //internal class PatchLoadGameDialog
    //{
    //    [HarmonyTranspiler]
    //    [HarmonyPatch("CreateLoadList")]
    //    internal static IEnumerable<CodeInstruction> Transpiler_LoadDetailsFromGame(IEnumerable<CodeInstruction> instructions)
    //    {
    //        foreach (var instruction in instructions)
    //        {
    //            if (instruction.LoadsConstant(10f))
    //                yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_R4, 1f);
    //            else
    //                yield return instruction;
    //        }
    //    }
    //}
}
