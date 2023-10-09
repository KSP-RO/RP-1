using HarmonyLib;

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

            if (SpaceCenterManagement.Instance.TechListHas(__instance.techID))
                return false;

            var tech = new ResearchProject(__instance);

            SpaceCenterManagement.Instance.TechList.Add(tech);
            tech.UpdateBuildRate(SpaceCenterManagement.Instance.TechList.Count - 1);

            SCMEvents.OnTechQueued.Fire(__instance);

            KCTUtilities.AddNodePartsToExperimental(__instance.techID);

            return false;
        }
    }
}
