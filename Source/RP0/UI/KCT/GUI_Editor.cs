using System;
using UniLinq;
using UnityEngine;
using ROUtils;

namespace RP0
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
            if (!SpaceCenterManagement.EditorShipEditingMode)
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
            double buildPoints = SpaceCenterManagement.Instance.EditorVessel.buildPoints;
            double bpLeaderEffect = SpaceCenterManagement.Instance.EditorVessel.LeaderEffect;
            double effic = SpaceCenterManagement.Instance.ActiveSC.ActiveLC.Efficiency;
            double rateWithCurEngis = KCTUtilities.GetBuildRate(SpaceCenterManagement.Instance.ActiveSC.ActiveLC, SpaceCenterManagement.Instance.EditorVessel.mass, SpaceCenterManagement.Instance.EditorVessel.buildPoints, SpaceCenterManagement.Instance.EditorVessel.humanRated, 0)
                * effic
                * SpaceCenterManagement.Instance.ActiveSC.ActiveLC.StrategyRateMultiplier;

            RenderBuildRateInputRow(buildPoints, rateWithCurEngis);

            if (KSP.UI.Screens.DebugToolbar.DebugScreenSpawner.Instance?.screen?.isShown ?? false)
            {
                GUILayout.Label($"BP Cost: {buildPoints:N0}");
                GUILayout.Label($"EC: {SpaceCenterManagement.Instance.EditorVessel.effectiveCost:N0}");
            }

            if (double.TryParse(BuildRateForDisplay, out double bR))
            {
                double buildTime = bR > 0d
                    ? (effic >= LCEfficiency.MaxEfficiency
                        ? buildPoints / (bR * bpLeaderEffect)
                        : SpaceCenterManagement.Instance.EditorVessel.CalculateTimeLeftForBuildRate(buildPoints, bR / effic, effic, out _))
                    : 0d;
                GUILayout.Label($"Integration Time: {(bR > 0 ? KSPUtil.PrintDateDeltaCompact(buildTime, true, false) : "infinity")} at {effic:P0}");

                if (SpaceCenterManagement.EditorRolloutBP > 0)
                {
                    GUILayout.Label($"Rollout Time: {(bR > 0 ? KSPUtil.PrintDateDeltaCompact(SpaceCenterManagement.EditorRolloutBP / bR, true, false) : "infinity")} at {effic:P0}");
                }
            }
            else
            {
                GUILayout.Label("Invalid Integration Rate");
            }

            if (EditorDriver.editorFacility == EditorFacility.SPH || (SpaceCenterManagement.Instance.ActiveSC.ActiveLC.LCType == LaunchComplexType.Pad && SpaceCenterManagement.Instance.ActiveSC.ActiveLC.IsHumanRated && !SpaceCenterManagement.Instance.EditorVessel.humanRated))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Engineer Cap:");
                GUILayout.Label((EditorDriver.editorFacility == EditorFacility.SPH ? SpaceCenterManagement.Instance.ActiveSC.Hangar : SpaceCenterManagement.Instance.ActiveSC.ActiveLC).MaxEngineersFor(SpaceCenterManagement.Instance.EditorVessel).ToString(), GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();
            }

            if (bR > 0d && rateWithCurEngis > 0d)
            {
                double effectiveEngCount = bR / rateWithCurEngis * SpaceCenterManagement.Instance.ActiveSC.ActiveLC.Engineers;
                double salaryPerDayAboveIdle = Database.SettingsSC.salaryEngineers * (1d / 365.25d) * (1d - Database.SettingsSC.IdleSalaryMult);
                double cost = buildPoints / bR / 86400d * effectiveEngCount * salaryPerDayAboveIdle;
                GUILayout.Label(new GUIContent($"Net Salary: √{-CurrencyUtils.Funds(TransactionReasonsRP0.SalaryEngineers, -cost):N1}", "The extra salary paid above the idle rate for these engineers"));
            }

            if (SpaceCenterManagement.EditorRolloutCost > 0)
                GUILayout.Label($"Rollout Cost: √{-CurrencyUtils.Funds(TransactionReasonsRP0.RocketRollout, -SpaceCenterManagement.EditorRolloutCost):N1}");

            bool showCredit = false;
            if (SpaceCenterManagement.EditorUnlockCosts > 0)
            {
                showCredit = true;
                GUILayout.Label($"Unlock Cost: √{-CurrencyUtils.Funds(TransactionReasonsRP0.PartOrUpgradeUnlock, -SpaceCenterManagement.EditorUnlockCosts):N1}");
            }

            if (SpaceCenterManagement.EditorToolingCosts > 0)
            {
                showCredit = true;
                GUILayout.Label($"Tooling Cost: √{-CurrencyUtils.Funds(TransactionReasonsRP0.ToolingPurchase, -SpaceCenterManagement.EditorToolingCosts):N1}");
            }

            if (showCredit)
                GUILayout.Label($"Unlock Credit: √{UnlockCreditHandler.Instance.TotalCredit:N1}");

            if (SpaceCenterManagement.EditorRequiredTechs.Count > 0)
            {
                string techLabel = string.Empty;
                foreach (string techId in SpaceCenterManagement.EditorRequiredTechs)
                {
                    string techName = ResearchAndDevelopment.GetTechnologyTitle(techId);

                    if (string.IsNullOrEmpty(techLabel))
                        techLabel = $"Needs: {techName}";
                    else
                        techLabel += $"\n       {techName}";
                }
                GUILayout.Label(techLabel);
            }

            if (SpaceCenterManagement.Instance.EditorVessel.humanRated && !SpaceCenterManagement.Instance.ActiveSC.ActiveLC.IsHumanRated)
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
                GameplayTips.Instance.CheckAndShowExcessECTip(EditorLogic.fetch.ship);
            }
            if (!KCTSettings.Instance.OverrideLaunchButton && GUILayout.Button("Integrate"))
            {
                KCTUtilities.TryAddVesselToBuildList();
                SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
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
                                         HighLogic.UISkin).HideGUIsWhilePopup();
        }

        private static void RenderEditorLaunchComplexControls()
        {
            LaunchComplex activeLC = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;
            bool rightLC = (EditorDriver.editorFacility == EditorFacility.SPH) == (activeLC.LCType == LaunchComplexType.Hangar);
            int lcCount = SpaceCenterManagement.Instance.ActiveSC.LaunchComplexCountPad;

            GUILayout.BeginHorizontal();
            if (rightLC)
            {
                if (EditorDriver.editorFacility == EditorFacility.VAB)
                {
                    if (lcCount > 1 && !GUIStates.ShowModifyLC && GUILayout.Button("<<", GUILayout.ExpandWidth(false)))
                    {
                        SpaceCenterManagement.Instance.ActiveSC.SwitchToPrevLaunchComplex();
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
                        SpaceCenterManagement.Instance.ActiveSC.SwitchToNextLaunchComplex();
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
                            SpaceCenterManagement.Instance.ActiveSC.SwitchToNextLaunchComplex();
                    }
                    else
                    {
                        GUILayout.Label(SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes.Count > 1 ? _gcNoLCAvailableSomeConstructing : _gcNoLCAvailable, GetLabelCenterAlignStyle());
                    }
                }
                else
                {
                    if (SpaceCenterManagement.Instance.ActiveSC.Hangar.IsOperational)
                    {
                        if (GUILayout.Button(_gcSwitchToHangar))
                            SpaceCenterManagement.Instance.ActiveSC.SwitchLaunchComplex(SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes.IndexOf(SpaceCenterManagement.Instance.ActiveSC.Hangar));
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
                    SetFieldsFromVessel(SpaceCenterManagement.Instance.EditorVessel);

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
                //if (GUILayout.Button(new GUIContent("Reconstruct",
                //    $"Perform a large reconstruction of the {(activeLC.LCType == LaunchComplexType.Pad ? "launch complex" : "hangar")} to best support the current vessel, removing support for any other variants.{(canModify ? string.Empty : modifyFailTooltip)}"),
                //    canModify ? GUI.skin.button : _yellowButton))
                //{
                //    if (EditorLogic.fetch.ship.parts.Count == 0)
                //    {
                //        ShowBuildVesselFirstDialog();
                //    }
                //    else
                //    {
                //        SetFieldsFromVessel(KerbalConstructionTimeData.Instance.EditorVessel, activeLC);

                //        _wasShowBuildList = GUIStates.ShowBuildList;
                //        GUIStates.ShowModifyLC = true;
                //        GUIStates.ShowBuildList = false;
                //        GUIStates.ShowBLPlus = false;
                //        GUIStates.ShowNewLC = false;
                //        GUIStates.ShowLCResources = false;
                //        _centralWindowPosition.width = 300;
                //    }
                //}
                if (GUILayout.Button(new GUIContent("Modify",
                    $"Modify the {(activeLC.LCType == LaunchComplexType.Pad ? "launch complex" : "hangar")} to support the current vessel, keeping existing support where possible.{(canModify ? string.Empty : modifyFailTooltip)}"),
                    canModify ? GUI.skin.button : _yellowButton))
                {
                    if (EditorLogic.fetch.ship.parts.Count == 0)
                    {
                        ShowBuildVesselFirstDialog();
                    }
                    else
                    {
                        SetFieldsFromVesselKeepOld(SpaceCenterManagement.Instance.EditorVessel, activeLC);

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
            VesselProject editedVessel = SpaceCenterManagement.Instance.EditedVessel;
            double fullVesselBP = SpaceCenterManagement.Instance.EditorVessel.buildPoints;
            double bpLeaderEffect = SpaceCenterManagement.Instance.EditorVessel.LeaderEffect;
            double effic = editedVessel.LC.Efficiency;
            KCTUtilities.GetShipEditProgress(editedVessel, out double newProgressBP, out double originalCompletionPercent, out double newCompletionPercent);
            GUILayout.Label($"Original: {Math.Max(0, originalCompletionPercent):P2}");
            GUILayout.Label($"Edited: {newCompletionPercent:P2}");
            
            double rateWithCurEngis = KCTUtilities.GetBuildRate(editedVessel.LC, SpaceCenterManagement.Instance.EditorVessel.mass, SpaceCenterManagement.Instance.EditorVessel.buildPoints, SpaceCenterManagement.Instance.EditorVessel.humanRated, 0)
                * effic * editedVessel.LC.StrategyRateMultiplier;

            RenderBuildRateInputRow(fullVesselBP, rateWithCurEngis);

            if (double.TryParse(BuildRateForDisplay, out double bR))
            {
                double startingEff = effic;
                double rolloutEff = effic;
                int idx = editedVessel.LC.BuildList.IndexOf(editedVessel);
                if (idx != -1 && bR > 0d && effic < LCEfficiency.MaxEfficiency)
                {
                    double brTrue = bR / effic;
                    for (int i = 0; i < idx; ++i)
                        editedVessel.LC.BuildList[i].CalculateTimeLeftForBuildRate(editedVessel.buildPoints - editedVessel.progress, brTrue, startingEff, out startingEff);
                }

                double buildPoints = Math.Abs(fullVesselBP - newProgressBP);
                double buildTime = bR > 0d
                    ? (effic >= LCEfficiency.MaxEfficiency
                        ? buildPoints / (bR * bpLeaderEffect)
                        : editedVessel.CalculateTimeLeftForBuildRate(buildPoints, bR / effic, startingEff, out rolloutEff))
                    : double.NaN;
                GUILayout.Label(new GUIContent($"Remaining: {RP0DTUtils.GetFormattedTime(buildTime, 0, false)}", 
                    idx == -1 ? "Time left takes efficiency increase into account based on assuming vessel will be placed at head of integration list" :
                    "Time left takes efficiency increase into account based on vessel's current place in the integration list"));

                if (SpaceCenterManagement.EditorRolloutBP > 0)
                {
                    GUILayout.Label($"Rollout Time: {RP0DTUtils.GetFormattedTime(SpaceCenterManagement.EditorRolloutBP / (bR / effic * rolloutEff) , 0, false)} at {rolloutEff:P0}");
                }
            }
            else
            {
                GUILayout.Label("Invalid Integration Rate");
            }

            GUILayout.BeginHorizontal();
            VesselProject curVessel = SpaceCenterManagement.Instance.EditorVessel;
            if (curVessel.LC != null && (EditorDriver.editorFacility == EditorFacility.SPH || (curVessel.LC.IsHumanRated && !curVessel.humanRated)))
            {
                GUILayout.Label("Engineer Cap:");
                GUILayout.Label(curVessel.LC.MaxEngineersFor(curVessel).ToString(), GetLabelRightAlignStyle());
            }
            else
            {
                GUILayout.Label(string.Empty);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Edits"))
            {
                KCTUtilities.TrySaveShipEdits(editedVessel);
            }
            if (GUILayout.Button("Cancel Edits"))
            {
                RP0Debug.Log("Edits cancelled.");
                SpaceCenterManagement.ClearVesselEditMode();

                HighLogic.LoadScene(GameScenes.SPACECENTER);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Simulate"))
            {
                _simulationConfigPosition.height = 1;
                EditorLogic.fetch.Lock(true, true, true, "KCTGUILock");
                GUIStates.ShowSimConfig = true;

                SpaceCenterManagement.Instance.LaunchedVessel = new VesselProject(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, EditorLogic.FlagURL, true);
                SpaceCenterManagement.Instance.LaunchedVessel.LCID = editedVessel.LC.ID; // should already be correct, but just in case.
            }
            GUILayout.EndHorizontal();

            if (!SpaceCenterManagement.Instance.EditorVessel.AreTanksFull() &&
                GUILayout.Button("Fill Tanks"))
            {
                foreach (Part p in EditorLogic.fetch.ship.parts)
                {
                    //fill as part prefab would be filled?
                    if (KCTUtilities.PartIsProcedural(p))
                    {
                        foreach (PartResource rsc in p.Resources)
                        {
                            if ((Database.ResourceInfo.LCResourceTypes.ValueOrDefault(rsc.resourceName) & LCResourceType.Fuel) != 0 && rsc.flowState)
                            {
                                rsc.amount = rsc.maxAmount;
                            }
                        }
                    }
                    else
                    {
                        foreach (PartResource rsc in p.Resources)
                        {
                            if ((Database.ResourceInfo.LCResourceTypes.ValueOrDefault(rsc.resourceName) & LCResourceType.Fuel) != 0 && rsc.flowState)
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
                var ship = SpaceCenterManagement.Instance.EditorVessel;
                var deltaToMaxEngineers = int.MaxValue - SpaceCenterManagement.Instance.ActiveSC.ActiveLC.Engineers;
                bR = KCTUtilities.GetBuildRate(SpaceCenterManagement.Instance.ActiveSC.ActiveLC, ship.mass, buildPoints, ship.humanRated, deltaToMaxEngineers)
                    * SpaceCenterManagement.Instance.ActiveSC.ActiveLC.Efficiency * SpaceCenterManagement.Instance.ActiveSC.ActiveLC.StrategyRateMultiplier;
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
