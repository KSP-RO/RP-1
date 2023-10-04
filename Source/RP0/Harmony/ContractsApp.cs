using HarmonyLib;
using KSP.UI.Screens;
using KSP.UI;
using UnityEngine;
using Contracts;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ContractsApp))]
    internal class PatchContractsApp
    {
        [HarmonyPrefix]
        [HarmonyPatch("CreateItem")]
        internal static bool Prefix_CreateItem(ContractsApp __instance, Contract contract, ref UICascadingList.CascadingListItem __result)
        {
            if (contract is ContractConfigurator.ConfiguredContract configuredContract)
            {
                // Records go at the end
                if (ContractUtils.ContractIsRecord(configuredContract))
                    return true;

                UnityEngine.UI.Button button;
                int index = -1;

                // Find the index of the first record.
                foreach (var kvp in __instance.contractList)
                {
                    if (ContractSystem.Instance.GetContractByGuid(kvp.Key) is ContractConfigurator.ConfiguredContract cc)
                    {
                        if (ContractUtils.ContractIsRecord(cc))
                        {
                            int idx = __instance.cascadingList.ruiList.cascadingList.GetIndex(kvp.Value.header);
                            if (idx < 0)
                            {
                                RP0Debug.LogError($"ContractsApp patcher: {cc.contractType.name} can't be found in UI list despite its guid existing in dictionary!");
                                continue;
                            }
                            if (index < 0 || idx < index)
                                index = idx;
                        }
                    }
                }

                UIListItem header = __instance.cascadingList.CreateHeader("<color=#e6752a>" + contract.Title + "</color>", out button, scaleBg: true);
                __result = __instance.cascadingList.ruiList.AddCascadingItem(header, __instance.cascadingList.CreateFooter(), __instance.CreateParameterList(contract), button, index);
                return false;
            }

            return true;
        }
    }
}