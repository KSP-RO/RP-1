using HarmonyLib;
using KerbalConstructionTime;
using KSP.UI;
using System;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(BaseCrewAssignmentDialog))]
    internal class PatchBaseCrewAssignmentDialog
    {
        private static bool _currentPcmIsInactive;

        /// <summary>
        /// Clobbers all crewmembers to active in Editor so that they can be used in simulations.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="crew"></param>
        [HarmonyPrefix]
        [HarmonyPatch("AddAvailItem", new Type[] { typeof(ProtoCrewMember), typeof(CrewListItem), typeof(UIList), typeof(CrewListItem.ButtonTypes) },
                      new ArgumentType[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal })]
        internal static void Prefix_CreateAvailList(BaseCrewAssignmentDialog __instance, ProtoCrewMember crew)
        {
            if (!ShouldClobberCrewToActive()) return;
            _currentPcmIsInactive = crew.inactive;
            crew.inactive = false;
        }

        /// <summary>
        /// Reverts the clobbering done in Prefix.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="crew"></param>
        [HarmonyPostfix]
        [HarmonyPatch("AddAvailItem", new Type[] { typeof(ProtoCrewMember), typeof(CrewListItem), typeof(UIList), typeof(CrewListItem.ButtonTypes) },
                      new ArgumentType[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal })]
        internal static void Postfix_CreateAvailList(BaseCrewAssignmentDialog __instance, ProtoCrewMember crew)
        {
            if (!ShouldClobberCrewToActive()) return;
            crew.inactive = _currentPcmIsInactive;
        }

        private static bool ShouldClobberCrewToActive()
        {
            return HighLogic.LoadedSceneIsEditor && !KCT_GUI.IsPrimarilyDisabled;
        }
    }
}
