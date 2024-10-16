using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class HarmonyPatcher : MonoBehaviour
    {
        private static bool _hasRun = false;
        // We have to run in Update rather than start, so that Kerbalism is loaded
        internal void Update()
        {
            if (_hasRun)
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "RP0HarmonyErr",
                $"<color=red>RP-1 Patching Error</color>",
                "RP-1 encountered an exception patching via Harmony. This usually means there's an install problem, please use the Install Guide to create a fresh RP-1 Express Install. If the error persists, create an issue on the RP-1 GitHub repository.", KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), true, HighLogic.UISkin,
                true, string.Empty);
            }

            _hasRun = true;
            var harmony = new HarmonyLib.Harmony("RP0.HarmonyPatcher");
            harmony.PatchAll();

            Harmony.PatchGameSettings.FixDV();
            Destroy(this);
        }
    }
}
