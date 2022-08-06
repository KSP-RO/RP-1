using HarmonyLib;
using KSP.UI;
using KSP.UI.Screens;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ACSceneSpawner))]
    internal class PatchACSceneSpawner
    {
        /// <summary>
        /// Removes autosaving when closing the AC overlay.
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch("onACDespawn")]
        internal static bool Prefix_onACDespawn(ACSceneSpawner __instance)
        {
            UIMasterController.Instance.RemoveCanvas(__instance.ACScreenPrefab);
            MusicLogic.fetch.UnpauseWithCrossfade();
            return false;
        }
    }
}
