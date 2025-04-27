using Contracts;
using HarmonyLib;
using RP0.Leaders;
using UniLinq;

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
        internal static bool Postfix_TextAward(ref string __result, string title, string value, int lines)
        {
            if (_contract == null)
                return true;

            if (Programs.ProgramHandler.Instance != null && Programs.ProgramHandler.Instance.RepToConfidenceForContract(_contract, _isReward) is float repToConf && repToConf > 0f)
            {
                var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ContractReward, 0d, 0d, 0d, _contract.ReputationCompletion * repToConf, 0d);
                value += $"<color={CurrencyModifierQueryRP0.CurrencyColor(CurrencyRP0.Confidence)}>{CurrencyModifierQueryRP0.SpriteString(CurrencyRP0.Confidence)} {cmq.GetTotal(CurrencyRP0.Confidence):N0} {cmq.GetEffectDeltaText(CurrencyRP0.Confidence, "N0", CurrencyModifierQuery.TextStyling.OnGUI)}  </color>";
            }

            if (PresetManager.Instance != null)
            {
                int applicants = Database.SettingsSC.ContractApplicants.GetApplicantsFromContract(_contract.contractType.name);
                if (applicants > 0)
                    value += $"\n{KSP.Localization.Localizer.Format("#rp0_ContractRewards_GainApplicants", applicants)}";
            }

            var leaderTitles = LeaderUtils.GetLeadersUnlockedByContract(_contract)
                .Where(s => !_isReward || !s.IsUnlocked())
                .Select(s => s.title);
            string leaderString = string.Join("\n", leaderTitles);
            if (!string.IsNullOrEmpty(leaderString))
                value += "\n" + KSP.Localization.Localizer.Format(_isReward ? "#rp0_Leaders_LeadersUnlocked" : "#rp0_Leaders_UnlocksLeader") + leaderString;

            __result = $"<b><color=#{RUIutils.ColorToHex(RichTextUtil.colorAwards)}>{title}:</color></b> {value}";
            if (lines > 0)
                __result += new string('\n', lines);
            return false;
        }
    }
}
