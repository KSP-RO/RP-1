using HarmonyLib;
using UnityEngine;
using KSP.UI.Screens;
using Contracts;

namespace RP0.Harmony
{
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
}
