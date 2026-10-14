using RP0;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// ContractConfigurator sets Contract Limits based on the prestige level of the contract.
    /// RP-1 doesn't want that, so we just override all of the limits to be the normal max active contracts limit.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class ContractLimitOverrider : MonoBehaviour
    {
        internal void UpdateContractLimits()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                int maxActive = GameVariables.Instance.GetActiveContractsLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl));

                RP0Debug.Log($"Setting trivial, significant, and exceptional contract limits to {maxActive}.");

                HighLogic.CurrentGame.Parameters.CustomParams<ContractConfiguratorParameters>().trivialContractLimit = maxActive;
                HighLogic.CurrentGame.Parameters.CustomParams<ContractConfiguratorParameters>().significantContractLimit = maxActive;
                HighLogic.CurrentGame.Parameters.CustomParams<ContractConfiguratorParameters>().exceptionalContractLimit = maxActive;
            }
        }

        internal void OnKSCFacilityUpgraded(Upgradeables.UpgradeableFacility facility, int _)
        {
            if (facility.name == "MissionControl")
            {
                UpdateContractLimits();
            }
        }

        internal void Start()
        {
            UpdateContractLimits();

            GameEvents.OnKSCFacilityUpgraded.Add(OnKSCFacilityUpgraded);
        }

        internal void OnDestroy()
        {
            GameEvents.OnKSCFacilityUpgraded.Remove(OnKSCFacilityUpgraded);
        }
    }
}