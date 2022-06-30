﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class VesselBuildValidator
    {
        private enum ValidationResult { Undecided, Fail, Success, Rerun };

        private const string InputLockID = "KCTVesselBuildValidator";

        public bool CheckFacilityRequirements { get; set; } = true;
        /// <summary>
        /// If true, will only give a warning message to the user and not fail the validation.
        /// </summary>
        public bool BypassFacilityRequirements { get; set; } = true;
        public bool CheckPartAvailability { get; set; } = true;
        public bool CheckPartConfigs { get; set; } = true;
        public bool CheckAvailableFunds { get; set; } = true;
        public Action<BuildListVessel> SuccessAction { get; set; }
        public Action FailureAction { get; set; }

        private static IEnumerator _routine;
        private ValidationResult _validationResult;

        private Action<BuildListVessel> _successActions;
        private Action _failureActions;

        public void ProcessVessel(BuildListVessel blv)
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
                KerbalConstructionTime.Instance.StopCoroutine(_routine);

            InputLockManager.SetControlLock(ControlTypes.EDITOR_UI, InputLockID);
            _routine = RunValidationRoutine(blv);
            KerbalConstructionTime.Instance.StartCoroutine(_routine);
        }

        private IEnumerator RunValidationRoutine(BuildListVessel blv)
        {
            if (ProcessFacilityChecks(blv) != ValidationResult.Success)
            {
                _failureActions();
                yield break;
            }
            if (!Utilities.CurrentGameIsCareer())
            {
                _successActions(blv);
                yield break;
            }

            ProcessPartAvailability(blv);
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
                ProcessPartConfigs(blv);
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

            if (ProcessFundsChecks(blv) != ValidationResult.Success)
            {
                _failureActions();
                yield break;
            }

            _successActions(blv);
        }

        private ValidationResult ProcessFacilityChecks(BuildListVessel blv)
        {
            if (CheckFacilityRequirements)
            {
                //Check if vessel fails facility checks but can still be built
                List<string> facilityChecks = blv.MeetsFacilityRequirements(true);
                if (facilityChecks.Count != 0)
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "editorChecksFailedPopup",
                        "Failed editor checks!",
                        "Warning! This vessel did not pass the editor checks! It will still be built, but you will not be able to launch it without upgrading. Listed below are the failed checks:\n"
                        + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()),
                        "Acknowledged",
                        false,
                        HighLogic.UISkin);

                    if (!BypassFacilityRequirements)
                    {
                        _failureActions();
                        return ValidationResult.Fail;
                    }
                }
            }

            return ValidationResult.Success;
        }

        private void ProcessPartAvailability(BuildListVessel blv)
        {
            _validationResult = ValidationResult.Undecided;
            if (!CheckPartAvailability)
            {
                _validationResult = ValidationResult.Success;
                return;
            }

            // Check if vessel contains locked parts, and therefore cannot be built
            Dictionary<AvailablePart, PartPurchasability> partStatuses = blv.GetPartsWithPurchasability();
            IEnumerable<KeyValuePair<AvailablePart, PartPurchasability>> lockedParts = partStatuses.Where(kvp => kvp.Value.Status == PurchasabilityStatus.Unavailable);
            if (lockedParts.Any())
            {
                KCTDebug.Log($"Tried to add {blv.ShipName} to build list but it contains locked parts.");

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
            int unlockCost = Utilities.FindUnlockCost(partList);
            int partCount = purchasableParts.Count();
            string mode = KCTGameStates.EditorShipEditingMode ? "save edits" : "build vessel";
            var buttons = new DialogGUIButton[] {
                new DialogGUIButton("Acknowledged", () => { _validationResult = ValidationResult.Fail; }),
                new DialogGUIButton($"Unlock {partCount} part{(partCount > 1? "s":"")} for {unlockCost} Fund{(unlockCost > 1? "s":"")} and {mode}", () =>
                {
                    if (Funding.Instance.Funds > unlockCost)
                    {
                        Utilities.UnlockExperimentalParts(partList);
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
                HighLogic.UISkin);
        }

        private void ProcessPartConfigs(BuildListVessel blv)
        {
            _validationResult = ValidationResult.Undecided;
            if (!CheckPartConfigs)
            {
                _validationResult = ValidationResult.Success;
                return;
            }

            Dictionary<Part, List<PartConfigValidationError>> dict = GetConfigErrorsDict(blv);
            if (dict.Count == 0)
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
                HighLogic.UISkin);
        }

        private ValidationResult ProcessFundsChecks(BuildListVessel blv)
        {
            if (CheckAvailableFunds)
            {
                double totalCost = blv.GetTotalCost();
                double prevFunds = Funding.Instance.Funds;
                if (totalCost > prevFunds)
                {
                    KCTDebug.Log($"Tried to add {blv.ShipName} to build list but not enough funds.");
                    KCTDebug.Log($"Vessel cost: {Utilities.GetTotalVesselCost(blv.ShipNode)}, Current funds: {prevFunds}");
                    var msg = new ScreenMessage("Not Enough Funds To Build!", 4f, ScreenMessageStyle.UPPER_CENTER);
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

        private Dictionary<Part, List<PartConfigValidationError>> GetConfigErrorsDict(BuildListVessel blv)
        {
            var dict = new Dictionary<Part, List<PartConfigValidationError>>();

            ShipConstruct sc = blv.GetShip();
            foreach (Part part in sc.parts)
            {
                foreach (PartModule pm in part.Modules)
                {
                    var types = new[] { typeof(string).MakeByRefType(), typeof(bool).MakeByRefType(), typeof(float).MakeByRefType() };
                    var mi = pm.GetType().GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public, null, types, null);
                    if (mi != null)
                    {
                        var parameters = new object[] { null, null, null };
                        bool allSucceeded;
                        try
                        {
                            allSucceeded = (bool)mi.Invoke(pm, parameters);
                        }
                        catch (Exception ex)
                        {
                            KCTDebug.LogError($"Config validation failed for {part.name}");
                            Debug.LogException(ex);
                            allSucceeded = false;
                            parameters[0] = "error occurred, check the logs";
                            parameters[1] = false;
                            parameters[2] = 0f;
                        }

                        if (!allSucceeded)
                        {
                            var validationError = new PartConfigValidationError
                            {
                                PM = pm,
                                Error = (string)parameters[0],
                                CanBeResolved = (bool)parameters[1],
                                CostToResolve = (float)parameters[2]
                            };

                            // Try to autoresolve issues that cost next to nothing
                            if (validationError.CanBeResolved && validationError.CostToResolve <= 1.1 &&
                                PurchaseConfig(pm))
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
                        list.Add(new DialogGUIHorizontalLayout(TextAnchor.MiddleLeft,
                                     new DialogGUILabel("<color=green><size=20>•</size></color>", 7),
                                     new DialogGUILabel(txt, expandW: true),
                                     new DialogGUIButton($"Unlock ({error.CostToResolve:N0})",
                                                         () => {
                                                             PurchaseConfig(error.PM);
                                                             _validationResult = ValidationResult.Rerun;
                                                         },
                                                         () => Funding.CanAfford(error.CostToResolve),
                                                         100, -1, true)));
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

        private bool PurchaseConfig(PartModule pm)
        {
            var mi = pm.GetType().GetMethod("ResolveValidationError", BindingFlags.Instance | BindingFlags.Public);
            object retVal = mi?.Invoke(pm, new object[] { });

            return (retVal is bool b) && b;
        }

        private class PartConfigValidationError
        {
            public PartModule PM { get; set; }
            public string Error { get; set; }
            public bool CanBeResolved { get; set; }
            public float CostToResolve { get; set; }
        }
    }
}
