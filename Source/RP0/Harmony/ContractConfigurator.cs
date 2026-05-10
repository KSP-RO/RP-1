using ContractConfigurator;
using Contracts;
using HarmonyLib;
using System;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ContractConfigurator.ContractConfigurator))]
    internal class PatchContractConfigurator
    {
        [HarmonyPrefix]
        [HarmonyPatch("ContractLimit")]
        internal static bool Prefix_ContractLimit(Contract.ContractPrestige prestige, ref int __result)
        {
            int level = (int)Math.Round(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) *
                ScenarioUpgradeableFacilities.GetFacilityLevelCount(SpaceCenterFacility.MissionControl));
            float rep = Reputation.Instance.reputation;
            float mult = HighLogic.CurrentGame.Parameters.CustomParams<ContractConfiguratorParameters>().ActiveContractMultiplier;
            __result = Math.Max(2, (int)Math.Round((rep + rep * level / 3) * mult / 200 + 6 + level)); // we only want the trivial count
            return false;
        }
    }
}
