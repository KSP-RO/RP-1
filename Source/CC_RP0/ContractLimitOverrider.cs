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
            HighLogic.CurrentGame.Parameters.CustomParams<ContractConfiguratorParameters>().contractLimitPrestigeOverride = ContractConfiguratorParameters.ContractLimitPrestigeOverride.Trivial;
        }
    }
}
