using HarmonyLib;
using KSP.UI;
using System;
using System.Collections.Generic;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(BaseCrewAssignmentDialog))]
    internal class PatchBaseCrewAssignmentDialog
    {
        private static readonly ProtoCrewMember.KerbalType[] _kerbalTypesToAdd = new[] { ProtoCrewMember.KerbalType.Crew, ProtoCrewMember.KerbalType.Tourist, ProtoCrewMember.KerbalType.Applicant };
        private static bool _currentPcmIsInactive;

        /// <summary>
        /// Clobbers all crewmembers to active in Editor so that they can be used in simulations.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="crew"></param>
        [HarmonyPrefix]
        [HarmonyPatch("AddAvailItem", new Type[] { typeof(ProtoCrewMember), typeof(CrewListItem), typeof(UIList), typeof(CrewListItem.ButtonTypes) },
                      new ArgumentType[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal })]
        internal static void Prefix_AddAvailItem(BaseCrewAssignmentDialog __instance, ProtoCrewMember crew)
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
        internal static void Postfix_AddAvailItem(BaseCrewAssignmentDialog __instance, ProtoCrewMember crew)
        {
            if (!ShouldClobberCrewToActive()) return;
            crew.inactive = _currentPcmIsInactive;
        }

        /// <summary>
        /// Will make all applicants available for selection in Editor
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="manifest"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch("CreateAvailList")]
        internal static bool Prefix_CreateAvailList(BaseCrewAssignmentDialog __instance, VesselCrewManifest manifest)
        {
            if (!ShouldClobberCrewToActive()) return true;

            if (manifest == null)
            {
                return false;
            }
            __instance.scrollListAvail.Clear(destroyElements: true);

            foreach (ProtoCrewMember.KerbalType kType in _kerbalTypesToAdd)
            {
                IEnumerator<ProtoCrewMember> enumerator = __instance.CurrentCrewRoster.Kerbals(kType, ProtoCrewMember.RosterStatus.Available).GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (!manifest.Contains(enumerator.Current))
                    {
                        __instance.AddAvailItem(enumerator.Current);
                    }
                }
            }

            return false;
        }

        private static bool ShouldClobberCrewToActive()
        {
            return HighLogic.LoadedSceneIsEditor && !KCT_GUI.IsPrimarilyDisabled;
        }
    }
}
