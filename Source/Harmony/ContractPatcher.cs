using HarmonyLib;
using UnityEngine;

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
        [HarmonyPatch(typeof(Contracts.ContractSystem))]
        internal class PatchContractSystem
        {
            [HarmonyPatch("GetContractCounts")]
            internal static bool Prefix_GetContractCounts(Contracts.ContractSystem __instance, ref float rep, ref int avgContracts, ref int tier1, ref int tier2, ref int tier3)
            {
                tier1 = tier2 = tier3 = int.MaxValue;
                return false;
            }
        }
    }
}
