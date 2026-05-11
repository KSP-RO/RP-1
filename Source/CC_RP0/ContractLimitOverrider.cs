using System;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// ContractConfigurator sets Contract Limits based on the prestige level of the contract.
    /// RP-1 doesn't want that, so we just override it to only use the limit that the trivial prestige level would have.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.AllGameScenes, true)]
    public class ContractLimitOverrider : MonoBehaviour
    {
        internal void Start()
        {
            int level = (int)Math.Round(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) *
                ScenarioUpgradeableFacilities.GetFacilityLevelCount(SpaceCenterFacility.MissionControl));
            float rep = Reputation.Instance.reputation;
            float mult = HighLogic.CurrentGame.Parameters.CustomParams<ContractConfiguratorParameters>().ActiveContractMultiplier;

            int contractLimit = Math.Max(2, (int)Math.Round((rep + rep * level / 3) * mult / 200 + 6 + level));

            HighLogic.CurrentGame.Parameters.CustomParams<ContractConfiguratorParameters>().trivialContractLimit = contractLimit;
            HighLogic.CurrentGame.Parameters.CustomParams<ContractConfiguratorParameters>().significantContractLimit = contractLimit;
            HighLogic.CurrentGame.Parameters.CustomParams<ContractConfiguratorParameters>().exceptionalContractLimit = contractLimit;
        }
    }
}
