using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(DoubleCurve))]
    internal class PatchDoubleCurve
    {
        [HarmonyPrefix]
        [HarmonyPatch("RecomputeTangents")]
        internal static bool Prefix_RecomputeTangents(DoubleCurve __instance)
        {
            int count = __instance.keys.Count;
            DoubleKeyframe value;
            if (count == 1)
                return false;

            value = __instance.keys[0];
            if (value.autoTangent)
            {
                value.inTangent = 0.0;
                value.outTangent = (__instance.keys[1].value - value.value) / (__instance.keys[1].time - value.time);
                __instance.keys[0] = value;
            }
            int num = count - 1;
            value = __instance.keys[num];
            if (value.autoTangent)
            {
                value.inTangent = (value.value - __instance.keys[num - 1].value) / (value.time - __instance.keys[num - 1].value);
                value.outTangent = 0.0;
                __instance.keys[num] = value;
            }
            if (count <= 2)
            {
                return false;
            }
            for (int i = 1; i < num; i++)
            {
                value = __instance.keys[i];
                if (!value.autoTangent)
                {
                    continue;
                }
                double num2 = (value.value - __instance.keys[i - 1].value) / (value.time - __instance.keys[i - 1].value);
                double num3 = (__instance.keys[i + 1].value - value.value) / (__instance.keys[i + 1].time - value.time);
                value.inTangent = (value.outTangent = (num2 + num3) * 0.5);
            }

            return false;
        }
    }
}
