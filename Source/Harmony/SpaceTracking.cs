using HarmonyLib;
using KSP.UI.Screens;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(SpaceTracking))]
    internal class PatchSpaceTracking
    {
        [HarmonyTranspiler]
        [HarmonyPatch("OnVesselDeleteConfirm")]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int startIndex = -1;
            int endIndex = -1;

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldstr &&
                    codes[i].operand as string == "persistent")
                {
                    startIndex = i;

                    for (int j = startIndex; j < codes.Count; j++)
                    {
                        if (codes[j].opcode == OpCodes.Ldarg_0)
                        {
                            endIndex = j;
                            break;
                        }
                    }
                    break;
                }
            }

            if (startIndex > -1 && endIndex > -1)
            {
                // Cuts out the section about GamePersistence.SaveGame()
                codes.RemoveRange(startIndex, endIndex - startIndex);
            }

            return codes.AsEnumerable();
        }
    }
}
