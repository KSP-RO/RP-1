using RP0;
using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        public static Rect EditorWindowPosition = new Rect(Screen.width / 3.5f, Screen.height / 3.5f, 275, 135);
        public static string BuildRateForDisplay;

        private static bool _isEditorLocked = false;
        private static bool _wasShowBuildList = false;
        private static readonly GUIContent _gcMaxBuildRate = new GUIContent("M", "Display integration rate for max engineers");
        private static readonly GUIContent _gcCurBuildRate = new GUIContent("C", "Display integration rate for current engineers");
        private static readonly GUIContent _gcSwitchToLC = new GUIContent("Switch to LC", "The Hangar is currently selected; this will switch to a launch complex, needed for rockets.");
        private static readonly GUIContent _gcNoLCAvailable = new GUIContent("No LC Available", "Build a new one.");
        private static readonly GUIContent _gcNoLCAvailableSomeConstructing = new GUIContent("No LC Available Now", "There is no operational launch complex. Build a new one or wait for an existing one to finish construction.");
        private static readonly GUIContent _gcNewLC = new GUIContent("New LC", "Build a new launch complex to support this vessel, with a margin of 10% to vessel mass and size upgrades.");
        private static readonly GUIContent _gcNoHangar = new GUIContent("Hangar Unavailable", "The Hangar is currently being modified.");
        private static readonly GUIContent _gcSwitchToHangar = new GUIContent("Switch to Hangar", "A Launch Complex is currently selected; this will switch to the Hangar, needed for planes.");

        public static void DrawEditorGUI(int windowID)
        {
            if (EditorLogic.fetch == null)
            {
                return;
            }
            if (EditorWindowPosition.width < 275)    // the size keeps getting changed for some reason, so this will avoid that
            {
                EditorWindowPosition.width = 275;
                EditorWindowPosition.height = 1;
            }
            GUILayout.BeginVertical();
            if (!KCTGameStates.EditorShipEditingMode)
            {
                RenderBuildMode();
            }
            else
            {
                RenderEditMode();
            }

            GUILayout.EndVertical();
            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();

            CheckEditorLock();
            ClampWindow(ref EditorWindowPosition, strict: false);
        }

        private static void RenderBuildMode()
        {
            double buildPoints = KerbalConstructionTime.Instance.EditorVessel.buildPoints + KerbalConstructionTime.Instance.EditorVessel.integrationPoints;
            double bpLeaderEffect = KerbalConstructionTime.Instance.EditorVessel.LeaderEffect;
            double effic = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.Efficiency;
            double rateWithCurEngis = Utilities.GetBuildRate(KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance, KerbalConstructionTime.Instance.EditorVessel.mass, KerbalConstructionTime.Instance.EditorVessel.buildPoints, KerbalConstructionTime.Instance.EditorVessel.humanRated, 0)
                * effic
                * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.StrategyRateMultiplier;

            RenderBuildRateInputRow(buildPoints, rateWithCurEngis);

            if (KSP.UI.Screens.DebugToolbar.DebugScreenSpawner.Instance?.screen?.isShown ?? false)
                GUILayout.Label($"BP Cost: {buildPoints:N0}");

            if (double.TryParse(BuildRateForDisplay, out double bR))
            {
                double buildTime = bR > 0d
                    ? (effic >= LCEfficiency.MaxEfficiency
                        ? buildPoints / (bR * bpLeaderEffect)
                        : KerbalConstructionTime.Instance.EditorVessel.CalculateTimeLeftForBuildRate(buildPoints, bR / effic, effic, out _))
                    : 0d;
                GUILayout.Label($"Integration Time: {(bR > 0 ? KSPUtil.PrintDateDeltaCompact(buildTime, true, false) : "infinity")}");

                if (KCTGameStates.EditorRolloutBP > 0)
                {
                    GUILayout.Label($"Rollout Time: {(bR > 0 ? KSPUtil.PrintDateDeltaCompact(KCTGameStates.EditorRolloutBP / bR, true, false) : "infinity")}");
                }
            }
            else
            {
                GUILayout.Label("Invalid Integration Rate");
            }

            if (EditorDriver.editorFacility == EditorFacility.SPH || (KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LCType == LaunchComplexType.Pad && KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.IsHumanRated && !KerbalConstructionTime.Instance.EditorVessel.humanRated))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Engineer Cap:");
                GUILayout.Label((EditorDriver.editorFacility == EditorFacility.SPH ? KCTGameStates.ActiveKSC.Hangar : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance).MaxEngineersFor(KerbalConstructionTime.Instance.EditorVessel).ToString(), GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();
            }

            if (KerbalConstructionTime.Instance.EditorVessel.integrationCost > 0)
                GUILayout.Label($"Integration Cost: √{KerbalConstructionTime.Instance.EditorVessel.integrationCost:N1}");

            if (bR > 0d && rateWithCurEngis > 0d)
            {
                double effectiveEngCount = bR / rateWithCurEngis * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.Engineers;
                double salaryPerDayAboveIdle = RP0.MaintenanceHandler.Settings.salaryEngineers * (1d / 365.25d) * (1d - PresetManager.Instance.ActivePreset.GeneralSettings.IdleSalaryMult);
                double cost = buildPoints / bR / 86400d * effectiveEngCount * salaryPerDayAboveIdle;
                GUILayout.Label(new GUIContent($"Net Salary: √{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.SalaryEngineers, -cost):N1}", "The extra salary paid above the idle rate for these engineers"));
            }

            if (KCTGameStates.EditorRolloutCost > 0)
                GUILayout.Label($"Rollout Cost: √{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.RocketRollout, -KCTGameStates.EditorRolloutCost):N1}");

            bool showCredit = false;
            if (KCTGameStates.EditorUnlockCosts > 0)
            {
                showCredit = true;
                GUILayout.Label($"Unlock Cost: √{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.PartOrUpgradeUnlock, -KCTGameStates.EditorUnlockCosts):N1}");
            }

            if (KCTGameStates.EditorToolingCosts > 0)
            {
                showCredit = true;
                GUILayout.Label($"Tooling Cost: √{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.ToolingPurchase, -KCTGameStates.EditorToolingCosts):N1}");
            }

            if (showCredit)
                GUILayout.Label($"Unlock Credit: √{RP0.UnlockCreditHandler.Instance.TotalCredit:N1}");

            if (KCTGameStates.EditorRequiredTechs.Count > 0)
            {
                string techLabel = string.Empty;
                foreach (string techId in KCTGameStates.EditorRequiredTechs)
                {
                    string techName = ResearchAndDevelopment.GetTechnologyTitle(techId);

                    if (string.IsNullOrEmpty(techLabel))
                        techLabel = $"Needs: {techName}";
                    else
                        techLabel += $"\n       {techName}";
                }
                GUILayout.Label(techLabel);
            }

            if (KerbalConstructionTime.Instance.EditorVessel.humanRated && !KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.IsHumanRated)
            {
                GUILayout.Label("WARNING: Cannot integrate vessel!");
                GUILayout.Label("Select a human-rated Launch Complex.");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Simulate"))
            {
                _simulationConfigPosition.height = 1;
                EditorLogic.fetch.Lock(true, true, true, "KCTGUILock");
                GUIStates.ShowSimConfig = true;
            }
            if (!KCTGameStates.Settings.OverrideLaunchButton && GUILayout.Button("Integrate"))
            {
                Utilities.TryAddVesselToBuildList();
                KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Show/Hide Management"))
            {
                if (!GUIStates.ShowNewLC && !GUIStates.ShowModifyLC)
                    GUIStates.ShowBuildList = !GUIStates.ShowBuildList;
            }

            RenderEditorLaunchComplexControls();
        }

        private static void ShowBuildVesselFirstDialog()
        {
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowBuildLVFirstDialog",
                                         "#rp0_Editor_LC_BuildVesselFirst_Title",
                                         "#rp0_Editor_LC_BuildVesselFirst_Text",
                                         "#autoLOC_190905",
                                         false,
                                         HighLogic.UISkin);
        }

        private static void RenderEditorLaunchComplexControls()
        {
            LCItem activeLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            bool rightLC = (EditorDriver.editorFacility == EditorFacility.SPH) == (activeLC.LCType == LaunchComplexType.Hangar);
            int lcCount = KCTGameStates.ActiveKSC.LaunchComplexCountPad;

            GUILayout.BeginHorizontal();
            if (rightLC)
            {
                if (EditorDriver.editorFacility == EditorFacility.VAB)
                {
                    if (lcCount > 1 && !GUIStates.ShowModifyLC && GUILayout.Button("<<", GUILayout.ExpandWidth(false)))
                    {
                        KCTGameStates.ActiveKSC.SwitchToPrevLaunchComplex();
                        BuildRateForDisplay = null;
                    }
                }
                GUILayout.FlexibleSpace();
                string lcText = $"{activeLC.Name} ({activeLC.SupportedMassAsPrettyText})";
                string lcTooltip = $"Size limit: {activeLC.SupportedSizeAsPrettyText}\nHuman-Rated: {(activeLC.IsHumanRated ? "Yes" : "No")}";
                GUILayout.Label(new GUIContent(lcText, lcTooltip));
                GUILayout.FlexibleSpace();
                if (EditorDriver.editorFacility == EditorFacility.VAB)
                {
                    if (lcCount > 1 && !GUIStates.ShowModifyLC && GUILayout.Button(">>", GUILayout.ExpandWidth(false)))
                    {
                        KCTGameStates.ActiveKSC.SwitchToNextLaunchComplex();
                        BuildRateForDisplay = null;
                    }
                }
            }
            else
            {
                if (EditorDriver.editorFacility == EditorFacility.VAB)
                {
                    if (lcCount > 0)
                    {
                        if (GUILayout.Button(_gcSwitchToLC))
                            KCTGameStates.ActiveKSC.SwitchToNextLaunchComplex();
                    }
                    else
                    {
                        GUILayout.Label(KCTGameStates.ActiveKSC.LaunchComplexes.Count > 1 ? _gcNoLCAvailableSomeConstructing : _gcNoLCAvailable, GetLabelCenterAlignStyle());
                    }
                }
                else
                {
                    if (KCTGameStates.ActiveKSC.Hangar.IsOperational)
                    {
                        if (GUILayout.Button(_gcSwitchToHangar))
                            KCTGameStates.ActiveKSC.SwitchLaunchComplex(KCTGameStates.ActiveKSC.LaunchComplexes.IndexOf(KCTGameStates.ActiveKSC.Hangar));
                    }
                    else
                    {
                        GUILayout.Label(_gcNoHangar, GetLabelCenterAlignStyle());
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (EditorLogic.fetch.ship.shipFacility == EditorFacility.VAB && GUILayout.Button(_gcNewLC))
            {
                if (EditorLogic.fetch.ship.parts.Count == 0)
                {
                    ShowBuildVesselFirstDialog();
                }
                else
                {
                    SetFieldsFromVessel(KerbalConstructionTime.Instance.EditorVessel);

                    _wasShowBuildList = GUIStates.ShowBuildList;
                    GUIStates.ShowNewLC = true;
                    GUIStates.ShowLCResources = false;
                    GUIStates.ShowModifyLC = false;
                    GUIStates.ShowBuildList = false;
                    GUIStates.ShowBLPlus = false;
                    _centralWindowPosition.width = 300;
                    _centralWindowPosition.height = 1;
                }
            }
            if (rightLC)
            {
                bool canModify = activeLC.CanModifyButton;

                const string modifyFailTooltip = "\n\nCurrently in use! Only modifications that leave any in-progress vessels capable of being serviced by this complex will be permitted.";
                if (GUILayout.Button(new GUIContent("Reconstruct",
                    $"Perform a large reconstruction of the {(activeLC.LCType == LaunchComplexType.Pad ? "launch complex" : "hangar")} to best support the current vessel, removing support for any other variants.{(canModify ? string.Empty : modifyFailTooltip)}"),
                    canModify ? GUI.skin.button : _yellowButton))
                {
                    if (EditorLogic.fetch.ship.parts.Count == 0)
                    {
                        ShowBuildVesselFirstDialog();
                    }
                    else
                    {
                        SetFieldsFromVessel(KerbalConstructionTime.Instance.EditorVessel, activeLC);

                        _wasShowBuildList = GUIStates.ShowBuildList;
                        GUIStates.ShowModifyLC = true;
                        GUIStates.ShowBuildList = false;
                        GUIStates.ShowBLPlus = false;
                        GUIStates.ShowNewLC = false;
                        GUIStates.ShowLCResources = false;
                        _centralWindowPosition.width = 300;
                    }
                }
                if (GUILayout.Button(new GUIContent("Upgrade",
                    $"Upgrade the {(activeLC.LCType == LaunchComplexType.Pad ? "launch complex" : "hangar")} to support the current vessel, keeping existing support where possible.{(canModify ? string.Empty : modifyFailTooltip)}"),
                    canModify ? GUI.skin.button : _yellowButton))
                {
                    if (EditorLogic.fetch.ship.parts.Count == 0)
                    {
                        ShowBuildVesselFirstDialog();
                    }
                    else
                    {
                        SetFieldsFromVesselKeepOld(KerbalConstructionTime.Instance.EditorVessel, activeLC);

                        _wasShowBuildList = GUIStates.ShowBuildList;
                        GUIStates.ShowModifyLC = true;
                        GUIStates.ShowBuildList = false;
                        GUIStates.ShowBLPlus = false;
                        GUIStates.ShowNewLC = false;
                        GUIStates.ShowLCResources = false;
                        _centralWindowPosition.width = 300;
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void RenderEditMode()
        {
            BuildListVessel editedVessel = KerbalConstructionTimeData.Instance.EditedVessel;
            double fullVesselBP = KerbalConstructionTime.Instance.EditorVessel.buildPoints + KerbalConstructionTime.Instance.EditorVessel.integrationPoints;
            double bpLeaderEffect = KerbalConstructionTime.Instance.EditorVessel.LeaderEffect;
            double effic = editedVessel.LC.Efficiency;
            Utilities.GetShipEditProgress(editedVessel, out double newProgressBP, out double originalCompletionPercent, out double newCompletionPercent);
            GUILayout.Label($"Original: {Math.Max(0, originalCompletionPercent):P2}");
            GUILayout.Label($"Edited: {newCompletionPercent:P2}");
            
            double rateWithCurEngis = Utilities.GetBuildRate(editedVessel.LC, KerbalConstructionTime.Instance.EditorVessel.mass, KerbalConstructionTime.Instance.EditorVessel.buildPoints, KerbalConstructionTime.Instance.EditorVessel.humanRated, 0)
                * effic * editedVessel.LC.StrategyRateMultiplier;

            RenderBuildRateInputRow(fullVesselBP, rateWithCurEngis);

            if (double.TryParse(BuildRateForDisplay, out double bR))
            {
                double buildPoints = Math.Abs(fullVesselBP - newProgressBP);
                double buildTime = bR > 0d
                    ? (effic >= LCEfficiency.MaxEfficiency
                        ? buildPoints / (bR * bpLeaderEffect)
                        : editedVessel.CalculateTimeLeftForBuildRate(buildPoints, bR / effic, effic, out _))
                    : double.NaN;
                GUILayout.Label(DTUtils.GetFormattedTime(buildTime, 0, false));

                if (KCTGameStates.EditorRolloutBP > 0)
                {
                    GUILayout.Label($"Rollout Time: {DTUtils.GetFormattedTime(KCTGameStates.EditorRolloutBP / bR, 0, false)}");
                }
            }
            else
            {
                GUILayout.Label("Invalid Integration Rate");
            }

            GUILayout.BeginHorizontal();
            if (EditorDriver.editorFacility == EditorFacility.SPH || (KerbalConstructionTime.Instance.EditorVessel.LC.IsHumanRated && !KerbalConstructionTime.Instance.EditorVessel.humanRated))
            {
                GUILayout.Label("Engineer Cap:");
                GUILayout.Label(KerbalConstructionTime.Instance.EditorVessel.LC.MaxEngineersFor(KerbalConstructionTime.Instance.EditorVessel).ToString(), GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label(string.Empty);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Edits"))
            {
                Utilities.TrySaveShipEdits(editedVessel);
            }
            if (GUILayout.Button("Cancel Edits"))
            {
                KCTDebug.Log("Edits cancelled.");
                KCTGameStates.ClearVesselEditMode();

                HighLogic.LoadScene(GameScenes.SPACECENTER);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Simulate"))
            {
                _simulationConfigPosition.height = 1;
                EditorLogic.fetch.Lock(true, true, true, "KCTGUILock");
                GUIStates.ShowSimConfig = true;

                KerbalConstructionTimeData.Instance.LaunchedVessel = new BuildListVessel(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, EditorLogic.FlagURL, true);
                KerbalConstructionTimeData.Instance.LaunchedVessel.LCID = editedVessel.LC.ID; // should already be correct, but just in case.
            }
            GUILayout.EndHorizontal();

            if (!KerbalConstructionTime.Instance.EditorVessel.AreTanksFull() &&
                GUILayout.Button("Fill Tanks"))
            {
                foreach (Part p in EditorLogic.fetch.ship.parts)
                {
                    //fill as part prefab would be filled?
                    if (Utilities.PartIsProcedural(p))
                    {
                        foreach (PartResource rsc in p.Resources)
                        {
                            if (GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(rsc.resourceName) && rsc.flowState)
                            {
                                rsc.amount = rsc.maxAmount;
                            }
                        }
                    }
                    else
                    {
                        foreach (PartResource rsc in p.Resources)
                        {
                            if (GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(rsc.resourceName) && rsc.flowState)
                            {
                                PartResource templateRsc = p.partInfo.partPrefab.Resources.FirstOrDefault(r => r.resourceName == rsc.resourceName);
                                if (templateRsc != null)
                                    rsc.amount = templateRsc.amount;
                            }
                        }
                    }
                }
            }

            //RenderMergeSection(ship);
        }

        private static void RenderBuildRateInputRow(double buildPoints, double rateWithCurEngis)
        {
            if (BuildRateForDisplay == null)
                BuildRateForDisplay = rateWithCurEngis.ToString();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Integration at");

            BuildRateForDisplay = GUILayout.TextField(BuildRateForDisplay, GUILayout.Width(75));
            GUILayout.Label(" BP/s:");

            double bR;
            if (GUILayout.Button(_gcCurBuildRate, GUILayout.ExpandWidth(false)))
            {
                bR = rateWithCurEngis;
                BuildRateForDisplay = bR.ToString();
            }

            if (GUILayout.Button(_gcMaxBuildRate, GUILayout.ExpandWidth(false)))
            {
                var ship = KerbalConstructionTime.Instance.EditorVessel;
                var deltaToMaxEngineers = int.MaxValue - KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.Engineers;
                bR = Utilities.GetBuildRate(KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance, ship.mass, buildPoints, ship.humanRated, deltaToMaxEngineers)
                    * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.Efficiency * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.StrategyRateMultiplier;
                BuildRateForDisplay = bR.ToString();
            }

            GUILayout.EndHorizontal();
        }

        private static void CheckEditorLock()
        {
            //On mouseover code for editor inspired by Engineer's editor mousover code
            Vector2 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            if (GUIStates.ShowEditorGUI && EditorWindowPosition.Contains(mousePos) && !_isEditorLocked)
            {
                EditorLogic.fetch.Lock(true, false, true, "KCTEditorMouseLock");
                _isEditorLocked = true;
            }
            else if (!(GUIStates.ShowEditorGUI && EditorWindowPosition.Contains(mousePos)) && _isEditorLocked)
            {
                EditorLogic.fetch.Unlock("KCTEditorMouseLock");
                _isEditorLocked = false;
            }
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
