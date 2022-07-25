using HarmonyLib;
using UnityEngine;
using KSP.UI.Screens;
using Contracts;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ContractSystem))]
    internal class PatchContractSystem
    {
        [HarmonyPrefix]
        [HarmonyPatch("GetContractCounts")]
        internal static bool Prefix_GetContractCounts(ContractSystem __instance, ref float rep, ref int avgContracts, ref int tier1, ref int tier2, ref int tier3)
        {
            tier1 = tier2 = tier3 = int.MaxValue;
            return false;
        }
    }
}
