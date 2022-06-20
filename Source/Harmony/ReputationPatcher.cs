using HarmonyLib;
using KSP.UI.Screens;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
        [HarmonyPatch(typeof(Reputation))]
        internal class PatchReputation
        {
            private static FieldInfo repField = typeof(Reputation).GetField("rep", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            [HarmonyPatch("addReputation_granular")]
            internal static bool Prefix_addReputation_granular(Reputation __instance, ref float value, ref float __result)
            {
                repField.SetValue(__instance, __instance.reputation + value);
                __result = value;
                return false;
            }

            [HarmonyPatch("OnCrewKilled")]
            internal static bool Prefix_OnCrewKilled(Reputation __instance, ref EventReport evt)
            {
                if (evt.eventType == FlightEvents.CREW_KILLED)
                {
                    float repFixed = HighLogic.CurrentGame?.Parameters.CustomParams<RP0Settings>()?.RepLossKerbalDeathFixed ?? 0f;
                    float repPct = HighLogic.CurrentGame?.Parameters.CustomParams<RP0Settings>()?.RepLossKerbalDeathPercent ?? 0f;
                    __instance.AddReputation(-1f * (repFixed + repPct * __instance.reputation), TransactionReasons.VesselLoss);
                }
                return false;
            }

            [HarmonyPatch("onvesselRecoveryProcessing")]
            internal static bool Prefix_onvesselRecoveryProcessing(Reputation __instance, ref ProtoVessel pv, ref MissionRecoveryDialog mrDialog, ref float recoveryScore)
            {
                if (mrDialog != null)
                    mrDialog.reputationEarned = 0f;

                return false;
            }
        }

        [HarmonyPatch]
        internal class PatchPlayerProfileInfo_LoadDetailsFromGame
        {
            static MethodBase TargetMethod() => typeof(LoadGameDialog).GetNestedType("PlayerProfileInfo", AccessTools.all).GetMethod("LoadDetailsFromGame", AccessTools.all);

            [HarmonyTranspiler]
            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    if (instruction.LoadsConstant(10f))
                        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_R4, 1f);
                    else
                        yield return instruction;
                }
            }
        }

        [HarmonyPatch(typeof(ReputationWidget))]
        internal class PatchReputationWidget
        {
            public static TextMeshProUGUI RepLabel;

            [HarmonyPrefix]
            [HarmonyPatch("onReputationChanged")]
            internal static bool Prefix_onReputationChanged(float rep, TransactionReasons reason)
            {
                if (RepLabel != null)
                {
                    RepLabel.text = KSPUtil.LocalizeNumber(rep, "0.0");
                }

                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch("DelayedStart")]
            internal static bool Prefix_DelayedStart(ReputationWidget __instance)
            {
                GameObject.DestroyImmediate(__instance.gauge);
                __instance.gauge = null;
                GameObject.DestroyImmediate(__instance.gameObject.transform.Find("circularGauge").gameObject);

                var frameImage = (Image)__instance.gameObject.GetComponentInChildren(typeof(Image));

                var img = GameObject.Instantiate(new GameObject("repBackground"), __instance.transform, worldPositionStays: false).AddComponent<Image>();
                img.color = new Color32(58, 58, 63, 255);
                img.rectTransform.anchorMin = frameImage.rectTransform.anchorMin;
                img.rectTransform.anchorMax = frameImage.rectTransform.anchorMax;
                img.rectTransform.anchoredPosition = frameImage.rectTransform.anchoredPosition;
                img.rectTransform.sizeDelta = ((RectTransform)__instance.gameObject.transform).sizeDelta;    // No idea why the frame image transform is larger than the component itself

                RepLabel = GameObject.Instantiate(new GameObject("repLabel"), __instance.transform, worldPositionStays: false).AddComponent<TextMeshProUGUI>();
                RepLabel.alignment = TextAlignmentOptions.Right;
                RepLabel.color = XKCDColors.Mustard;
                RepLabel.fontSize = 22;
                RepLabel.rectTransform.localPosition = new Vector3(-9, -1, 0);
                RepLabel.fontStyle = FontStyles.Bold;

                return true;
            }
        }
    }
}
