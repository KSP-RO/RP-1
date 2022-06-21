using HarmonyLib;
using KSP.UI.Screens;
using KSP.UI;
using Strategies;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using RP0.Programs;
using UniLinq;

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
        [HarmonyPatch(typeof(Administration))]
        internal class PatchAdministration
        {
            internal static FieldInfo activeStrategyCountField = typeof(Administration).GetField("activeStrategyCount", BindingFlags.NonPublic | BindingFlags.Instance);
            internal static FieldInfo maxActiveStrategiesField = typeof(Administration).GetField("maxActiveStrategies", BindingFlags.NonPublic | BindingFlags.Instance);
            internal static List<Strategy> strategies = new List<Strategy>();

            [HarmonyPrefix]
            [HarmonyPatch("CreateActiveStratList")]
            internal static bool Prefix_CreateActiveStratList(Administration __instance)
            {
                __instance.scrollListActive.Clear(true);
                Administration.StrategyWrapper wrapper = null;

                if (AdminUIFixer.Instance.ActiveTabView == AdministrationActiveTabView.Leaders)
                {
                    foreach (var s in StrategySystem.Instance.Strategies)
                        if (s.IsActive && !(s is ProgramStrategy))
                            strategies.Add(s);
                }
                else
                {
                    List<Program> programs = AdminUIFixer.Instance.ActiveTabView == AdministrationActiveTabView.Active ? ProgramHandler.Instance.ActivePrograms : ProgramHandler.Instance.CompletedPrograms;
                    foreach (Program p in programs)
                    {
                        Strategy strategy = StrategySystem.Instance.Strategies.Find(s => s.Config.Name == p.name);
                        if (strategy == null)
                            continue;

                        // Just in case. This should never happen unless you use the debugging UI...
                        if (AdminUIFixer.Instance.ActiveTabView == AdministrationActiveTabView.Active && !strategy.IsActive)
                        {
                            strategy.Activate();
                        }

                        strategies.Add(strategy);
                    }
                }
                foreach (Strategy strategy in strategies)
                {
                    UIListItem item = UnityEngine.Object.Instantiate(__instance.prefabActiveStrat);
                    ActiveStrategyListItem stratItem = item.GetComponent<ActiveStrategyListItem>();
                    UIRadioButton button = item.GetComponent<UIRadioButton>();
                    wrapper = new Administration.StrategyWrapper(strategy, button);
                    button.Data = wrapper;
                    button.onTrue.AddListener(wrapper.OnTrue);
                    button.onFalse.AddListener(wrapper.OnFalse);
                    Texture icon = wrapper.strategy.Config.IconImage;
                    if (icon == null)
                    {
                        icon = __instance.defaultIcon;
                    }
                    stratItem.Setup("<b><color=" + XKCDColors.HexFormat.KSPBadassGreen + ">" + strategy.Title + "</color></b>", strategy.Effect, icon as Texture2D);

                    __instance.scrollListActive.AddItem(item);
                }
                strategies.Clear();

                activeStrategyCountField.SetValue(__instance, ProgramHandler.Instance.ActivePrograms.Count);
                maxActiveStrategiesField.SetValue(__instance, ProgramHandler.Instance.ActiveProgramLimit);
                __instance.activeStratCount.text = KSP.Localization.Localizer.Format("#autoLOC_439627", ProgramHandler.Instance.ActivePrograms.Count, ProgramHandler.Instance.ActiveProgramLimit);

                return false;
            }

            internal static string cancelTooltipStr = String.Empty;
            internal static string acceptTooltipStr = String.Empty;

            [HarmonyPrefix]
            [HarmonyPatch("SetSelectedStrategy")]
            internal static void Prefix_SetSelectedStrategy(Administration __instance, ref Administration.StrategyWrapper wrapper)
            {
                // Cache the loc strings, update tooltip
                var tooltip = __instance.btnAcceptCancel.GetComponent<KSP.UI.TooltipTypes.UIStateButtonTooltip>();
                if (tooltip != null)
                {
                    if (string.IsNullOrEmpty(cancelTooltipStr))
                        cancelTooltipStr = tooltip.tooltipStates.First(s => s.name == "cancel").tooltipText;
                    if (string.IsNullOrEmpty(acceptTooltipStr))
                        acceptTooltipStr = tooltip.tooltipStates.First(s => s.name == "accept").tooltipText;
                    else // update to the normal one
                        tooltip.tooltipStates.First(s => s.name == "accept").tooltipText = acceptTooltipStr;
                }

                if (wrapper.strategy is ProgramStrategy ps)
                {
                    ps.NextTextIsShowSelected = true; // pass through we're about to print the long-form description.
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch("SetSelectedStrategy")]
            internal static void Postfix_SetSelectedStrategy(Administration __instance, ref Administration.StrategyWrapper wrapper)
            {
                // If it's a Program, we always want to show the green checkmark
                // not the red x.
                if (wrapper.strategy is ProgramStrategy ps)
                {
                    string name = ps.Config.Name;
                    Program program = ProgramHandler.Instance.CompletedPrograms.Find(p => p.name == name);
                    if (program != null)
                        __instance.btnAcceptCancel.gameObject.SetActive(false);
                    else if (__instance.btnAcceptCancel.currentState != "accept")
                    {
                        // Use the "deactivate" tooltip for the accept button
                        var tooltip = __instance.btnAcceptCancel.GetComponent<KSP.UI.TooltipTypes.UIStateButtonTooltip>();
                        if (tooltip != null)
                            tooltip.tooltipStates.First(s => s.name == "accept").tooltipText = cancelTooltipStr;
                        __instance.btnAcceptCancel.SetState("accept");
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch("BtnInputAccept")]
            internal static void Prefix_BtnInputAccept(Administration __instance, ref string state)
            {
                // We're changing the button to always be the checkmark.
                // But that means if this is an active strategy, we need
                // to change what state this handler thinks we're in
                // so we complete the contract.
                if (__instance.SelectedWrapper.strategy is ProgramStrategy ps)
                {
                    if (ps.IsActive)
                        state = "cancel";
                }
            }
        }

        [HarmonyPatch(typeof(StrategySystemConfig))]
        internal class PatchStrategySystemConfig
        {
            [HarmonyPostfix]
            [HarmonyPatch("LoadStrategyConfigs")]
            internal static void Postfix_LoadStrategyConfigs(StrategySystemConfig __instance)
            {
                ProgramHandler.EnsurePrograms();
                foreach (Program p in ProgramHandler.Programs)
                {
                    Debug.Log($"Added Program {p.name} as config");
                    System.Type type = StrategySystem.GetStrategyType(p.name);
                    if (type == null)
                        Debug.LogError("ERROR! Can't find type!");
                    else
                        Debug.Log($"Found type: {type.FullName}");
                    ConfigNode node = new ConfigNode();
                    node.AddValue("name", p.name);
                    node.AddValue("department", "Programs");
                    node.AddValue("title", p.title);
                    node.AddValue("desc", p.description);
                    if (!string.IsNullOrEmpty(p.icon))
                        node.AddValue("icon", p.icon);
                    node.AddValue("groupTag", p.name); // will never conflict except with itself.
                    StrategyConfig cfg = StrategyConfig.Create(node, __instance.Departments);
                    __instance.Strategies.Add(cfg);
                }
            }
        }

        //[HarmonyPatch(typeof(Strategy))]
        //internal class PatchStrategy
        //{
        //    [HarmonyPrefix]
        //    [HarmonyPatch("Activate")]
        //    internal static bool Prefix_Activate(Strategy __instance, ref bool __result)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            __result = v.Activate();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("CanBeActivated")]
        //    internal static bool Prefix_CanBeActivated(Strategy __instance, ref bool __result, ref string reason)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            __result = v.CanBeActivated(out reason);
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("CanBeDeactivated")]
        //    internal static bool Prefix_CanBeDeactivated(Strategy __instance, ref bool __result, ref string reason)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            __result = v.CanBeDeactivated(out reason);
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Deactivate")]
        //    internal static bool Prefix_Deactivate(Strategy __instance, ref bool __result)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            __result = v.Deactivate();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Register")]
        //    internal static bool Prefix_Register(Strategy __instance)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Register();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Unregister")]
        //    internal static bool Prefix_Unregister(Strategy __instance)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Unregister();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Update")]
        //    internal static bool Prefix_Update(Strategy __instance)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Update();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Load")]
        //    internal static bool Prefix_Load(Strategy __instance, ref ConfigNode node)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Load(node);
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Save")]
        //    internal static bool Prefix_Save(Strategy __instance, ref ConfigNode node)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Save(node);
        //            return false;
        //        }

        //        return true;
        //    }
        //}
    }
}