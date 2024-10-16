using CommNet;
using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Vessel))]
    internal class PatchVessel
    {
        [HarmonyPatch("GetControlLevel")]
        internal static bool Prefix_GetControlLevel(Vessel __instance, ref Vessel.ControlLevel __result)
        {
            if (__instance.isEVA && __instance.crew.Count > 0)
            {
                if (!__instance.crew[0].inactive)
                {
                    __result = Vessel.ControlLevel.PARTIAL_MANNED;
                    return false;
                }
                __result = Vessel.ControlLevel.NONE;
                return false;
            }

            if (__instance.connection != null && CommNetScenario.Instance != null && CommNetScenario.CommNetEnabled)
            {
                __result = __instance.connection.GetControlLevel();
                if (__result == Vessel.ControlLevel.NONE) return false;
            }

            var controlLevel = Vessel.ControlLevel.NONE;
            int count = __instance.parts.Count;
            while (count-- > 0)
            {
                Part part = __instance.parts[count];
                if (part.isControlSource > controlLevel)
                {
                    controlLevel = part.isControlSource;
                }
            }

            if (controlLevel < __result) __result = controlLevel;

            return false;
        }
    }
}
