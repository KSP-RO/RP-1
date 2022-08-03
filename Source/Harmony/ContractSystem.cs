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

        internal static bool _nextResetIsAfterLoad = false;
        [HarmonyPrefix]
        [HarmonyPatch("OnLoadRoutine")]
        internal static void Prefix_OnLoadRoutine()
        {
            _nextResetIsAfterLoad = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("ResetContracts")]
        internal static void Postfix_ResetContracts(ContractSystem __instance)
        {
            if (!_nextResetIsAfterLoad)
                return;

            _nextResetIsAfterLoad = false;

            foreach (var c in __instance.ContractsFinished)
            {
                if (c is ContractConfigurator.ConfiguredContract cc && c.ContractState == Contract.State.Completed && cc.contractType != null && !string.IsNullOrEmpty(cc.contractType.name))
                {
                    Programs.ProgramHandler.Instance.CompletedCCContracts.Add(cc.contractType.name);
                }
            }
        }
    }
}
