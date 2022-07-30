using HarmonyLib;
using UnityEngine;
using KSP.UI.Screens;
using Contracts;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ProgressTracking))]
    internal class PatchProgressTracking
    {
        [HarmonyPrefix]
        [HarmonyPatch("OnAwake")]
        internal static bool Prefix_OnAwake(ProgressTracking __instance)
        {
            __instance.achievementTree = new ProgressTree();
            typeof(ProgressTracking).GetProperty("Instance").SetValue(null, __instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnLoad")]
        internal static bool Prefix_OnLoad(ProgressTracking __instance)
        {
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnSave")]
        internal static bool Prefix_OnSave(ProgressTracking __instance)
        {
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Start")]
        internal static bool Prefix_Start(ProgressTracking __instance)
        {
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        internal static bool Prefix_Update(ProgressTracking __instance)
        {
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnDestroy")]
        internal static bool Prefix_OnDestroy(ProgressTracking __instance)
        {
            if(ProgressTracking.Instance == __instance)
                typeof(ProgressTracking).GetProperty("Instance").SetValue(null, null);

            return false;
        }
    }
}
