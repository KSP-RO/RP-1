using System;
using UniLinq;
using UnityEngine;
using RP0;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private const int _editorWindowWidth = 275;

        public static Rect EditorWindowPosition = new Rect(Screen.width / 3.5f, Screen.height / 3.5f, _editorWindowWidth * UIHolder.UIScale, 1);
        public static string BuildRateForDisplay;

        private static bool _isEditorLocked = false;
        private static bool _wasShowBuildList = false;
        private static readonly GUIContent _gcResetBuildRate = new GUIContent("*", "Reset Build Rate");
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
            if (EditorWindowPosition.width < _editorWindowWidth * UIHolder.UIScale)    // the size keeps getting changed for some reason, so this will avoid that
            {
                EditorWindowPosition.width = _editorWindowWidth * UIHolder.UIScale;
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
            BuildListVessel.ListType type = EditorLogic.fetch.ship.shipFacility == EditorFacility.VAB ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH;
            double rate = Utilities.GetBuildRate(0, type, KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance, KerbalConstructionTime.Instance.EditorVessel.humanRated, 0)
                * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.Efficiency
                * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.StrategyRateMultiplier;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Time at ");
            if (BuildRateForDisplay == null)
                BuildRateForDisplay = rate.ToString();
            BuildRateForDisplay = GUILayout.TextField(BuildRateForDisplay, UIHolder.Width(75));
            GUILayout.Label(" BP/s:");

            double bR;
            if (GUILayout.Button(_gcResetBuildRate, GUILayout.ExpandWidth(false)))
            {
                bR = rate;
                BuildRateForDisplay = bR.ToString();
            }
            if (double.TryParse(BuildRateForDisplay, out bR))
            {
                GUILayout.EndHorizontal();
                double buildRateCapped = Math.Min(bR, Utilities.GetBuildRate(KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance, KerbalConstructionTime.Instance.EditorVessel.mass, buildPoints, KerbalConstructionTime.Instance.EditorVessel.humanRated)
                    * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.Efficiency * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.StrategyRateMultiplier);
                GUILayout.Label(Utilities.GetFormattedTime(buildPoints / buildRateCapped, 0, false));

                if (KCTGameStates.EditorRolloutTime > 0)
                {
                    GUILayout.Label($"Rollout Time: {Utilities.GetFormattedTime(KCTGameStates.EditorRolloutTime / buildRateCapped, 0, false)}");
                }
            }
            else
            {
                GUILayout.EndHorizontal();
                GUILayout.Label("Invalid Build Rate");
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

            if (KCTGameStates.EditorRolloutCosts > 0)
                GUILayout.Label($"Rollout Cost: √{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.RocketRollout, -KCTGameStates.EditorRolloutCosts):N1}");

            if (KCTGameStates.EditorUnlockCosts > 0)
                GUILayout.Label($"Unlock Cost: √{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.PartOrUpgradeUnlock, -KCTGameStates.EditorUnlockCosts):N1}");

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
                GUILayout.Label("WARNING: Cannot build vessel!");
                GUILayout.Label("Select a human-rated Launch Complex.");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Simulate"))
            {
                _simulationConfigPosition.height = 1;
                EditorLogic.fetch.Lock(true, true, true, "KCTGUILock");
                GUIStates.ShowSimConfig = true;
            }
            if (!KCTGameStates.Settings.OverrideLaunchButton && GUILayout.Button("Build"))
            {
                Utilities.TryAddVesselToBuildList();
                Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Show/Hide Management"))
            {
                if (!GUIStates.ShowNewLC && !GUIStates.ShowModifyLC)
                    GUIStates.ShowBuildList = !GUIStates.ShowBuildList;
            }

            RenderEditorLaunchComplexControls();
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
                SetFieldsFromVessel(KerbalConstructionTime.Instance.EditorVessel);

                _wasShowBuildList = GUIStates.ShowBuildList;
                GUIStates.ShowNewLC = true;
                GUIStates.ShowLCResources = false;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
                _centralWindowPosition.width = 300 * UIHolder.UIScale;
            }
            if (rightLC)
            {
                bool canModify = activeLC.CanModify;

                const string modifyFailTooltip = "Currently in use! Only modifications that leave any in-progress vessels capable of being serviced by this complex will be permitted.";
                const string wrongLCTooltip = "This is the wrong vessel type (plane/rocket) for this complex type. Select another complex / the hangar";
                if (GUILayout.Button(new GUIContent("Reconstruct",
                    rightLC ? (canModify ? $"Perform a large reconstruction of the {(activeLC.LCType == LaunchComplexType.Pad ? "launch complex" : "hangar")} to best support the current vessel, removing support for any other variants." : modifyFailTooltip) : wrongLCTooltip),
                    rightLC ? canModify ? GUI.skin.button : _yellowButton : _redButton))
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
                if (GUILayout.Button(new GUIContent("Upgrade",
                    rightLC ? (canModify ? $"Upgrade the {(activeLC.LCType == LaunchComplexType.Pad ? "launch complex" : "hangar")} to support the current vessel, keeping existing support where possible." : modifyFailTooltip) : wrongLCTooltip),
                    rightLC ? canModify ? GUI.skin.button : _yellowButton : _redButton))
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
            GUILayout.EndHorizontal();
        }

        private static void RenderEditMode()
        {
            BuildListVessel ship = KCTGameStates.EditedVessel;
            
            Utilities.GetShipEditProgress(ship, out double newProgressBP, out double originalCompletionPercent, out double newCompletionPercent);
            GUILayout.Label($"Original: {Math.Max(0, originalCompletionPercent):P2}");
            GUILayout.Label($"Edited: {newCompletionPercent:P2}");

            double rate = Utilities.GetBuildRate(0, ship.Type, ship.LC, KerbalConstructionTime.Instance.EditorVessel.humanRated, 0)
                * ship.LC.Efficiency * ship.LC.StrategyRateMultiplier;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Time at ");
            if (BuildRateForDisplay == null)
                BuildRateForDisplay = rate.ToString();
            BuildRateForDisplay = GUILayout.TextField(BuildRateForDisplay, UIHolder.Width(75));
            GUILayout.Label(" BP/s:");

            double bR;
            if (GUILayout.Button(_gcResetBuildRate, GUILayout.ExpandWidth(false)))
            {
                bR = rate;
                BuildRateForDisplay = bR.ToString();
            }

            if (double.TryParse(BuildRateForDisplay, out bR))
            {
                GUILayout.EndHorizontal();
                double bp = KerbalConstructionTime.Instance.EditorVessel.buildPoints + KerbalConstructionTime.Instance.EditorVessel.integrationPoints;
                double buildRateCapped = Math.Min(bR, Utilities.GetBuildRate(ship.LC, KerbalConstructionTime.Instance.EditorVessel.mass, bp, KerbalConstructionTime.Instance.EditorVessel.humanRated)
                    * ship.LC.Efficiency * ship.LC.StrategyRateMultiplier);
                GUILayout.Label(Utilities.GetFormattedTime(Math.Abs(bp - newProgressBP) / buildRateCapped, 0, false));

                if (KCTGameStates.EditorRolloutTime > 0)
                {
                    GUILayout.Label($"Rollout Time: {Utilities.GetFormattedTime(KCTGameStates.EditorRolloutTime / buildRateCapped, 0, false)}");
                }
            }
            else
            {
                GUILayout.EndHorizontal();
                GUILayout.Label("Invalid Build Rate");
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
                Utilities.TrySaveShipEdits(ship);
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

                KCTGameStates.LaunchedVessel = new BuildListVessel(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, EditorLogic.FlagURL);
                KCTGameStates.LaunchedVessel.LCID = ship.LC.ID;
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
