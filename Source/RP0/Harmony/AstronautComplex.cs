using HarmonyLib;
using UnityEngine;
using KSP.UI;
using KSP.UI.Screens;
using RP0.Crew;
using KSP.Localization;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(AstronautComplex))]
    internal class PatchAstronautComplex
    {
        public static AstronautComplex Instance { get; private set; }

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        internal static void Postfix_Awake(AstronautComplex __instance)
        {
            if (Instance != __instance)
                Object.Destroy(Instance);

            Instance = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDestroy")]
        internal static void Postfix_OnDestroy(AstronautComplex __instance)
        {
            Instance = null;
        }

        // These are done as postfix, not replace-prefix, for compatibility with anything else touching them.
        [HarmonyPostfix]
        [HarmonyPatch("AddItem_Available")]
        internal static void Postfix_AddItem_Available(AstronautComplex __instance, ProtoCrewMember crew)
        {
            if (CrewHandler.Instance == null)
                return;

            if (crew.inactive)
            {
                var cli = __instance.scrollListAvailable.list.items[__instance.scrollListAvailable.list.Count - 1].listItem.GetComponent<CrewListItem>();
                cli.MouseoverEnabled = false;

                double time = CrewHandler.Instance.GetTrainingFinishTime(crew);
                string label;
                if (time > 0)
                {
                    label = "#rp0_AC_Crew_Status_Training";
                }
                else
                {
                    time = crew.inactiveTimeEnd;
                    label = "#rp0_AC_Crew_Status_Recovering";
                }

                cli.SetLabel(Localizer.Format(label, KSPUtil.PrintDate(time, false)));
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("HireRecruit")]
        internal static void Postfix_HireRecruit(AstronautComplex __instance, UIList tolist)
        {
            if (tolist == __instance.scrollListAvailable)
            {
                var cli = tolist.list.items[tolist.list.items.Count - 1].listItem.GetComponent<CrewListItem>();
                Postfix_AddItem_Available(__instance, cli.GetCrewRef());
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("AddItem_Kia")]
        internal static void Postfix_AddItem_Kia(AstronautComplex __instance, ProtoCrewMember crew)
        {
            if (CrewHandler.Instance == null || !CrewHandler.Instance.RetirementEnabled || !CrewHandler.Instance.IsRetired(crew))
                return;

            var cli = __instance.scrollListKia.list.items[__instance.scrollListKia.list.Count - 1].listItem.GetComponent<CrewListItem>();
            cli.SetLabel(Localizer.Format("#rp0_AC_Crew_Status_Retired"));
            cli.MouseoverEnabled = false;
        }
    }
}
