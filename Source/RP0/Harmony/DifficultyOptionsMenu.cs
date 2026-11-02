using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(DifficultyOptionsMenu))]
    internal class PatchDifficultyOptionsMenu
    {
        // Stock sliders for the career multipliers step by 10%.
        // Our presets use 75% and 125% for rep loss, so those sliders need 5% steps.
        internal const float StockSliderScale = 10f;
        internal const float MultiplierSliderScale = 20f;

        private static bool IsCareerMultiplierField(CodeInstruction ci)
        {
            return (ci.opcode == OpCodes.Ldfld || ci.opcode == OpCodes.Stfld)
                && ci.operand is FieldInfo fi
                && fi.DeclaringType == typeof(GameParameters.CareerParams)
                && fi.Name.EndsWith("Multiplier");
        }

        // The getter and setter lambdas of the career multiplier sliders.
        internal static bool IsMultiplierSliderLambda(MethodBase method)
        {
            if (method == null || method.IsAbstract || !method.Name.Contains("CreateDifficultWindow"))
                return false;

            bool usesField = false, usesScale = false;
            foreach (CodeInstruction ci in PatchProcessor.GetOriginalInstructions(method))
            {
                usesField |= IsCareerMultiplierField(ci);
                usesScale |= ci.LoadsConstant(StockSliderScale);
            }
            return usesField && usesScale;
        }

        [HarmonyTranspiler]
        [HarmonyPatch("CreateDifficultWindow")]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            const float mult = MultiplierSliderScale / StockSliderScale;
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            for (int i = 0; i < code.Count; ++i)
            {
                if (code[i].LoadsConstant(-100f))
                {
                    code[i++] = new CodeInstruction(OpCodes.Ldc_R4, 0f);
                    code[i] = new CodeInstruction(OpCodes.Ldc_R4, 10f);
                    continue;
                }

                // Rescale the min and max of each career multiplier slider, which follow its getter.
                if (code[i].opcode == OpCodes.Ldftn
                    && code[i].operand is MethodInfo mi
                    && mi.ReturnType == typeof(float)
                    && IsMultiplierSliderLambda(mi)
                    && i + 3 < code.Count
                    && code[i + 2].LoadsConstant(1f)
                    && code[i + 3].LoadsConstant(100f))
                {
                    code[i + 2] = new CodeInstruction(OpCodes.Ldc_R4, 1f * mult);
                    code[i + 3] = new CodeInstruction(OpCodes.Ldc_R4, 100f * mult);
                    i += 3;
                }
            }
            return code;
        }
    }

    [HarmonyPatch]
    internal class PatchDifficultyOptionsMenu_MultiplierSliders
    {
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (System.Type t in typeof(DifficultyOptionsMenu).GetNestedTypes(AccessTools.all))
            {
                foreach (MethodInfo mi in AccessTools.GetDeclaredMethods(t))
                {
                    if (PatchDifficultyOptionsMenu.IsMultiplierSliderLambda(mi))
                        yield return mi;
                }
            }
        }

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction ci in instructions)
            {
                if (ci.LoadsConstant(PatchDifficultyOptionsMenu.StockSliderScale))
                    ci.operand = PatchDifficultyOptionsMenu.MultiplierSliderScale;
                yield return ci;
            }
        }
    }
}
