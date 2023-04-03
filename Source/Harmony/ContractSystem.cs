using Contracts;
using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ContractSystem))]
    internal class PatchContractSystem
    {
        [HarmonyPrefix]
        [HarmonyPatch("GetContractCounts")]
        internal static bool Prefix_GetContractCounts(ContractSystem __instance, float rep, int avgContracts, ref int tier1, ref int tier2, ref int tier3)
        {
            tier1 = tier2 = tier3 = int.MaxValue;
            return false;
        }
    }
}
