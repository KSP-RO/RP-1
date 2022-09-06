using HarmonyLib;
using KerbalConstructionTime;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(EditorDriver))]
    internal class PatchEditorDriver
    {
        [HarmonyPrefix]
        [HarmonyPatch("StartEditor")]
        internal static void Prefix_StartEditor(EditorFacility facility)
        {
            FixActiveLC(facility);
        }

        [HarmonyPrefix]
        [HarmonyPatch("StartAndLoadVessel")]
        internal static void Prefix_StartAndLoadVessel(EditorFacility facility)
        {
            FixActiveLC(facility);
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
            if (KCTGameStates.ActiveKSC?.ActiveLaunchComplexInstance.LCType != type)
            {
                for (int i = 0; i < KCTGameStates.ActiveKSC.LaunchComplexes.Count; ++i)
                {
                    var lc = KCTGameStates.ActiveKSC.LaunchComplexes[i];
                    if (lc.IsOperational && lc.LCType == type)
                    {
                        KCTGameStates.ActiveKSC.SwitchLaunchComplex(i);
                        return;
                    }
                }
            }
        }
    }
}