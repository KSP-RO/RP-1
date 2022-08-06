using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace RP0.Harmony
{
    [HarmonyPatch]
    internal class PatchEditorExtensionsRedux_Start
    {
        static MethodBase TargetMethod() => AccessTools.TypeByName("EditorExtensionsRedux.EditorExtensions")?.GetMethod("Start", AccessTools.all);

        internal static System.Type EEXType = AccessTools.TypeByName("EditorExtensionsRedux.EditorExtensions");

        [HarmonyPrepare]
        internal static bool Prepare()
        {
            return EEXType != null;
        }

        internal static KeyBinding toggleSym = new KeyBinding(KeyCode.None);
        internal static KeyBinding toggleSnap = new KeyBinding(KeyCode.None);


        [HarmonyPrefix]
        internal static void Prefix_Start()
        {
            if (EEXType != null)
            {
                toggleSym.primary = GameSettings.Editor_toggleSymMode.primary;
                toggleSym.secondary = GameSettings.Editor_toggleSymMode.secondary;

                toggleSnap.primary = GameSettings.Editor_toggleAngleSnap.primary;
                toggleSnap.secondary = GameSettings.Editor_toggleAngleSnap.secondary;
            }
        }
    }
}
