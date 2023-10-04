using HarmonyLib;
using KSP.UI;
using RP0.Crew;
using KSP.Localization;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(CrewListItem))]
    internal class PatchCrewListItem
    {
        // Kerbalism patches the tooltip controller directly
        // We are postfixing the CLI that calls the TTC's SetTooltip
        // so we run after.
        [HarmonyPostfix]
        [HarmonyPatch("SetTooltip")]
        internal static void Postfix_SetTooltip(CrewListItem __instance, ProtoCrewMember crew)
        {
            if (CrewHandler.Instance == null)
                return;

            if (crew.rosterStatus != ProtoCrewMember.RosterStatus.Dead && CrewHandler.Instance.RetirementEnabled)
            {
                double retireUT = CrewHandler.Instance.GetRetireTime(crew.name);
                if (retireUT > 0d)
                    __instance.tooltipController.descriptionString += Localizer.Format("#rp0_AC_Crew_Tooltip_RetireDate", KSPUtil.PrintDate(retireUT, false));
            }

            string trainingStr = CrewHandler.Instance.GetTrainingString(crew);
            if (!string.IsNullOrEmpty(trainingStr))
                __instance.tooltipController.descriptionString += trainingStr;
        }
    }
}
