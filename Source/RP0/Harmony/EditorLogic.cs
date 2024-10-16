using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(EditorLogic))]
    internal class PatchEditorLogic
    {
        [HarmonyPrefix]
        [HarmonyPatch("goForLaunch")]
        internal static void Prefix_goForLaunch()
        {
            if (PresetManager.Instance.ActivePreset?.GeneralSettings?.Enabled != true) return;

            SpaceCenterManagement.Instance.LaunchedVessel = new VesselProject(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, EditorLogic.FlagURL, false);
            SpaceCenterManagement.Instance.LaunchedVessel.LCID = SpaceCenterManagement.Instance.ActiveSC.ActiveLC.ID;
        }
    }
}
