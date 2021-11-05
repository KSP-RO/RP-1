using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class VesselBuildValidator
    {
        private enum ValidationResult { Undecided, Fail, Success };

        public bool CheckFacilityRequirements { get; set; } = true;
        /// <summary>
        /// If true, will only give a warning message to the user and not fail the validation.
        /// </summary>
        public bool BypassFacilityRequirements { get; set; } = true;
        public bool CheckPartAvailability { get; set; } = true;
        public bool CheckAvailableFunds { get; set; } = true;
        public Action<BuildListVessel> SuccessAction { get; set; }
        public Action FailureAction { get; set; }

        private static IEnumerator _routine;
        private ValidationResult _validationResult;

        public void ProcessVessel(BuildListVessel blv)
        {
            SuccessAction = SuccessAction ?? ((_) => { });
            FailureAction = FailureAction ?? (() => { });

            if (!Utilities.CurrentGameIsCareer())
            {
                SuccessAction(blv);
                return;
            }

            if (_routine != null)
                KerbalConstructionTime.Instance.StopCoroutine(_routine);

            _routine = RunValidationRoutine(blv);
            KerbalConstructionTime.Instance.StartCoroutine(_routine);
        }

        private IEnumerator RunValidationRoutine(BuildListVessel blv)
        {
            if (ProcessFacilityChecks(blv) != ValidationResult.Success)
            {
                FailureAction();
                yield break;
            }

            ProcessPartAvailability(blv);
            while (_validationResult == ValidationResult.Undecided)
                yield return null;

            _routine = null;
            if (_validationResult != ValidationResult.Success)
            {
                FailureAction();
                yield break;
            }

            if (ProcessFundsChecks(blv) != ValidationResult.Success)
            {
                FailureAction();
                yield break;
            }

            SuccessAction(blv);
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
                        FailureAction();
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
    }
}
