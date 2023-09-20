using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class HarmonyPatcher : MonoBehaviour
    {
        // We have to run in Update rather than start, so that Kerbalism is loaded
        internal void Update()
        {
            var harmony = new HarmonyLib.Harmony("RP0.HarmonyPatcher");
            harmony.PatchAll();

            Harmony.PatchGameSettings.FixDV();
            Destroy(this);
        }
    }
}
