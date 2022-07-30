using HarmonyLib;
using KSP.UI.Screens;
using KSP.UI;
using UnityEngine;
using Contracts;
using System.Reflection;
using System.Collections.Generic;
using System;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ContractsApp))]
    internal class PatchContractsApp
    {
        internal static MethodInfo CreateParameterListMethod = typeof(ContractsApp).GetMethod("CreateParameterList", AccessTools.all);
        
        [HarmonyPrefix]
        [HarmonyPatch("CreateItem")]
        internal static bool Prefix_CreateItem(ContractsApp __instance, ref Contract contract, ref KSP.UI.UICascadingList.CascadingListItem __result, ref GenericCascadingList ___cascadingList, ref Dictionary<Guid, UICascadingList.CascadingListItem> ___contractList)
        {
            if (contract is ContractConfigurator.ConfiguredContract configuredContract)
            {
                // Records go at the end
                if (ContractUtils.ContractIsRecord(configuredContract))
                    return true;

                UnityEngine.UI.Button button;
                int index = -1;

                // Find the index of the first record.
                foreach (var kvp in ___contractList)
                {
                    if (ContractSystem.Instance.GetContractByGuid(kvp.Key) is ContractConfigurator.ConfiguredContract cc)
                    {
                        if (ContractUtils.ContractIsRecord(cc))
                        {
                            int idx = ___cascadingList.ruiList.cascadingList.GetIndex(kvp.Value.header);
                            if (idx < 0)
                            {
                                Debug.LogError($"[RP-0] ContractsApp patcher: {cc.contractType.name} can't be found in UI list despite its guid existing in dictionary!");
                                continue;
                            }
                            if (index < 0 || idx < index)
                                index = idx;
                        }
                    }
                }

                UIListItem header = ___cascadingList.CreateHeader("<color=#e6752a>" + contract.Title + "</color>", out button, scaleBg: true);
                __result = ___cascadingList.ruiList.AddCascadingItem(header, ___cascadingList.CreateFooter(), CreateParameterListMethod.Invoke(__instance, new object[] { contract }) as List<UIListItem>, button, index);
                return false;
            }

            return true;
        }
    }
}