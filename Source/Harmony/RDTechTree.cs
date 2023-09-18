using HarmonyLib;
using KSP.UI.Screens;
using UnityEngine;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(RDTechTree))]
    internal class PatchRDTechTree
    {
        public static RDTechTree Instance { get; private set; }

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        internal static void Postfix_Awake(RDTechTree __instance)
        {
            Instance = __instance;
            // Annoyingly RDTechTree has no OnDestroy method, so we add a new component
            // to handle that.
            __instance.gameObject.AddComponent<RDTechTreeInstanceDestroyer>();
        }

        private class RDTechTreeInstanceDestroyer : MonoBehaviour
        {
            public void OnDestroy()
            {
                Instance = null;
            }
        }
    }
}
