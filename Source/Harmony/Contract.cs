﻿using HarmonyLib;
using UnityEngine;
using KSP.UI.Screens;
using Contracts;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Contract))]
    internal class PatchContractRewards
    {
        internal static ContractConfigurator.ConfiguredContract _contract;

        [HarmonyPrefix]
        [HarmonyPatch("MessageRewards")]
        internal static void Prefix_MessageRewards(Contract __instance)
        {
            if (__instance is ContractConfigurator.ConfiguredContract cc)
            {
                _contract = cc;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("MessageRewards")]
        internal static void Postfix_MessageRewards(Contract __instance)
        {
            _contract = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch("MissionControlTextRich")]
        internal static void Prefix_MissionControlTextRich(Contract __instance)
        {
            if (__instance is ContractConfigurator.ConfiguredContract cc)
            {
                _contract = cc;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("MissionControlTextRich")]
        internal static void Postfix_MissionControlTextRich(Contract __instance)
        {
            _contract = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RichTextUtil))]
        [HarmonyPatch("TextAward")]
        internal static bool Postfix_TextAward(ref string __result, ref string title, ref string value, ref int lines)
        {
            if (_contract == null)
                return true;

            if (Programs.ProgramHandler.Instance != null && Programs.ProgramHandler.Instance.IsContractOptional(_contract))
            {
                CurrencyModifierQuery cmq = CurrencyModifierQuery.RunQuery(TransactionReasons.ContractReward, 0, 0, _contract.ReputationCompletion);
                value += $"\n<color=#{RUIutils.ColorToHex(XKCDColors.KSPBadassGreen)}>{KSP.Localization.Localizer.Format("#rp0ConfidenceValue", (cmq.GetTotal(Currency.Reputation) * Programs.ProgramHandler.Settings.repToConfidence).ToString("N0"))}</color>";
            }
            if (KerbalConstructionTime.PresetManager.Instance != null)
            {
                int applicants = KerbalConstructionTime.PresetManager.Instance.ActivePreset.GeneralSettings.ContractApplicants.GetApplicantsFromContract(_contract.contractType.name);
                if (applicants > 0)
                    value += $"\n{KSP.Localization.Localizer.Format("#rp0GainApplicants", applicants)}";
            }

            __result = $"<b><color=#{RUIutils.ColorToHex(RichTextUtil.colorAwards)}>{title}:</color></b> {value}";
            if (lines > 0)
                __result += new string('\n', lines);
            return false;
        }
    }
}