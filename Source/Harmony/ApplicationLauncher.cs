using HarmonyLib;
using KSP.UI.Screens;
using KSP.UI;
using UnityEngine;
using Contracts;
using System.Reflection;
using System.Collections.Generic;
using System;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ApplicationLauncher))]
    internal class PatchAppLauncher
    {
        [HarmonyPrefix]
        [HarmonyPatch("Show")]
        internal static bool Prefix_Show(ApplicationLauncher __instance)
        {
            //if (KerbalConstructionTime.KCT_GUI.InSCSubscene)
            //{
            //    Transform trf = __instance.transform;
            //    while (trf.parent != null)
            //        trf = trf.parent;
            //    Debug.Log("$$$$$$$$$$$$");
            //    trf.gameObject.Dump();
            //}
            if (ApplicationLauncher.Ready && __instance.launcherSpace.gameObject.activeSelf)
                return false;

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Hide")]
        internal static bool Prefix_Hide(ApplicationLauncher __instance)
        {
            if (!ApplicationLauncher.Ready && !__instance.launcherSpace.gameObject.activeSelf)
                return false;

            return true;
        }
    }
}