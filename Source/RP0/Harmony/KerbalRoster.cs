using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(KerbalRoster))]
    internal class PatchKerbalRoster
    {
        [HarmonyPrefix]
        [HarmonyPatch("GenerateInitialCrewRoster")]
        internal static bool Prefix_GenerateInitialCrewRoster(Game.Modes mode, ref KerbalRoster __result)
        {
            if (mode != Game.Modes.CAREER)
                return true;

            // Short-circuit: just create the roster, don't add any crew
            __result = new KerbalRoster(mode);
            return false;
        }
    }
}
