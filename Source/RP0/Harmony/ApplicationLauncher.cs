using HarmonyLib;
using KSP.UI.Screens;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ApplicationLauncher))]
    internal class PatchAppLauncher
    {
        [HarmonyPrefix]
        [HarmonyPatch("Show")]
        internal static bool Prefix_Show(ApplicationLauncher __instance)
        {
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