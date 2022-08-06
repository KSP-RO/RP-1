using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(GameSettings))]
    internal class PatchGameSettings
    {
        internal static bool DVSet = false;
        internal static bool DELTAV_APP_ENABLED = true;
        internal static bool DELTAV_CALCULATIONS_ENABLED = true;
        public static void FixDV()
        {
            if (DVSet)
                return;

            DVSet = true;

            DELTAV_APP_ENABLED = GameSettings.DELTAV_APP_ENABLED;
            DELTAV_CALCULATIONS_ENABLED = GameSettings.DELTAV_CALCULATIONS_ENABLED;

            GameSettings.DELTAV_APP_ENABLED = false;
            GameSettings.DELTAV_CALCULATIONS_ENABLED = false;
        }

        internal static System.Type EEXType = AccessTools.TypeByName("EditorExtensionsRedux.EditorExtensions");
        internal static PropertyInfo EEXInstance = EEXType?.GetProperty("Instance", AccessTools.all);
        internal static FieldInfo HotkeyEditor_toggleSymModePrimary = EEXType?.GetField("HotkeyEditor_toggleSymModePrimary", AccessTools.all);
        internal static FieldInfo HotkeyEditor_toggleSymModeSecondary = EEXType?.GetField("HotkeyEditor_toggleSymModeSecondary", AccessTools.all);
        internal static FieldInfo HotkeyEditor_toggleAngleSnapPrimary = EEXType?.GetField("HotkeyEditor_toggleAngleSnapPrimary", AccessTools.all);
        internal static FieldInfo HotkeyEditor_toggleAngleSnapSecondary = EEXType?.GetField("HotkeyEditor_toggleAngleSnapSecondary", AccessTools.all);


        [HarmonyPrefix]
        [HarmonyPatch("WriteCfg")]
        internal static void Prefix_WriteCfg()
        {
            if (EEXType != null)
            {
                Debug.Log("Temporarily changing GameSettings editor keybindings to work around an Editor Extensions Redux issue.");
                object eex = EEXInstance.GetValue(null);
                if (eex != null)
                {
                    PatchEditorExtensionsRedux_Start.toggleSym.primary = HotkeyEditor_toggleSymModePrimary.GetValue(eex) as KeyCodeExtended;
                    PatchEditorExtensionsRedux_Start.toggleSym.secondary = HotkeyEditor_toggleSymModeSecondary.GetValue(eex) as KeyCodeExtended;
                    PatchEditorExtensionsRedux_Start.toggleSnap.primary = HotkeyEditor_toggleAngleSnapPrimary.GetValue(eex) as KeyCodeExtended;
                    PatchEditorExtensionsRedux_Start.toggleSnap.secondary = HotkeyEditor_toggleAngleSnapSecondary.GetValue(eex) as KeyCodeExtended;
                }

                GameSettings.Editor_toggleSymMode.primary = PatchEditorExtensionsRedux_Start.toggleSym.primary;
                GameSettings.Editor_toggleSymMode.secondary = PatchEditorExtensionsRedux_Start.toggleSym.secondary;
                GameSettings.Editor_toggleAngleSnap.primary = PatchEditorExtensionsRedux_Start.toggleSnap.primary;
                GameSettings.Editor_toggleAngleSnap.secondary = PatchEditorExtensionsRedux_Start.toggleSnap.secondary;
            }
            
            FixDV();
            
            GameSettings.DELTAV_APP_ENABLED = DELTAV_APP_ENABLED;
            GameSettings.DELTAV_CALCULATIONS_ENABLED = DELTAV_CALCULATIONS_ENABLED;
        }

        [HarmonyPostfix]
        [HarmonyPatch("WriteCfg")]
        internal static void Postfix_WriteCfg()
        {
            if (EEXType != null)
            {
                Debug.Log("Settings returned.");
                GameSettings.Editor_toggleSymMode.primary = new KeyCodeExtended(KeyCode.None);
                GameSettings.Editor_toggleSymMode.secondary = new KeyCodeExtended(KeyCode.None);
                GameSettings.Editor_toggleAngleSnap.primary = new KeyCodeExtended(KeyCode.None);
                GameSettings.Editor_toggleAngleSnap.secondary = new KeyCodeExtended(KeyCode.None);
            }

            GameSettings.DELTAV_APP_ENABLED = false;
            GameSettings.DELTAV_CALCULATIONS_ENABLED = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("ParseCfg")]
        internal static void Postfix_ParseCfg()
        {
            FixDV();
        }

        [HarmonyPostfix]
        [HarmonyPatch("SetDefaultValues")]
        internal static void Postfix_SetDefaultValues()
        {
            FixDV();

            if (EEXType != null)
            {
                PatchEditorExtensionsRedux_Start.toggleSym.primary = GameSettings.Editor_toggleSymMode.primary;
                PatchEditorExtensionsRedux_Start.toggleSym.secondary = GameSettings.Editor_toggleSymMode.secondary;

                PatchEditorExtensionsRedux_Start.toggleSnap.primary = GameSettings.Editor_toggleAngleSnap.primary;
                PatchEditorExtensionsRedux_Start.toggleSnap.secondary = GameSettings.Editor_toggleAngleSnap.secondary;

                GameSettings.Editor_toggleSymMode.primary = new KeyCodeExtended(KeyCode.None);
                GameSettings.Editor_toggleSymMode.secondary = new KeyCodeExtended(KeyCode.None);
                GameSettings.Editor_toggleAngleSnap.primary = new KeyCodeExtended(KeyCode.None);
                GameSettings.Editor_toggleAngleSnap.secondary = new KeyCodeExtended(KeyCode.None);
            }
        }
    }
}
