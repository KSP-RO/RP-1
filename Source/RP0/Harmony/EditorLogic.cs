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

            KerbalConstructionTimeData.Instance.LaunchedVessel = new VesselProject(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, EditorLogic.FlagURL, false);
            KerbalConstructionTimeData.Instance.LaunchedVessel.LCID = KerbalConstructionTimeData.Instance.ActiveSC.ActiveLC.ID;
        }
    }
}
