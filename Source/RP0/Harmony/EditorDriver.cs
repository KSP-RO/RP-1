﻿using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(EditorDriver))]
    internal class PatchEditorDriver
    {
        [HarmonyPrefix]
        [HarmonyPatch("onEditorStarted")]
        internal static void Prefix_onEditorStarted()
        {
            FixActiveLC(EditorDriver.editorFacility);
        }

        [HarmonyPrefix]
        [HarmonyPatch("SwitchEditor")]
        internal static void Prefix_SwitchEditor(EditorFacility facility)
        {
            FixActiveLC(facility);
        }

        private static void FixActiveLC(EditorFacility facility)
        {
            var type = facility == EditorFacility.SPH ? LaunchComplexType.Hangar : LaunchComplexType.Pad;
            if (KerbalConstructionTimeData.Instance?.ActiveSC.ActiveLC.LCType != type)
            {
                for (int i = 0; i < KerbalConstructionTimeData.Instance.ActiveSC.LaunchComplexes.Count; ++i)
                {
                    var lc = KerbalConstructionTimeData.Instance.ActiveSC.LaunchComplexes[i];
                    if (lc.IsOperational && lc.LCType == type)
                    {
                        KerbalConstructionTimeData.Instance.ActiveSC.SwitchLaunchComplex(i);
                        return;
                    }
                }
            }
        }
    }
}