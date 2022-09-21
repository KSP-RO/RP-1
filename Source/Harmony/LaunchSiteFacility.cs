using HarmonyLib;
using UnityEngine;
using KSP.UI.Screens;
using KerbalConstructionTime;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(LaunchSiteFacility))]
    internal class PatchLaunchSiteFacility
    {
        [HarmonyPrefix]
        [HarmonyPatch("showShipSelection")]
        internal static bool Prefix_showShipSelection()
        {
            if (!KCT_GUI.IsPrimarilyDisabled && HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                KCTDebug.LogError("LaunchSiteFacility tried to spawn VesselSpawnDialog! Aborting.");
                return false;
            }

            return true;
        }
    }
}
