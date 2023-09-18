using HarmonyLib;

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
                RP0Debug.LogError("LaunchSiteFacility tried to spawn VesselSpawnDialog! Aborting.");
                return false;
            }

            return true;
        }
    }
}
