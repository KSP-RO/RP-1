using HarmonyLib;
using KSP.UI;
using KSP.UI.Screens;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(AdministrationSceneSpawner))]
    internal class PatchAdministrationSceneSpawner
    {
        /// <summary>
        /// Removes autosaving when closing the Admin building overlay.
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch("onAdminDespawn")]
        internal static bool Prefix_onAdminDespawn(AdministrationSceneSpawner __instance)
        {
            UIMasterController.Instance.RemoveCanvas(__instance.AdministrationScreenPrefab);
            MusicLogic.fetch.UnpauseWithCrossfade();
            return false;
        }
    }
}
