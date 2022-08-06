using HarmonyLib;
using KSP.UI;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(MCSceneSpawner))]
    internal class PatchMCSceneSpawner
    {
        /// <summary>
        /// Removes autosaving when closing the Mission Control overlay.
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch("OnMCDespawn")]
        internal static bool Prefix_OnMCDespawn(MCSceneSpawner __instance)
        {
            MusicLogic.fetch.UnpauseWithCrossfade();
            UIMasterController.Instance.RemoveCanvas(__instance.missionControlPrefab);
            return false;
        }
    }
}
