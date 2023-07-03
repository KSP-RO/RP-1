using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Planetarium))]
    internal class PatchPlanetarium
    {
        [HarmonyPrefix]
        [HarmonyPatch("GetUniversalTime")]
        internal static bool Prefix_GetUniversalTime(ref double __result)
        {
            if (HighLogic.LoadedSceneIsEditor || Planetarium.fetch == null)
                __result = HighLogic.CurrentGame.UniversalTime;
            else
                __result = Planetarium.fetch.time;

            return false;
        }
    }
}
