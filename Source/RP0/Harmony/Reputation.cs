using HarmonyLib;
using KSP.UI.Screens;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Reputation))]
    internal class PatchReputation
    {
        [HarmonyPrefix]
        [HarmonyPatch("addReputation_granular")]
        internal static bool Prefix_addReputation_granular(Reputation __instance, float value, out float __result)
        {
            __instance.rep = __instance.rep + value;
            __result = value;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnCrewKilled")]
        internal static bool Prefix_OnCrewKilled(Reputation __instance, EventReport evt)
        {
            if (evt.eventType == FlightEvents.CREW_KILLED)
            {
                float nonCrewMult = 1f;
                if (HighLogic.CurrentGame?.CrewRoster[evt.sender]?.type == ProtoCrewMember.KerbalType.Tourist)
                    nonCrewMult = 0.3f;
                float repFixed = HighLogic.CurrentGame?.Parameters.CustomParams<RP0Settings>()?.RepLossNautDeathFixed ?? 0f;
                float repPct = HighLogic.CurrentGame?.Parameters.CustomParams<RP0Settings>()?.RepLossNautDeathPercent ?? 0f;
                __instance.AddReputation(-1f * nonCrewMult * (repFixed + repPct * __instance.reputation), TransactionReasonsRP0.LossOfCrew.Stock());
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("AddReputation")]
        internal static bool Prefix_AddReputation(Reputation __instance, out float __result, float r, TransactionReasons reason)
        {
            __instance.rep += r;
            CurrencyModifierQueryRP0 data = new CurrencyModifierQueryRP0(reason, 0f, 0f, r);
            GameEvents.Modifiers.OnCurrencyModifierQuery.Fire(data);
            GameEvents.Modifiers.OnCurrencyModified.Fire(data);
            if (reason == TransactionReasons.None)
            {
                RP0Debug.Log($"Added {r} ({r}) reputation. Total Rep: {__instance.rep}");
            }
            else
            {
                RP0Debug.Log($"Added {r} ({r}) reputation: '{reason}'.");
            }

            if (r != 0f)
            {
                GameEvents.OnReputationChanged.Fire(__instance.rep, reason);
            }
            __result = r;

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("onvesselRecoveryProcessing")]
        internal static bool Prefix_onvesselRecoveryProcessing(Reputation __instance, ProtoVessel pv, MissionRecoveryDialog mrDialog, float recoveryScore)
        {
            if (mrDialog != null)
                mrDialog.reputationEarned = 0f;

            return false;
        }
    }
}
