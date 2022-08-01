using HarmonyLib;
using System;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(EnumExtensions))]
    internal class PatchEnumExtensions
    {
        [HarmonyPrefix]
        [HarmonyPatch("Description")]
        internal static void Prefix_Description(ref Enum e)
        {
            if (e is Currency)
            {
                CurrencyRP0 newValue = (CurrencyRP0)e;
                e = newValue;
            }
        }
    }
}
