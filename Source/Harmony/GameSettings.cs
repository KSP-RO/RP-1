using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(GameSettings))]
    internal class PatchGameSettings
    {
        internal static System.Type EEXType = AccessTools.TypeByName("EditorExtensionsRedux.EditorExtensions");
        internal static FieldInfo EEXInstance = EEXType?.GetField("Instance");
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
                GameSettings.Editor_toggleSymMode.primary = HotkeyEditor_toggleSymModePrimary.GetValue(eex) as KeyCodeExtended;
                GameSettings.Editor_toggleSymMode.secondary = HotkeyEditor_toggleSymModeSecondary.GetValue(eex) as KeyCodeExtended;
                GameSettings.Editor_toggleAngleSnap.primary = HotkeyEditor_toggleAngleSnapPrimary.GetValue(eex) as KeyCodeExtended;
                GameSettings.Editor_toggleAngleSnap.secondary = HotkeyEditor_toggleAngleSnapSecondary.GetValue(eex) as KeyCodeExtended;
            }
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
        }
    }
}
