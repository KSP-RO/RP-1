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
            if (!(contract is ContractConfigurator.ConfiguredContract configuredContract))
            {
                return true;
            }

            // Skopos goes at the end
            if (ContractUtils.ContractIsSkoposMaintenance(configuredContract))
                return true;

            int index = -1;

            // search for the contract we need to go before:
            // if record, find Skopos
            // otherwise, find record
            bool isRecord = ContractUtils.ContractIsRecord(configuredContract);
            foreach (var kvp in __instance.contractList)
            {
                if (ContractSystem.Instance.GetContractByGuid(kvp.Key) is ContractConfigurator.ConfiguredContract cc &&
                    ((isRecord && ContractUtils.ContractIsSkoposMaintenance(cc)) || (!isRecord && ContractUtils.ContractIsRecord(cc))))
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
            UnityEngine.UI.Button button;

            UIListItem header = __instance.cascadingList.CreateHeader("<color=#e6752a>" + contract.Title + "</color>", out button, scaleBg: true);
            __result = __instance.cascadingList.ruiList.AddCascadingItem(header, __instance.cascadingList.CreateFooter(), __instance.CreateParameterList(contract), button, index);
            return false;
        }
    }
}