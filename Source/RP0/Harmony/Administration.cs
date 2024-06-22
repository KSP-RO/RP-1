using HarmonyLib;
using KSP.UI.Screens;
using KSP.UI;
using Strategies;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RP0.Programs;
using UniLinq;
using KSP.Localization;
using ROUtils;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Administration))]
    internal class PatchAdministration
    {
        internal static void FixProgramCounts(Administration __instance)
        {
            // We need to reset the strategy count because we only want to track programs, not leaders too.
            __instance.activeStrategyCount = ProgramHandler.Instance.ActiveProgramSlots;
            __instance.maxActiveStrategies = ProgramHandler.Instance.MaxProgramSlots;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Start")]
        internal static void Prefix_Start(Administration __instance)
        {
            // We need another component to live on the Admin UI because we can only
            // use harmony to override methods, not add fields, and we need
            // to store a bunch of references to new objects.
            var adminExt = __instance.gameObject.GetComponent<AdminExtender>() ?? __instance.gameObject.AddComponent<AdminExtender>();
            adminExt.BindAndFixUI();
        }

        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        internal static void Postfix_Start(Administration __instance)
        {
            FixProgramCounts(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch("CreateStrategiesList")]
        internal static void Prefix_CreateStrategiesList(Administration __instance)
        {
            FixProgramCounts(__instance);

            // First we need to find each program (well, each ProgramStrategy) and
            // figure out what its best allowable speed is, because that depends on
            // the current confidence quantity.
            foreach (var strat in StrategySystem.Instance.GetStrategies("Programs"))
            {
                if (strat is ProgramStrategy ps)
                {
                    ps.Program.SetBestAllowableSpeed();
                }
            }

            // We'll also kill the old spacers since we create them again in the Postfix of this method.
            Transform[] trfs = __instance.scrollListKerbals.gameObject.GetComponentsInChildren<Transform>(true);
            foreach (var trf in trfs)
            {
                if (trf.name == "DepartmentSpacer1" || trf.name == "DepartmentSpacer2")
                    Object.DestroyImmediate(trf.gameObject);
            }
        }

        internal static void SetOrUnsetLeader(Strategy leader)
        {
            if (Administration.Instance.SelectedWrapper?.strategy == leader)
                Administration.Instance.UnselectStrategy();
            else
                Administration.Instance.SetSelectedStrategy(new Administration.StrategyWrapper(leader, (UIRadioButton)null));
        }

        /// <summary>
        /// Helper method to find the first active strategy in a given department.
        /// Supports StrategyRP0 strategies with alternate departments.
        /// </summary>
        /// <param name="dep"></param>
        /// <returns></returns>
        internal static Strategy FindActiveStrategyForDepartment(DepartmentConfig dep)
        {
            bool skipFirst = true;
            foreach (var s in StrategySystem.Instance.Strategies)
            {
                if (!s.IsActive)
                    continue;

                if (s.Department == dep)
                    return s;

                if (s is StrategyRP0 sR && sR.ConfigRP0.DepartmentNameAlt == dep.Name)
                {
                    // This check is necessary because all leaders with a primary and secondary
                    // department will be first found for the primary department.
                    // When we search for the secondary department, we'll find a leader
                    // that was already returned for their primary. So we need to skip them
                    // and the *second* active leader is the one to take.
                    if (skipFirst)
                    {
                        skipFirst = false;
                        continue;
                    }

                    return s;
                }
            }

            return null;
        }

        [HarmonyPostfix]
        [HarmonyPatch("CreateStrategiesList")]
        internal static void Postfix_CreateStrategiesList(Administration __instance)
        {
            // Replace Department images if required.
            // (Leaders replace the department image when active)
            for (int i = 0; i < __instance.scrollListKerbals.Count; ++i)
            {
                var dep = StrategySystem.Instance.Departments[i];
                var kerbal = __instance.scrollListKerbals.GetUilistItemAt(i).GetComponent<KerbalListItem>();

                // Hardcoded check for Programs department -- not a leader.
                if (dep.Name == "Programs")
                {
                    kerbal.kerbalImage.texture = GameDatabase.Instance.GetTexture(HighLogic.CurrentGame.flagURL, false);
                    kerbal.kerbalImage.rectTransform.anchorMin = new Vector2(0f, 0.1875f);
                    kerbal.kerbalImage.rectTransform.anchorMax = new Vector2(1f, 0.8125f);

                    continue;
                }

                var leader = FindActiveStrategyForDepartment(dep);
                if (leader == null)
                    continue;

                // We found a leader. Replace the image (and its tooltip).
                string headName = $"<color=#{RUIutils.ColorToHex(dep.Color)}>{leader.Title}\n({dep.Title})</color>";
                kerbal.Initialize(headName, dep.Description, null);
                kerbal.tooltip.textString = dep.Description + "\n\n" + leader.Effect;
                kerbal.kerbalImage.texture = (leader.Config as StrategyConfigRP0).IconDepartmentImage;

                // There will not previously be a button here because all this UI is recreated as part of the stock
                // code for this method. So it's safe to just add, not replace.
                var button = kerbal.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                //button.transition = Selectable.Transition.ColorTint;
                //button.targetGraphic = kerbal.kerbalImage;
                //var cb = new ColorBlock();
                //cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
                //button.colors = cb;
                button.interactable = true;
                button.onClick.AddListener(() => SetOrUnsetLeader(leader));

            }

            // Create some spacers to make everything look nicer.
            __instance.scrollListStrategies.GetUilistItemAt(0).GetComponent<LayoutElement>().minWidth = 280f;
            var firstDep = __instance.scrollListKerbals.GetUilistItemAt(0);
            GameObject spacer = Object.Instantiate(firstDep.gameObject);
            spacer.name = "DepartmentSpacer1";
            for (int i = spacer.transform.childCount - 1; i >= 0; --i)
                Object.DestroyImmediate(spacer.transform.GetChild(i).gameObject);

            Object.DestroyImmediate(spacer.GetComponent<KerbalListItem>());
            Object.DestroyImmediate(spacer.GetComponent<Image>());
            Object.DestroyImmediate(spacer.GetComponent<UIListItem>());

            spacer.GetComponent<LayoutElement>().minWidth = 70f;
            spacer.transform.SetParent(firstDep.transform.parent, false);
            spacer.transform.SetAsFirstSibling();

            firstDep.transform.SetAsFirstSibling();

            GameObject spacer2 = Object.Instantiate(spacer);
            spacer2.name = "DepartmentSpacer2";
            spacer2.transform.SetParent(spacer.transform.parent, false);
            spacer2.transform.SetAsFirstSibling();
        }

        // We'll cache the strategy list to save a tiny bit of GC
        internal static List<Strategy> _strategies = new List<Strategy>();

        [HarmonyPrefix]
        [HarmonyPatch("CreateActiveStratList")]
        internal static bool Prefix_CreateActiveStratList(Administration __instance)
        {
            __instance.scrollListActive.Clear(true);
            Administration.StrategyWrapper wrapper = null;

            // We have to handle multiple tabs here: We support active and completed programs, and active leaders.
            if (AdminExtender.Instance.ActiveTabView == AdministrationActiveTabView.Leaders)
            {
                // Simple case: We just find all active strategies that _aren't_ programs.
                // If they're not programs, they're leaders.
                foreach (var s in StrategySystem.Instance.Strategies)
                    if (s.IsActive && !(s is ProgramStrategy))
                        _strategies.Add(s);
            }
            else
            {
                // Find the right set of programs
                List<Program> programs = AdminExtender.Instance.ActiveTabView == AdministrationActiveTabView.Active ? ProgramHandler.Instance.ActivePrograms : ProgramHandler.Instance.CompletedPrograms;
                foreach (Program p in programs)
                {
                    // Find the matching ProgramStrategy for the program
                    Strategy strategy = StrategySystem.Instance.Strategies.Find(s => s.Config.Name == p.name);
                    if (strategy == null)
                        continue;

                    // Just in case. This should never happen unless you use the debugging UI...
                    // (But in certain load scenarious it might not be? Bleh.)
                    if (AdminExtender.Instance.ActiveTabView == AdministrationActiveTabView.Active && !strategy.IsActive)
                    {
                        RP0Debug.LogError($"ProgramStrategy {p.name} is not active but program is active. Activating...");
                        strategy.Activate();
                    }

                    // This should *also* always be true at this point.
                    if (strategy is ProgramStrategy ps)
                    {
                        if (ps.Program != p)
                            RP0Debug.LogError($"ProgramStrategy binding mismatch for completed program! Strat {strategy.Config.Name} is bound to program that is not the same. Null? {ps.Program == null}");
                    }

                    _strategies.Add(strategy);
                }
            }

            // Now we actually create the items. This broadly equates to stock code.
            foreach (Strategy strategy in _strategies)
            {
                UIListItem item = Object.Instantiate(__instance.prefabActiveStrat);
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
            _strategies.Clear();

            FixProgramCounts(__instance);
            __instance.UpdateStrategyCount();

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("AddStrategiesListItem")]
        internal static void Postfix_AddStrategiesListItem(Administration __instance, UIList itemList)
        {
            // This is called from stock code. We need to postfix it however
            // to fix the text on leaders.
            for (int i = 0; i < itemList.Count; ++i)
            {
                var item = itemList.GetUilistItemAt(i);
                var stratItem = item.GetComponent<StrategyListItem>();
                if (stratItem.toggleButton.Data is Administration.StrategyWrapper sw)
                {
                    if (sw.strategy.Department.Name == "Programs")
                    {
                        stratItem.title = Localizer.Format("#rp0_Admin_ProgramTitle", sw.strategy.Config.Title, (sw.strategy as ProgramStrategy).Program.slots);
                        stratItem.updateTitle(stratItem.title, stratItem.toggleStateChanger.currentState == "ok" ? stratItem.validColor : stratItem.invalidColor);
                        continue;
                    }
                }
                stratItem.transform.FindDeepChild("Text").GetComponent<RectTransform>().anchorMin = new Vector2(0.05f, 0f);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("SetSelectedStrategy")]
        internal static void Prefix_SetSelectedStrategy(Administration __instance, Administration.StrategyWrapper wrapper)
        {
            // This is a bit of a pain. We need to pass some state to the Strategy code
            // so it knows whether to show short info (for the active tab at the bottom) or long info
            // (for the entire strategy-info tab on the right).
            if (wrapper.strategy is StrategyRP0 s)
            {
                s.ShowExtendedInfo = true; // pass through we're about to print the long-form description.
            }

            if (wrapper.strategy is ProgramStrategy ps)
            {
                // Set best speed before we get description
                // This is maybe a duplicate of the work we did in CreateStrategiesList but eh.
                // NOTE: We have to be sure this didn't happen because we pressed a speed button.
                // Because if we did press a speed button, we want that speed, we don't want to clobber.
                if (!AdminExtender.Instance.PressedSpeedButton)
                    ps.Program.SetBestAllowableSpeed();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("SetSelectedStrategy")]
        internal static void Postfix_SetSelectedStrategy(Administration __instance, Administration.StrategyWrapper wrapper)
        {
            var tooltip = __instance.btnAcceptCancel.GetComponent<KSP.UI.TooltipTypes.UIStateButtonTooltip>();

            // If it's a Program, we always want to show the green checkmark
            // not the red x.
            if (wrapper.strategy is ProgramStrategy ps)
            {
                if (ps.Program.IsComplete)
                {
                    __instance.btnAcceptCancel.gameObject.SetActive(false);
                }
                else if (__instance.btnAcceptCancel.currentState == "accept")
                {
                    var stateAccept = tooltip.tooltipStates.First(s => s.name == "accept");

                    var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ProgramActivation, 0d, 0d, 0d, -ps.Program.ConfidenceCost, 0d);
                    string costStr = cmq.GetCostLineOverride(true, true, true, true);
                    if (string.IsNullOrEmpty(costStr))
                        stateAccept.tooltipText = Localizer.Format("#rp0_Admin_AcceptProgram");
                    else
                        stateAccept.tooltipText = Localizer.Format("#rp0_Admin_AcceptProgramWithCost", costStr);
                }
                else
                {
                    // Use the "deactivate" tooltip for the accept button
                    var state = tooltip.tooltipStates.First(s => s.name == "accept");

                    var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ProgramCompletion, 0d, 0d, ps.Program.RepForComplete(Planetarium.GetUniversalTime()), 0d, 0d);
                    string rewardStr = cmq.GetCostLineOverride(false, true, false, true);
                    if (string.IsNullOrEmpty(rewardStr))
                        state.tooltipText = Localizer.Format("#rp0_Admin_CompleteProgram");
                    else
                        state.tooltipText = Localizer.Format("#rp0_Admin_CompleteProgramWithReward", rewardStr);
                    __instance.btnAcceptCancel.SetState("accept");
                }

                AdminExtender.Instance.SetSpeedButtonsActive(!ps.Program.IsActive && !ps.Program.IsComplete ? ps.Program : null);
                AdminExtender.Instance.SetFundingGraphActive(ps.Program);
            }
            else
            {
                tooltip.tooltipStates.First(s => s.name == "accept").tooltipText = Localizer.Format("#rp0_Leaders_Appoint");
                tooltip.tooltipStates.First(s => s.name == "cancel").tooltipText = Localizer.Format("#rp0_Leaders_Remove");
                AdminExtender.Instance.SetSpeedButtonsActive(null);
                AdminExtender.Instance.SetFundingGraphActive(null);
            }
            AdminExtender.Instance.BtnSpacer.gameObject.SetActive(!Administration.Instance.btnAcceptCancel.gameObject.activeSelf);
        }

        [HarmonyPostfix]
        [HarmonyPatch("UnselectStrategy")]
        internal static void Postfix_UnselectStrategy()
        {
            // If we deselect a strategy, hide the custom controls too.
            AdminExtender.Instance.SetSpeedButtonsActive(null);
            AdminExtender.Instance.SetFundingGraphActive(null);
        }

        internal static void OnPopupDismiss()
        {
            // Stock does nothing here. Leaving this in case we need it.
        }

        // Essentially a copy of the stock code, but we need to be able to call it.
        internal static void OnCompleteProgramConfirm()
        {
            OnPopupDismiss();

            if (!Administration.Instance.SelectedWrapper.strategy.Deactivate())
                return;

            Administration.Instance.UnselectStrategy();
            Administration.Instance.RedrawPanels();
        }

        // A modified version of the stock code.
        // We need to do some extra work on programs.
        internal static void OnActivateProgramConfirm()
        {
            OnPopupDismiss();
            if (Administration.Instance.SelectedWrapper.strategy.Activate())
            {
                var newActiveStrat = Administration.Instance.SelectedWrapper.strategy;
                AdminExtender.Instance.SetTabView(AdministrationActiveTabView.Active);
                Administration.Instance.scrollListActive.AddItem(Administration.Instance.CreateActiveStratItem(Administration.Instance.SelectedWrapper.strategy, out var wrapper));
                Administration.Instance.SetSelectedStrategy(wrapper);
                FixProgramCounts(Administration.Instance);
                Administration.Instance.UpdateStrategyCount();
                // Reset program speeds - safe because we early-out if a program is active or complete.
                foreach (var strat in StrategySystem.Instance.Strategies)
                {
                    if (strat is ProgramStrategy ps)
                        ps.Program.SetBestAllowableSpeed();
                }
                // Reset the strategy UI (as stock does)
                Administration.Instance.CreateStrategiesList(StrategySystem.Instance.SystemConfig.Departments);
                Administration.Instance.SelectedWrapper.ButtonInUse.Value = true;

                Program p = ProgramHandler.Instance.ActivePrograms[ProgramHandler.Instance.ActivePrograms.Count - 1];
                if (p.isHSF)
                    GameplayTips.Instance.ShowHSFProgramTip();

                // Special handling if you have accepted your first program and don't realize you can select another.
                if (ProgramHandler.Instance.ActivePrograms.Count < 2 && ProgramHandler.Instance.CompletedPrograms.Count == 0)
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowAcceptAdditionalProgramDialog",
                                         "#rp0_Admin_AcceptAdditional_Program_Title",
                                         "#rp0_Admin_AcceptAdditional_Program_Text",
                                         "#autoLOC_190905",
                                         false,
                                         HighLogic.UISkin).HideGUIsWhilePopup();
                }
            }
            StrategySystem.Instance.StartCoroutine(CallbackUtil.DelayedCallback(2, delegate
            {
                if (!Administration.Instance.SelectedWrapper.strategy.IsActive)
                {
                    Administration.Instance.UnselectStrategy();
                    return;
                }
            }));
        }

        // Helper for the popup
        internal static void OnRemoveLeaderConfirm()
        {
            OnPopupDismiss();
            if (!Administration.Instance.SelectedWrapper.strategy.Deactivate())
                return;

            Administration.Instance.UnselectStrategy();
            Administration.Instance.RedrawPanels();
        }

        [HarmonyPrefix]
        [HarmonyPatch("BtnInputAccept")]
        internal static bool Prefix_BtnInputAccept(Administration __instance, string state)
        {
            // This is what runs when you click the checkmark. We have to handle both programs and leaders.

            if (__instance.SelectedWrapper.strategy is ProgramStrategy ps)
            {
                if (state != "accept" && state != "cancel")
                    return false;

                // If it's active, we're trying to complete it.
                if (ps.IsActive)
                {
                    if (!ps.CanBeDeactivated(out _))
                        return false;

                    // Calculate the reward for display in the popup
                    var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ProgramCompletion, 0d, 0d, ps.Program.RepForComplete(Planetarium.GetUniversalTime()), 0d, 0d);
                    string rewardStr = cmq.GetCostLineOverride(false, true, false, true);
                    if (!string.IsNullOrEmpty(rewardStr))
                        rewardStr = $"\n\n{Localizer.Format("#rp0_Generic_Reward", rewardStr)}";

                    // and then actually spawn the popup.
                    var dlg = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog("StrategyConfirmation",
                            Localizer.Format("#rp0_Admin_CompleteProgram_Confirm", rewardStr),
                            Localizer.GetStringByTag("#autoLOC_464288"),
                            HighLogic.UISkin,
                            new DialogGUIButton(Localizer.GetStringByTag("#autoLOC_439855"), OnCompleteProgramConfirm),
                            new DialogGUIButton(Localizer.GetStringByTag("#autoLOC_439856"), OnPopupDismiss)),
                        persistAcrossScenes: false, HighLogic.UISkin);
                    dlg.OnDismiss = OnPopupDismiss;
                    dlg.HideGUIsWhilePopup();
                }
                else
                {
                    // If it's not active and it's complete, how did we get here?
                    if (ps.Program.IsComplete)
                        return false;

                    // Else we need to activate.

                    // Create the activation cost string
                    var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ProgramActivation, 0d, 0d, 0d, -ps.Program.ConfidenceCost, 0d);
                    string costStr = cmq.GetCostLineOverride(true, true, true, true);
                    if (!string.IsNullOrEmpty(costStr))
                        costStr = $"\n\n{Localizer.Format("#rp0_Generic_Cost", costStr)}";

                    // and spawn the popup
                    var dlg = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog("StrategyConfirmation",
                        Localizer.Format("#rp0_Admin_AcceptProgram_Confirm", costStr),
                        Localizer.Format("#autoLOC_464288"),
                        HighLogic.UISkin,
                        new DialogGUIButton(Localizer.Format("#autoLOC_439839"), OnActivateProgramConfirm),
                        new DialogGUIButton(Localizer.Format("#autoLOC_439840"), OnPopupDismiss)), persistAcrossScenes: false, HighLogic.UISkin);
                    dlg.OnDismiss = OnPopupDismiss;
                    dlg.HideGUIsWhilePopup();
                }

                return false;
            }

            if (state == "cancel")
            {
                // Leaders have a deactivate cost and a cooldown. Compute these.
                var leader = __instance.SelectedWrapper.strategy as StrategyRP0;
                var cfg = leader.Config as StrategyConfigRP0;
                string deactivateCostStr = leader.DeactivateCostString();
                string reappointStr = cfg.RemoveOnDeactivate 
                    ? cfg.ReactivateCooldown > 0
                        ? $"\n\n{Localizer.Format("#rp0_Leaders_Deactivates_WithCooldown", KSPUtil.PrintDateDelta(cfg.ReactivateCooldown, false))}"
                        : $"\n\n{Localizer.GetStringByTag("#rp0_Leaders_Deactivates")}"
                    : string.Empty;
                string message = !string.IsNullOrEmpty(deactivateCostStr)
                    ? Localizer.Format("#rp0_Leaders_Remove_ConfirmWithCost", 
                        deactivateCostStr,
                        reappointStr)
                    : Localizer.Format("#rp0_Leaders_Remove_Confirm", reappointStr);

                var dlg = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new MultiOptionDialog("StrategyConfirmation",
                    message,
                    Localizer.Format("#autoLOC_464288"),
                    HighLogic.UISkin,
                    new DialogGUIButton(Localizer.Format("#autoLOC_439839"), OnRemoveLeaderConfirm),
                    new DialogGUIButton(Localizer.Format("#autoLOC_439840"), OnPopupDismiss)), persistAcrossScenes: false, HighLogic.UISkin);
                dlg.OnDismiss = OnPopupDismiss;
                dlg.HideGUIsWhilePopup();

                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnAcceptConfirm")]
        internal static void Prefix_OnAcceptConfirm(Administration __instance)
        {
            // FIXME do we want non-Leader, non-Program strategies?
            if (!(__instance.SelectedWrapper.strategy is ProgramStrategy))
            {
                AdminExtender.Instance.SetTabView(AdministrationActiveTabView.Leaders);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateStrategyCount")]
        internal static bool Prefix_UpdateStrategyCount(Administration __instance)
        {
            __instance.activeStratCount.text = Localizer.Format("#autoLOC_439627", ProgramHandler.Instance.ActiveProgramSlots, ProgramHandler.Instance.MaxProgramSlots);
            return false;
        }
    }
}