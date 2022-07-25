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
    }

    [HarmonyPatch(typeof(ContractParameter))]
    internal class PatchContractParameter
    {
        internal static float _storedRep = 0f;

        [HarmonyPrefix]
        [HarmonyPatch("SendStateMessage")]
        internal static void Prefix_SendStateMessage(ContractParameter __instance, ref string title, ref string message, ref MessageSystemButton.MessageButtonColor color, ref MessageSystemButton.ButtonIcons icon)
        {
            if (icon != MessageSystemButton.ButtonIcons.COMPLETE || !__instance.Optional)
                return;

            if (__instance.Root is ContractConfigurator.ConfiguredContract cc && Programs.ProgramHandler.Instance != null && Programs.ProgramHandler.Instance.IsContractOptional(cc))
            {
                message += $"\n<color=#{RUIutils.ColorToHex(XKCDColors.KSPBadassGreen)}>{KSP.Localization.Localizer.Format("#rp0ConfidenceValue", (_storedRep * Programs.ProgramHandler.Settings.repToConfidence).ToString("N0"))}</color>";
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("AwardCompletion")]
        internal static void Prefix_AwardCompletion(ContractParameter __instance)
        {
            //KSP sets the rewards to 0 as part of this!
            //So we have to intercept and store the value.
            if (__instance.Root is ContractConfigurator.ConfiguredContract cc && Programs.ProgramHandler.Instance != null && Programs.ProgramHandler.Instance.IsContractOptional(cc))
            {
                CurrencyModifierQuery cmq = CurrencyModifierQuery.RunQuery(TransactionReasons.ContractReward, 0, 0, __instance.ReputationCompletion);
                _storedRep = cmq.GetTotal(Currency.Reputation);
            }
        }
    }

    [HarmonyPatch]
    internal class PatchContractRewards
    {
        internal static ContractConfigurator.ConfiguredContract _contract;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Contract))]
        [HarmonyPatch("MessageRewards")]
        internal static void Prefix_MessageRewards(Contract __instance)
        {
            if (__instance is ContractConfigurator.ConfiguredContract cc)
            {
                _contract = cc;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Contract))]
        [HarmonyPatch("MessageRewards")]
        internal static void Postfix_MessageRewards(Contract __instance)
        {
            _contract = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Contract))]
        [HarmonyPatch("MissionControlTextRich")]
        internal static void Prefix_MissionControlTextRich(Contract __instance)
        {
            if (__instance is ContractConfigurator.ConfiguredContract cc)
            {
                _contract = cc;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Contract))]
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
