using HarmonyLib;
using UnityEngine;
using KSP.UI.Screens;
using Contracts;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Contract))]
    internal class PatchContractRewards
    {
        internal static ContractConfigurator.ConfiguredContract _contract;
        internal static bool _isReward = false;

        [HarmonyPrefix]
        [HarmonyPatch("MessageRewards")]
        internal static void Prefix_MessageRewards(Contract __instance)
        {
            if (__instance is ContractConfigurator.ConfiguredContract cc)
            {
                _contract = cc;
                _isReward = true;
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
                _isReward = false;
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
                var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ContractReward, 0d, 0d, 0d, _contract.ReputationCompletion * Programs.ProgramHandler.Settings.repToConfidence, 0d);
                value += $"<color={CurrencyModifierQueryRP0.CurrencyColor(CurrencyRP0.Confidence)}>{CurrencyModifierQueryRP0.SpriteString(CurrencyRP0.Confidence)} {cmq.GetTotal(CurrencyRP0.Confidence):N0} {cmq.GetEffectDeltaText(CurrencyRP0.Confidence, "N0", CurrencyModifierQuery.TextStyling.OnGUI)}  </color>";
            }
            if (KerbalConstructionTime.PresetManager.Instance != null)
            {
                int applicants = KerbalConstructionTime.PresetManager.Instance.ActivePreset.GeneralSettings.ContractApplicants.GetApplicantsFromContract(_contract.contractType.name);
                if (applicants > 0)
                    value += $"\n{KSP.Localization.Localizer.Format("#rp0GainApplicants", applicants)}";
            }

            string leaderString = string.Empty;
            foreach (var s in Strategies.StrategySystem.Instance.SystemConfig.Strategies)
            {
                if (s is StrategyConfigRP0 cfg && s.DepartmentName != "Programs")
                {
                    if ((!_isReward || !cfg.IsUnlocked()) && cfg.UnlockByContractComplete.Contains(_contract.contractType.name))
                        leaderString += "\n" + cfg.Title;
                }
            }
            if (leaderString != string.Empty)
                value += "\n" + KSP.Localization.Localizer.GetStringByTag(_isReward ? "#rp0LeaderNowAvailable" : "#rp0LeaderMakesAvailable") + leaderString;

            __result = $"<b><color=#{RUIutils.ColorToHex(RichTextUtil.colorAwards)}>{title}:</color></b> {value}";
            if (lines > 0)
                __result += new string('\n', lines);
            return false;
        }
    }
}
