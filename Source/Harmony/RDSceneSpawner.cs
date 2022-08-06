using HarmonyLib;
using KSP.UI;
using KSP.UI.Screens;
using UnityEngine;
using UnityEngine.Rendering;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(RDSceneSpawner))]
    internal class PatchRDSceneSpawner
    {
        /// <summary>
        /// Removes autosaving when closing the RnD overlay.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___oldReflectionMode"></param>
        /// <param name="___oldReflection"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch("onRDDespawn")]
        internal static bool Prefix_onRDDespawn(RDSceneSpawner __instance, DefaultReflectionMode ___oldReflectionMode, Cubemap ___oldReflection)
        {
            UIMasterController.Instance.RemoveCanvas(__instance.RDScreenPrefab);
            RenderSettings.defaultReflectionMode = ___oldReflectionMode;
            RenderSettings.customReflection = ___oldReflection;
            MusicLogic.fetch.UnpauseWithCrossfade();
            return false;
        }
    }
}
