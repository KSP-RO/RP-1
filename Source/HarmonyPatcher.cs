using HarmonyLib;
using KerbalConstructionTime;
using KSP.UI.Screens;
using KSP.UI.Screens.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class HarmonyPatcher : MonoBehaviour
    {
        internal void Start()
        {
            var harmony = new Harmony("RP0.HarmonyPatcher");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(ResearchAndDevelopment))]
        internal class PatchRnDPartAvailability
        {
            [HarmonyPrefix]
            [HarmonyPatch("PartTechAvailable")]
            internal static bool Prefix(AvailablePart ap, ref bool __result)
            {
                if (ResearchAndDevelopment.Instance == null)
                {
                    __result = true;
                    return false;
                }

                Dictionary<string, ProtoTechNode> protoTechNodes = GetProtoTechNodes();
                __result = PartTechAvailable(ap, protoTechNodes);

                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch("PartModelPurchased")]
            internal static bool Prefix_PartModelPurchased(AvailablePart ap, ref bool __result)
            {
                if (ResearchAndDevelopment.Instance == null)
                {
                    __result = true;
                    return false;
                }

                Dictionary<string, ProtoTechNode> protoTechNodes = GetProtoTechNodes();

                if (PartTechAvailable(ap, protoTechNodes))
                {
                    if (protoTechNodes.TryGetValue(ap.TechRequired, out ProtoTechNode ptn) &&
                        ptn.partsPurchased.Contains(ap))
                    {
                        __result =  true;
                        return false;
                    }

                    __result = false;
                    return false;
                }

                __result = false;
                return false;
            }

            private static Dictionary<string, ProtoTechNode> GetProtoTechNodes()
            {
                return Traverse.Create(ResearchAndDevelopment.Instance)
                               .Field("protoTechNodes")
                               .GetValue<Dictionary<string, ProtoTechNode>>();
            }

            private static bool PartTechAvailable(AvailablePart ap, Dictionary<string, ProtoTechNode> protoTechNodes)
            {
                if (string.IsNullOrEmpty(ap.TechRequired))
                {
                    return false;
                }

                if (protoTechNodes.TryGetValue(ap.TechRequired, out ProtoTechNode ptn))
                {
                    return ptn.state == RDTech.State.Available;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(RDController))]
        [HarmonyPatch("UpdatePurchaseButton")]
        internal class PatchRnDUpdatePurchaseButton
        {
            internal static bool Prefix(RDController __instance)
            {
                if (KCTGameStates.TechList.Any(tech => tech.TechID == __instance.node_selected.tech.techID))
                {
                    __instance.actionButton.gameObject.SetActive(false);
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PartListTooltip))]
        internal class PatchPartListTooltipSetup
        {
            [HarmonyPostfix]
            [HarmonyPatch("Setup")]
            [HarmonyPatch(new Type[] { typeof(AvailablePart), typeof(Callback<PartListTooltip>), typeof(RenderTexture) })]
            internal static void Postfix_Setup1(PartListTooltip __instance, AvailablePart availablePart, bool ___requiresEntryPurchase)
            {
                PatchButtons(__instance, availablePart, null, ___requiresEntryPurchase);
            }

            [HarmonyPostfix]
            [HarmonyPatch("Setup")]
            [HarmonyPatch(new Type[] { typeof(AvailablePart), typeof(PartUpgradeHandler.Upgrade), typeof(Callback<PartListTooltip>), typeof(RenderTexture) })]
            internal static void Postfix_Setup2(PartListTooltip __instance, AvailablePart availablePart, PartUpgradeHandler.Upgrade up, bool ___requiresEntryPurchase)
            {
                PatchButtons(__instance, null, up, ___requiresEntryPurchase);
            }

            private static void PatchButtons(PartListTooltip __instance, AvailablePart availablePart, PartUpgradeHandler.Upgrade up, bool ___requiresEntryPurchase)
            {
                if (___requiresEntryPurchase && KCTGameStates.TechList.Any(tech => tech.TechID == (availablePart?.TechRequired ?? up.techRequired)))
                {
                    __instance.buttonPurchaseContainer.SetActive(false);
                    __instance.costPanel.SetActive(true);
                }
            }
        }
    
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
}
