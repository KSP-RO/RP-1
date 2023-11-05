using HarmonyLib;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace RP0.Harmony
{
    [HarmonyPatch]
    internal class PatchKSPWheel_KSPWheelDamage_wearUpdateSimple
    {
        static bool Prepare()
        {
            bool foundKSPWheel = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name.Equals("KSPWheel", System.StringComparison.OrdinalIgnoreCase)) != null;
            RP0Debug.Log("Attempting to patch KSPWheel. Found? " + foundKSPWheel.ToString(), true);
            return foundKSPWheel;
        }

        static MethodBase TargetMethod() => AccessTools.TypeByName("KSPWheel.KSPWheelDamage")?.GetMethod("wearUpdateSimple", AccessTools.all);

        [HarmonyPrefix]
        internal static bool Prefix_wearUpdateSimple(PartModule __instance, ref float ___load, ref float ___loadStress, ref float ___speed, ref float ___stressTime)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return true;

            if (__instance.vessel == null)
                return true;

            // We clobber prelaunch just in case there's kraken twitching.
            // And we obviously clobber all the other situations
            // so we only bail when Landed.
            if (__instance.vessel.situation == Vessel.Situations.LANDED)
                return true;

            // Reset everything
            ___load = ___loadStress = ___speed = 0f;
            // the method does this if load and speed are under limit
            ___stressTime = Mathf.Max(0f, ___stressTime - Time.fixedDeltaTime);
            return false;
        }
    }
}