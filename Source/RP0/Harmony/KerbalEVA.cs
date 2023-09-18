using HarmonyLib;
using UnityEngine;
using KSP.Localization;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(KerbalEVA))]
    internal class PatchKerbalEVA
    {
        [HarmonyPrefix]
        [HarmonyPatch("proceedAndBoard")]
        internal static bool Prefix_proceedAndBoard(KerbalEVA __instance, Part p)
        {
            if (__instance.part.protoModuleCrew.Count == 0)
                return true;

            ProtoCrewMember pcm = __instance.part.protoModuleCrew[0];
            if (pcm == null || Crew.CrewHandler.CheckCrewForPart(pcm, p.partInfo.name, true, false))
                return true;

            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowTransferFailFromTraining",
                                         Localizer.Format("#rp0_Crew_TransferFail_Title"),
                                         Localizer.Format("#rp0_Crew_TransferFail_Text"),
                                         Localizer.Format("#autoLOC_190905"),
                                         false,
                                         HighLogic.UISkin,
                                         false).HideGUIsWhilePopup();

            return false;
        }
    }
}
