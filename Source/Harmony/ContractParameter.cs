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
                var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ContractReward, 0d, 0d, 0d, _storedRep * Programs.ProgramHandler.Settings.repToConfidence, 0d);
                message += $"<color={CurrencyModifierQueryRP0.CurrencyColor(CurrencyRP0.Confidence)}>{CurrencyModifierQueryRP0.SpriteString(CurrencyRP0.Confidence)} {cmq.GetTotal(CurrencyRP0.Confidence):N0} {cmq.GetEffectDeltaText(CurrencyRP0.Confidence, "N0", CurrencyModifierQuery.TextStyling.OnGUI)}  </color>";
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
                _storedRep = __instance.ReputationCompletion;
            }
        }
    }
}
