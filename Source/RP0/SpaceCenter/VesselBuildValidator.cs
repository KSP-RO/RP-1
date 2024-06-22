using System;
using System.Collections;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using UnityEngine;
using RP0.UI;
using ROUtils;

namespace RP0
{
    public class VesselBuildValidator
    {
        private enum ValidationResult { Undecided, Fail, Success, Rerun };

        private const string InputLockID = "KCTVesselBuildValidator";

        public bool CheckFacilityRequirements { get; set; } = true;
        /// <summary>
        /// If true, will only give a warning message to the user and not fail the validation.
        /// </summary>
        public bool BypassFacilityRequirements { get; set; } = false;
        public bool CheckPartAvailability { get; set; } = true;
        public bool CheckPartConfigs { get; set; } = true;
        public bool CheckAvailableFunds { get; set; } = true;
        public bool CheckUntooledParts { get; set; } = true;
        public double? CostOffset { get; set; } = null;
        public Action<VesselProject> SuccessAction { get; set; }
        public Action FailureAction { get; set; }

        private static IEnumerator _routine;
        private ValidationResult _validationResult;

        private Action<VesselProject> _successActions;
        private Action _failureActions;

        public void ProcessVessel(VesselProject vp)
        {
            _successActions = SuccessAction + ((_) =>
            {
                InputLockManager.RemoveControlLock(InputLockID);
            });
            _failureActions = FailureAction + (() =>
            {
                InputLockManager.RemoveControlLock(InputLockID);
            });

            if (_routine != null)
                SpaceCenterManagement.Instance.StopCoroutine(_routine);

            InputLockManager.SetControlLock(ControlTypes.EDITOR_UI, InputLockID);
            _routine = RunValidationRoutine(vp);
            SpaceCenterManagement.Instance.StartCoroutine(_routine);
        }

        private IEnumerator RunValidationRoutine(VesselProject vp)
        {
            if (ProcessFacilityChecks(vp) != ValidationResult.Success)
            {
                _failureActions();
                yield break;
            }
            if (!KSPUtils.CurrentGameIsCareer())
            {
                _successActions(vp);
                yield break;
            }

            ProcessPartAvailability(vp);
            while (_validationResult == ValidationResult.Undecided)
                yield return null;

            _routine = null;
            if (_validationResult != ValidationResult.Success)
            {
                _failureActions();
                yield break;
            }

            do
            {
                ProcessPartConfigs(vp);
                while (_validationResult == ValidationResult.Undecided)
                    yield return null;
            }
            while (_validationResult == ValidationResult.Rerun);

            _routine = null;
            if (_validationResult != ValidationResult.Success)
            {
                _failureActions();
                yield break;
            }

            if (ProcessFundsChecks(vp) != ValidationResult.Success)
            {
                _failureActions();
                yield break;
            }

            ProcessUntooledParts(vp);
            while (_validationResult == ValidationResult.Undecided)
                yield return null;

            ProcessExcessEC(vp);
            while (_validationResult == ValidationResult.Undecided)
                yield return null;

            _routine = null;
            if (_validationResult != ValidationResult.Success)
            {
                _failureActions();
                yield break;
            }

            _successActions(vp);
        }

        private ValidationResult ProcessFacilityChecks(VesselProject vp)
        {
            if (CheckFacilityRequirements)
            {
                //Check if vessel fails facility checks but can still be built
                List<string> facilityChecks = new List<string>();
                if (!vp.MeetsFacilityRequirements(facilityChecks))
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "editorChecksFailedPopup",
                        "Failed editor checks!",
                        "Warning! This vessel did not pass the editor checks! " 
                            + (BypassFacilityRequirements ? "It will still be added to plans, but you cannot integrate it without rectifying these issues." : string.Empty)
                            + "\nListed below are the failed checks:\n"
                        + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()),
                        "Acknowledged",
                        false,
                        HighLogic.UISkin).HideGUIsWhilePopup();

                    if (!BypassFacilityRequirements)
                    {
                        _failureActions();
                        return ValidationResult.Fail;
                    }
                }
            }

            return ValidationResult.Success;
        }

        private void ProcessPartAvailability(VesselProject vp)
        {
            _validationResult = ValidationResult.Undecided;
            if (!CheckPartAvailability)
            {
                _validationResult = ValidationResult.Success;
                return;
            }

            // Check if vessel contains locked parts, and therefore cannot be built
            Dictionary<AvailablePart, PartPurchasability> partStatuses = vp.GetPartsWithPurchasability();
            IEnumerable<KeyValuePair<AvailablePart, PartPurchasability>> lockedParts = partStatuses.Where(kvp => kvp.Value.Status == PurchasabilityStatus.Unavailable);
            if (lockedParts.Any())
            {
                RP0Debug.Log($"Tried to add {vp.shipName} to build list but it contains locked parts.");

                // Simple ScreenMessage since there's not much you can do other than removing the locked parts manually.
                string lockedMsg = ConstructLockedPartsWarning(lockedParts);
                var msg = new ScreenMessage(lockedMsg, 4f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(msg);

                _validationResult = ValidationResult.Fail;
                return;
            }

            IEnumerable<KeyValuePair<AvailablePart, PartPurchasability>> purchasableParts = partStatuses.Where(kvp => kvp.Value.Status == PurchasabilityStatus.Purchasable);
            if (!purchasableParts.Any())
            {
                _validationResult = ValidationResult.Success;
                return;
            }

            string devPartsMsg = ConstructUnlockablePartsWarning(purchasableParts);
            List<AvailablePart> partList = purchasableParts.Select(kvp => kvp.Key).ToList();
            
            // PopupDialog asking you if you want to pay the entry cost for all the parts that can be unlocked (tech node researched)
            
            double unlockCost = ECMHelper.FindUnlockCost(partList);
            var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.PartOrUpgradeUnlock, -unlockCost, 0d, 0d);
            double postCMQUnlockCost = -cmq.GetTotal(CurrencyRP0.Funds, false);

            double credit = UnlockCreditHandler.Instance.GetCreditAmount(partList);

            double spentCredit = Math.Min(postCMQUnlockCost, credit);
            cmq.AddPostDelta(CurrencyRP0.Funds, spentCredit, true);

            int partCount = partList.Count;
            string mode = SpaceCenterManagement.EditorShipEditingMode ? "save edits" : "integrate vessel";
            var buttons = new DialogGUIButton[] {
                new DialogGUIButton("Acknowledged", () => { _validationResult = ValidationResult.Fail; }),
                new DialogGUIButton($"Unlock {partCount} part{(partCount > 1? "s":"")} for <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>{Math.Max(0d, -cmq.GetTotal(CurrencyRP0.Funds, true)):N0} and {mode} (spending <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>{spentCredit:N0} unlock credit)", () =>
                {
                    if (cmq.CanAfford())
                    {
                        KCTUtilities.UnlockExperimentalParts(partList);
                        _validationResult = ValidationResult.Success;
                    }
                    else
                    {
                        var msg = new ScreenMessage("Insufficient funds to unlock parts", 5f, ScreenMessageStyle.UPPER_CENTER);
                        ScreenMessages.PostScreenMessage(msg);
                        _validationResult = ValidationResult.Fail;
                    }
                })
            };

            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new MultiOptionDialog("devPartsCheckFailedPopup",
                    devPartsMsg,
                    "Vessel cannot be built!",
                    HighLogic.UISkin,
                    buttons),
                false,
                HighLogic.UISkin).HideGUIsWhilePopup();
        }

        private void ProcessPartConfigs(VesselProject vp)
        {
            _validationResult = ValidationResult.Undecided;
            if (!CheckPartConfigs)
            {
                _validationResult = ValidationResult.Success;
                return;
            }

            Dictionary<Part, List<PartConfigValidationError>> dict = GetConfigErrorsDict(vp);
            if (dict == null || dict.Count == 0)
            {
                _validationResult = ValidationResult.Success;
                return;
            }

            DialogGUIBase[] controls = ConstructPartConfigErrorsUI(dict);
            var dlgRect = new Rect(0.5f, 0.5f, 400, 100);

            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new MultiOptionDialog("partConfigValidationFailedPopup",
                    null,
                    "Vessel cannot be built!",
                    HighLogic.UISkin,
                    dlgRect,
                    controls),
                false,
                HighLogic.UISkin).HideGUIsWhilePopup();
        }

        private void ProcessUntooledParts(VesselProject vp)
        {
            _validationResult = ValidationResult.Success;
            if (!CheckUntooledParts || !HighLogic.LoadedSceneIsEditor ||
                !HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().ShowToolingReminders)
            {
                return;
            }

            bool hasUntooledParts = EditorLogic.fetch.ship.Parts.Any(p => p.FindModuleImplementing<ModuleTooling>()?.IsUnlocked() == false);
            if (hasUntooledParts)
            {
                _validationResult = ValidationResult.Undecided;

                var dlgRect = new Rect(0.5f, 0.5f, 400, 100);
                var buttons = new DialogGUIButton[] {
                    new DialogGUIButton("Cancel integration", () => { _validationResult = ValidationResult.Fail; }),
                    new DialogGUIButton("Integrate anyway", () => { _validationResult = ValidationResult.Success; })
                };

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new MultiOptionDialog("ontooledPartsWarningPopup",
                        "Tool them in the RP-1 menu to reduce vessel cost and integration time.",
                        "Untooled parts",
                        HighLogic.UISkin,
                        dlgRect,
                        buttons),
                    false,
                    HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }

        private void ProcessExcessEC(VesselProject vp)
        {
            _validationResult = ValidationResult.Success;
            if (!HighLogic.LoadedSceneIsEditor)
            {
                return;
            }
            
            if (GameplayTips.Instance.ShipHasExcessEC(EditorLogic.fetch.ship))
            {
                _validationResult = ValidationResult.Undecided;

                var dlgRect = new Rect(0.5f, 0.5f, 400, 100);
                var buttons = new DialogGUIButton[] {
                    new DialogGUIButton("Cancel integration", () => { _validationResult = ValidationResult.Fail; }),
                    new DialogGUIButton("Integrate anyway", () => { _validationResult = ValidationResult.Success; })
                };

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new MultiOptionDialog("excessECWarningPopup",
                        KSP.Localization.Localizer.GetStringByTag("#rp0_GameplayTip_ExcessEC_Text"),
                        KSP.Localization.Localizer.GetStringByTag("#rp0_GameplayTip_ExcessEC_Title"),
                        HighLogic.UISkin,
                        dlgRect,
                        buttons),
                    false,
                    HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }

        private ValidationResult ProcessFundsChecks(VesselProject vp)
        {
            if (CheckAvailableFunds)
            {
                double totalCost = vp.GetTotalCost();
                if (CostOffset != null)
                    totalCost -= CostOffset.Value;
                var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.VesselPurchase, -totalCost, 0d, 0d);
                if (!cmq.CanAfford())
                {
                    RP0Debug.Log($"Tried to add {vp.shipName} to integration list but not enough funds.");
                    RP0Debug.Log($"Vessel cost: {cmq.GetTotal(CurrencyRP0.Funds, true)}, Current funds: {Funding.Instance.Funds}");
                    var msg = new ScreenMessage("Not Enough Funds To Integrate!", 4f, ScreenMessageStyle.UPPER_CENTER);
                    ScreenMessages.PostScreenMessage(msg);

                    return ValidationResult.Fail;
                }
            }

            return ValidationResult.Success;
        }

        private static string ConstructLockedPartsWarning(IEnumerable<KeyValuePair<AvailablePart, PartPurchasability>> lockedPartsOnShip)
        {
            var sb = StringBuilderCache.Acquire();
            sb.Append("Warning! This vessel cannot be built. It contains parts which are not available at the moment:\n");

            foreach (KeyValuePair<AvailablePart, PartPurchasability> kvp in lockedPartsOnShip)
            {
                sb.Append($" <color=orange><b>{kvp.Value.PartCount}x {kvp.Key.title}</b></color>\n");
            }

            return sb.ToStringAndRelease();
        }

        private static string ConstructUnlockablePartsWarning(IEnumerable<KeyValuePair<AvailablePart, PartPurchasability>> unlockablePartsOnShip)
        {
            var sb = StringBuilderCache.Acquire();
            sb.Append("This vessel contains parts that are still in development. ");
            if (unlockablePartsOnShip.Any(kvp => ResearchAndDevelopment.GetTechnologyState(kvp.Key.TechRequired) == RDTech.State.Available))
                sb.Append("Green parts have been researched and can be unlocked.\n");
            else
                sb.Append("\n");

            foreach (KeyValuePair<AvailablePart, PartPurchasability> kvp in unlockablePartsOnShip)
            {
                if (ResearchAndDevelopment.GetTechnologyState(kvp.Key.TechRequired) == RDTech.State.Available)
                    sb.Append($" <color=green><b>{kvp.Value.PartCount}x {kvp.Key.title}</b></color>\n");
                else
                    sb.Append($" <color=orange><b>{kvp.Value.PartCount}x {kvp.Key.title}</b></color>\n");
            }

            return sb.ToStringAndRelease();
        }

        private Dictionary<Part, List<PartConfigValidationError>> GetConfigErrorsDict(VesselProject vp)
        {
            ShipConstruct sc = vp.GetShip();
            if (sc == null)
                return null;

            var dict = new Dictionary<Part, List<PartConfigValidationError>>();
            foreach (Part part in sc.parts)
            {
                foreach (PartModule pm in part.Modules)
                {
                    var types = new[] { typeof(string).MakeByRefType(), typeof(bool).MakeByRefType(), typeof(float).MakeByRefType(), typeof(string).MakeByRefType() };
                    var mi = pm.GetType().GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public, null, types, null);
                    if (mi != null)
                    {
                        var parameters = new object[] { null, null, null, null };
                        bool allSucceeded;
                        try
                        {
                            allSucceeded = (bool)mi.Invoke(pm, parameters);
                        }
                        catch (Exception ex)
                        {
                            RP0Debug.LogError($"Config validation failed for {part.name}");
                            Debug.LogException(ex);
                            allSucceeded = false;
                            parameters[0] = "error occurred, check the logs";
                            parameters[1] = false;
                            parameters[2] = 0f;
                            parameters[3] = string.Empty;
                        }

                        if (!allSucceeded)
                        {
                            var validationError = new PartConfigValidationError
                            {
                                PM = pm,
                                Error = (string)parameters[0],
                                CanBeResolved = (bool)parameters[1],
                                CostToResolve = (float)parameters[2],
                                TechToResolve = (string)parameters[3]
                            };

                            // Try to autoresolve issues that cost next to nothing
                            if (validationError.CanBeResolved && validationError.CostToResolve <= 1.1 &&
                                PurchaseConfig(pm, validationError.TechToResolve))
                            {
                                continue;
                            }

                            if (!dict.TryGetValue(part, out List<PartConfigValidationError> list))
                            {
                                list = new List<PartConfigValidationError>(2);
                                dict[part] = list;
                            }

                            list.Add(validationError);
                        }
                    }
                }
            }

            return dict;
        }

        private DialogGUIBase[] ConstructPartConfigErrorsUI(Dictionary<Part, List<PartConfigValidationError>> errorDict)
        {
            var list = new List<DialogGUIBase>(10);

            list.Add(new DialogGUILabel("This vessel contains prototype configs that haven't been unlocked yet."));
            if (errorDict.Any(kvp => kvp.Value.Any(v => v.CanBeResolved)))
            {
                list.Add(new DialogGUILabel("Issues in <color=green>green</color> can be resolved."));
            }

            foreach (var kvp in errorDict)
            {
                Part p = kvp.Key;
                foreach (PartConfigValidationError error in kvp.Value)
                {
                    if (error.CanBeResolved)
                    {
                        string txt = $"<color=green><b>{p.partInfo.title}: {error.Error}</b></color>\n";
                        var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.PartOrUpgradeUnlock, -error.CostToResolve, 0f, 0f);
                        string costStr = cmq.GetCostLineOverride(true, false, false, true);
                        double trueTotal = -cmq.GetTotal(CurrencyRP0.Funds, false);
                        double invertCMQOp = error.CostToResolve / trueTotal;
                        double creditAmtToUse = Math.Min(trueTotal, UnlockCreditHandler.Instance.GetCreditAmount(error.TechToResolve));
                        cmq.AddPostDelta(CurrencyRP0.Funds, creditAmtToUse, true);
                        string afterCreditLine = cmq.GetCostLineOverride(true, false, true, true, true);
                        if (string.IsNullOrEmpty(afterCreditLine))
                            afterCreditLine = "free";
                        var button = new DialogGUIButtonWithTooltip($"Unlock ({afterCreditLine})",
                                                         () =>
                                                         {
                                                             PurchaseConfig(error.PM, error.TechToResolve);
                                                             _validationResult = ValidationResult.Rerun;
                                                         },
                                                         () => cmq.CanAfford(),
                                                         100, -1, true)
                                                            { tooltipText = $"Spending {creditAmtToUse:N0} unlock credit\n(Base cost {costStr})" };
                        list.Add(new DialogGUIHorizontalLayout(TextAnchor.MiddleLeft,
                                     new DialogGUILabel("<color=green><size=20>•</size></color>", 7),
                                     new DialogGUILabel(txt, expandW: true),
                                     button));
                    }
                    else
                    {
                        string txt = $" <color=orange><b>{p.partInfo.title}: {error.Error}</b></color>\n";
                        list.Add(new DialogGUIHorizontalLayout(TextAnchor.MiddleLeft,
                                     new DialogGUILabel("<color=orange><size=20>•</size></color>", 7),
                                     new DialogGUILabel(txt, expandW: true),
                                     new DialogGUILabel(string.Empty, 100)));
                    }
                }
            }

            list.Add(new DialogGUIFlexibleSpace());
            list.Add(new DialogGUIHorizontalLayout(sw: true, sh: false, new DialogGUIButton("Acknowledged", () => { _validationResult = ValidationResult.Fail; })));

            return list.ToArray();
        }

        private bool PurchaseConfig(PartModule pm, string tech)
        {
            Harmony.RFECMPatcher.techNode = tech;
            var mi = pm.GetType().GetMethod("ResolveValidationError", BindingFlags.Instance | BindingFlags.Public);
            object retVal = mi?.Invoke(pm, new object[] { });
            Harmony.RFECMPatcher.techNode = null;

            bool ret = (retVal is bool b) && b;
            if (ret)
            {
                if (HighLogic.LoadedSceneIsEditor)
                    SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
            }
            return ret;
        }

        private class PartConfigValidationError
        {
            public PartModule PM { get; set; }
            public string Error { get; set; }
            public bool CanBeResolved { get; set; }
            public float CostToResolve { get; set; }
            public string TechToResolve { get; set; }
        }
    }
}
