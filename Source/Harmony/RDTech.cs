using HarmonyLib;
using KerbalConstructionTime;
using UniLinq;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(RDTech))]
    internal class PatchRDTech
    {
        [HarmonyPrefix]
        [HarmonyPatch("UnlockTech")]
        internal static bool Prefix_UnlockTech(RDTech __instance, bool updateGameState)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled 
                || !PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes 
                || !PresetManager.Instance.ActivePreset.GeneralSettings.BuildTimes)
                return true;

            if (KerbalConstructionTimeData.Instance.TechListHas(__instance.techID))
                return false;

            var tech = new TechItem(__instance);

            KerbalConstructionTimeData.Instance.TechList.Add(tech);
            tech.UpdateBuildRate(KerbalConstructionTimeData.Instance.TechList.Count - 1);

            KCTEvents.OnTechQueued.Fire(__instance);

            KerbalConstructionTime.Utilities.AddNodePartsToExperimental(__instance.techID);

            return false;
        }
    }
}
