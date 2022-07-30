using HarmonyLib;
using System;
using KSP.UI.Screens;
using System.Collections.Generic;
using KSP.Localization;
using ContractConfigurator;
using Contracts;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(MissionControl))]
    internal class PatchMissionControl
    {
        internal static int ContractSortAvailable(Contract a, Contract b)
        {
            return RUIutils.SortAscDescPrimarySecondary(true, a.Prestige.CompareTo(b.Prestige), a.Title.CompareTo(b.Title));
        }

        internal static int ContractSortActive(Contract a, Contract b)
        {
            if (a is ConfiguredContract ccA && b is ConfiguredContract ccB)
            {
                bool aRecord = ContractUtils.ContractIsRecord(ccA);
                bool bRecord = ContractUtils.ContractIsRecord(ccB);
                if (aRecord != bRecord)
                {
                    return bRecord ? -1 : 1;
                }
            }

            return RUIutils.SortAscDescPrimarySecondary(true, b.Prestige.CompareTo(a.Prestige), a.Title.CompareTo(b.Title));
        }

        internal static int ContractSortArchive(Contract a, Contract b)
        {
            return RUIutils.SortAscDescPrimarySecondary(asc: true, a.DateFinished.CompareTo(b.DateFinished), a.Title.CompareTo(b.Title));
        }

        [HarmonyPrefix]
        [HarmonyPatch("RebuildContractList")]
        internal static bool Prefix_RebuildContractList(MissionControl __instance)
        {
            __instance.scrollListContracts.Clear(true);
            List<Contract> contracts = ContractSystem.Instance.Contracts;

            switch (__instance.displayMode)
            {
                case MissionControl.DisplayMode.Available:
                    contracts.Sort(ContractSortAvailable);
                    foreach (var contract in contracts)
                    {
                        if (contract.ContractState == Contract.State.Offered)
                        {
                            __instance.AddItem(contract, isAvailable: true);
                        }
                    }

                    break;

                case MissionControl.DisplayMode.Active:
                    contracts.Sort(ContractSortActive);
                    foreach (var contract in contracts)
                    {
                        if (contract.ContractState == Contract.State.Active)
                        {
                            __instance.AddItem(contract, isAvailable: false);
                        }
                    }
                    break;

                case MissionControl.DisplayMode.Archive:
                    contracts = ContractSystem.Instance.ContractsFinished;
                    contracts.Sort(ContractSortArchive);
                    foreach (var contract in contracts)
                    {
                        if (contract.ContractState == Contract.State.Completed && (__instance.archiveMode == MissionControl.ArchiveMode.All || __instance.archiveMode == MissionControl.ArchiveMode.Completed))
                        {

                            __instance.AddItem(contract, isAvailable: false, $"<color=#00ff00>{Localizer.Format("#autoLOC_468222")}</color> <color=#fefa87>{contract.Title}</color>");
                        }
                        else if ((contract.ContractState == Contract.State.Failed || contract.ContractState == Contract.State.DeadlineExpired) && (__instance.archiveMode == MissionControl.ArchiveMode.All || __instance.archiveMode == MissionControl.ArchiveMode.Failed))
                        {
                            if (contract.ContractState == Contract.State.Failed)
                                __instance.AddItem(contract, isAvailable: false, $"<color=#ff0000>{Localizer.Format("#autoLOC_468227")}</color> <color=#fefa87>{contract.Title}</color>");
                            else
                                __instance.AddItem(contract, isAvailable: false, $"<color=#ff0000>{Localizer.Format("#autoLOC_468229")}</color> <color=#fefa87>{contract.Title}</color>");
                        }
                        else if (contract.ContractState == Contract.State.Cancelled && (__instance.archiveMode == MissionControl.ArchiveMode.All || __instance.archiveMode == MissionControl.ArchiveMode.Cancelled))
                        {
                            __instance.AddItem(contract, isAvailable: false, $"<color=#aaaaaa>{Localizer.Format("#autoLOC_468233")}</color> <color=#fefa87>{contract.Title}</color>");
                        }
                    }
                    break;
            }
            return false;
        }
    }
}
