using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(CrewTransfer))]
    internal class PatchCrewTransfer
    {
        [HarmonyPrefix]
        [HarmonyPatch("IsValidPart")]
        internal static bool Prefix_IsValidPart(CrewTransfer __instance, Part p, ref bool __result)
        {
            __result = p.CrewCapacity > 0 && p.protoModuleCrew.Count < p.CrewCapacity && p.crewTransferAvailable && Crew.CrewHandler.CheckCrewForPart(__instance.crew, p.partInfo.name, true, false);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("IsSemiValidPart")]
        internal static bool Prefix_IsSemiValidPart(CrewTransfer __instance, Part p, ref bool __result)
        {
            __result = p.CrewCapacity > 0 && p.crewTransferAvailable && (p.protoModuleCrew.Count >= p.CrewCapacity || !Crew.CrewHandler.CheckCrewForPart(__instance.crew, p.partInfo.name, true, false));
            return false;
        }
    }
}
