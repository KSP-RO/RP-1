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
using KSP.Localization;

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
        [HarmonyPatch(typeof(Administration))]
        internal class PatchAdministration
        {
            [HarmonyPrefix]
            [HarmonyPatch("Start")]
            internal static void Prefix_Start(Administration __instance, ref int ___activeStrategyCount, ref int ___maxActiveStrategies)
            {
                var adminExt = __instance.gameObject.GetComponent<AdminExtender>() ?? __instance.gameObject.AddComponent<AdminExtender>();
                adminExt.BindAndFixUI();
            }

            [HarmonyPostfix]
            [HarmonyPatch("Start")]
            internal static void Postfix_Start(Administration __instance, ref int ___activeStrategyCount, ref int ___maxActiveStrategies)
            {
                ___activeStrategyCount = ProgramHandler.Instance.ActivePrograms.Count;
                ___maxActiveStrategies = ProgramHandler.Instance.ActiveProgramLimit;
            }

            [HarmonyPrefix]
            [HarmonyPatch("CreateStrategiesList")]
            internal static void Prefix_CreateStrategiesList(Administration __instance)
            {
                Transform tSpacer1 = __instance.scrollListKerbals.transform.Find("DepartmentSpacer1");
                if (tSpacer1 != null)
                    GameObject.Destroy(tSpacer1.gameObject);

                Transform tSpacer2 = __instance.scrollListKerbals.transform.Find("DepartmentSpacer2");
                if(tSpacer2 != null)
                    GameObject.Destroy(tSpacer2.gameObject);
            }

            [HarmonyPostfix]
            [HarmonyPatch("CreateStrategiesList")]
            internal static void Postfix_CreateStrategiesList(Administration __instance)
            {
                __instance.scrollListStrategies.GetUilistItemAt(0).GetComponent<LayoutElement>().minWidth = 280f;
                var firstDep = __instance.scrollListKerbals.GetUilistItemAt(0);
                GameObject spacer = GameObject.Instantiate(firstDep.gameObject);
                spacer.name = "DepartmentSpacer1";
                for (int i = spacer.transform.childCount - 1; i >= 0; --i)
                    GameObject.DestroyImmediate(spacer.transform.GetChild(i).gameObject);

                GameObject.DestroyImmediate(spacer.GetComponent<KerbalListItem>());
                GameObject.DestroyImmediate(spacer.GetComponent<Image>());
                GameObject.DestroyImmediate(spacer.GetComponent<UIListItem>());


                spacer.GetComponent<LayoutElement>().minWidth = 70f;
                spacer.transform.SetParent(firstDep.transform.parent, false);
                spacer.transform.SetAsFirstSibling();

                GameObject spacer2 = GameObject.Instantiate(spacer);
                spacer2.name = "DepartmentSpacer2";
                spacer2.transform.SetParent(spacer.transform.parent, false);
                spacer2.transform.SetSiblingIndex(firstDep.transform.GetSiblingIndex() + 1);
            }

            internal static List<Strategy> strategies = new List<Strategy>();

            [HarmonyPrefix]
            [HarmonyPatch("CreateActiveStratList")]
            internal static bool Prefix_CreateActiveStratList(Administration __instance, ref int ___activeStrategyCount, ref int ___maxActiveStrategies)
            {
                __instance.scrollListActive.Clear(true);
                Administration.StrategyWrapper wrapper = null;

                if (AdminExtender.Instance.ActiveTabView == AdministrationActiveTabView.Leaders)
                {
                    foreach (var s in StrategySystem.Instance.Strategies)
                        if (s.IsActive && !(s is ProgramStrategy))
                            strategies.Add(s);
                }
                else
                {
                    List<Program> programs = AdminExtender.Instance.ActiveTabView == AdministrationActiveTabView.Active ? ProgramHandler.Instance.ActivePrograms : ProgramHandler.Instance.CompletedPrograms;
                    foreach (Program p in programs)
                    {
                        Strategy strategy = StrategySystem.Instance.Strategies.Find(s => s.Config.Name == p.name);
                        if (strategy == null)
                            continue;

                        // Just in case. This should never happen unless you use the debugging UI...
                        if (AdminExtender.Instance.ActiveTabView == AdministrationActiveTabView.Active && !strategy.IsActive)
                        {
                            strategy.Activate();
                        }

                        if (strategy is ProgramStrategy ps)
                        {
                            if (ps.Program != p)
                                Debug.LogError($"[RP-0] ProgramStrategy binding mismatch for completed program! Strat {strategy.Config.Name} is bound to program that is not the same. Null? {ps.Program == null}");
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

                ___activeStrategyCount = ProgramHandler.Instance.ActivePrograms.Count;
                ___maxActiveStrategies = ProgramHandler.Instance.ActiveProgramLimit;
                __instance.activeStratCount.text = Localizer.Format("#autoLOC_439627", ProgramHandler.Instance.ActivePrograms.Count, ProgramHandler.Instance.ActiveProgramLimit);

                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch("SetSelectedStrategy")]
            internal static void Prefix_SetSelectedStrategy(Administration __instance, ref Administration.StrategyWrapper wrapper)
            {
                if (wrapper.strategy is ProgramStrategy ps)
                {
                    ps.NextTextIsShowSelected = true; // pass through we're about to print the long-form description.

                    // Set best speed before we get description
                    if (!ps.Program.IsComplete && !ps.Program.IsActive && !AdminExtender.Instance.PressedSpeedButton)
                        ps.Program.SetBestAllowableSpeed();
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch("SetSelectedStrategy")]
            internal static void Postfix_SetSelectedStrategy(Administration __instance, ref Administration.StrategyWrapper wrapper)
            {
                var tooltip = __instance.btnAcceptCancel.GetComponent<KSP.UI.TooltipTypes.UIStateButtonTooltip>();

                // If it's a Program, we always want to show the green checkmark
                // not the red x.
                if (wrapper.strategy is ProgramStrategy ps)
                {
                    if (ps.Program.IsComplete)
                        __instance.btnAcceptCancel.gameObject.SetActive(false);
                    else if (__instance.btnAcceptCancel.currentState == "accept")
                    {
                        float cost = ps.Program.TrustCost;
                        var stateAccept = tooltip.tooltipStates.First(s => s.name == "accept");
                        if (cost > 0)
                            stateAccept.tooltipText = Localizer.Format("#rp0AcceptProgramWithCost", cost.ToString("N0"));
                        else
                            stateAccept.tooltipText = Localizer.GetStringByTag("#autoLOC_900259");
                    }
                    else
                    {
                        // Use the "deactivate" tooltip for the accept button
                        tooltip.tooltipStates.First(s => s.name == "accept").tooltipText = Localizer.GetStringByTag("#autoLOC_900260");
                        __instance.btnAcceptCancel.SetState("accept");
                    }

                    AdminExtender.Instance.SetSpeedButtonsActive(!ps.Program.IsActive && !ps.Program.IsComplete, ps.Program);
                }
                else
                {
                    tooltip.tooltipStates.First(s => s.name == "accept").tooltipText = Localizer.GetStringByTag("#rp0AppointLeader");
                    tooltip.tooltipStates.First(s => s.name == "cancel").tooltipText = Localizer.GetStringByTag("#rp0RemoveLeader");
                    AdminExtender.Instance.SetSpeedButtonsActive(false, null);
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
    }
}