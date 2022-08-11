using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        public static Rect EditorWindowPosition = new Rect(Screen.width / 3.5f, Screen.height / 3.5f, 275, 135);
        public static string BuildRateForDisplay;

        private static double _finishedShipBP = -1;
        private static bool _isEditorLocked = false;

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
            double buildPoints = KCTGameStates.EditorBuildPoints + KCTGameStates.EditorIntegrationPoints;
            BuildListVessel.ListType type = EditorLogic.fetch.ship.shipFacility == EditorFacility.VAB ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH;
            double rate = Utilities.GetBuildRate(0, type, KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance, KCTGameStates.EditorIsHumanRated, 0)
                * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.Efficiency
                * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.StrategyRateMultiplier;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Time at ");
            if (BuildRateForDisplay == null)
                BuildRateForDisplay = rate.ToString();
            BuildRateForDisplay = GUILayout.TextField(BuildRateForDisplay, GUILayout.Width(75));
            GUILayout.Label(" BP/s:");

            double bR;
            if (GUILayout.Button(new GUIContent("*", "Reset Build Rate"), GUILayout.ExpandWidth(false)))
            {
                bR = rate;
                BuildRateForDisplay = bR.ToString();
            }
            if (double.TryParse(BuildRateForDisplay, out bR))
            {
                GUILayout.EndHorizontal();
                double buildRateCapped = Math.Min(bR, Utilities.GetBuildRate(KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance, KCTGameStates.EditorShipMass, buildPoints, KCTGameStates.EditorIsHumanRated)
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

            if (KCTGameStates.EditorIntegrationCosts > 0)
                GUILayout.Label($"Integration Cost: √{KCTGameStates.EditorIntegrationCosts:N1}");

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

            if (KCTGameStates.EditorIsHumanRated && !KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.IsHumanRated)
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
                GUIStates.ShowBuildList = !GUIStates.ShowBuildList;
            }

            RenderEditorLaunchComplexControls();
        }

        private static void RenderEditorLaunchComplexControls()
        {
            LCItem activeLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

            GUILayout.BeginHorizontal();
            int lcCount = KCTGameStates.ActiveKSC.LaunchComplexCount;
            if (lcCount > 1 && GUILayout.Button("<<", GUILayout.ExpandWidth(false)))
            {
                KCTGameStates.ActiveKSC.SwitchToPrevLaunchComplex();
                BuildRateForDisplay = null;
            }
            GUILayout.FlexibleSpace();
            string lcText = $"{activeLC.Name} ({activeLC.SupportedMassAsPrettyText})";
            string lcTooltip = $"Size limit: {activeLC.SupportedSizeAsPrettyText}\nHuman-Rated: {(activeLC.IsHumanRated ? "Yes" : "No")}";
            GUILayout.Label(new GUIContent(lcText, lcTooltip));
            GUILayout.FlexibleSpace();
            if (lcCount > 1 && GUILayout.Button(">>", GUILayout.ExpandWidth(false)))
            {
                KCTGameStates.ActiveKSC.SwitchToNextLaunchComplex();
                BuildRateForDisplay = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (EditorLogic.fetch.ship.shipFacility == EditorFacility.VAB && GUILayout.Button(new GUIContent("New LC", "Build a new launch complex for this vessel")))
            {
                _newName = $"Launch Complex {(KCTGameStates.ActiveKSC.LaunchComplexes.Count)}";
                _lengthLimit = Mathf.CeilToInt(KCTGameStates.EditorShipSize.z * 1.5f).ToString();
                _widthLimit = Mathf.CeilToInt(KCTGameStates.EditorShipSize.x * 1.5f).ToString();
                _heightLimit = Mathf.CeilToInt(KCTGameStates.EditorShipSize.y * 1.1f).ToString();
                _tonnageLimit = Mathf.CeilToInt((float)(KCTGameStates.EditorShipMass * 1.1d)).ToString();
                _isHumanRated = KCTGameStates.EditorIsHumanRated;

                GUIStates.ShowNewLC = true;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
                _centralWindowPosition.width = 300;
            }
            bool canModify = activeLC.CanModify 
                && ((activeLC.LCType == LaunchComplexType.Hangar && EditorLogic.fetch.ship.shipFacility == EditorFacility.SPH) 
                    || (activeLC.LCType == LaunchComplexType.Pad && EditorLogic.fetch.ship.shipFacility == EditorFacility.VAB));
            const string modifyFailTooltip = "Currently in use! No projects can be underway or\nvessels at pads/airlaunching, though vessels can be in storage.";
            if (GUILayout.Button(new GUIContent("Modify", canModify ? ("Modify " + (activeLC.LCType == LaunchComplexType.Pad ? "launch complex limits" : "hangar limits")) : modifyFailTooltip),
                canModify ? GUI.skin.button : _yellowButton))
            {
                _lengthLimit = Mathf.CeilToInt(KCTGameStates.EditorShipSize.z * 1.5f).ToString();
                _widthLimit = Mathf.CeilToInt(KCTGameStates.EditorShipSize.x * 1.5f).ToString();
                _heightLimit = Mathf.CeilToInt(KCTGameStates.EditorShipSize.y * 1.1f).ToString();
                _tonnageLimit = Mathf.CeilToInt((float)(KCTGameStates.EditorShipMass * 1.1d)).ToString();
                _isHumanRated = EditorLogic.fetch.ship.shipFacility == EditorFacility.SPH || KCTGameStates.EditorIsHumanRated;

                GUIStates.ShowModifyLC = true;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
                GUIStates.ShowNewLC = false;
                _centralWindowPosition.width = 300;
            }
            GUILayout.EndHorizontal();
        }

        private static void RenderEditMode()
        {
            BuildListVessel ship = KCTGameStates.EditedVessel;
            if (_finishedShipBP < 0 && ship.IsFinished)
            {
                // If ship is finished, then both build and integration times can be refreshed with newly calculated values
                _finishedShipBP = Utilities.GetVesselBuildPoints(ship.ExtractedPartNodes);
                ship.BuildPoints = _finishedShipBP;
                ship.IntegrationPoints = Formula.GetIntegrationBP(ship);
            }

            Utilities.GetShipEditProgress(ship, out double newProgressBP, out double originalCompletionPercent, out double newCompletionPercent);
            GUILayout.Label($"Original: {Math.Max(0, originalCompletionPercent):P2}");
            GUILayout.Label($"Edited: {newCompletionPercent:P2}");

            double rate = Utilities.GetBuildRate(0, ship.Type, ship.LC, KCTGameStates.EditorIsHumanRated, 0)
                * ship.LC.Efficiency * ship.LC.StrategyRateMultiplier;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Time at ");
            if (BuildRateForDisplay == null)
                BuildRateForDisplay = rate.ToString();
            BuildRateForDisplay = GUILayout.TextField(BuildRateForDisplay, GUILayout.Width(75));
            GUILayout.Label(" BP/s:");

            double bR;
            if (GUILayout.Button(new GUIContent("*", "Reset Build Rate"), GUILayout.ExpandWidth(false)))
            {
                bR = rate;
                BuildRateForDisplay = bR.ToString();
            }

            if (double.TryParse(BuildRateForDisplay, out bR))
            {
                GUILayout.EndHorizontal();
                double buildRateCapped = Math.Min(bR, Utilities.GetBuildRate(ship.LC, KCTGameStates.EditorShipMass, KCTGameStates.EditorBuildPoints + KCTGameStates.EditorIntegrationPoints, KCTGameStates.EditorIsHumanRated)
                    * ship.LC.Efficiency * ship.LC.StrategyRateMultiplier);
                GUILayout.Label(Utilities.GetFormattedTime(Math.Abs(KCTGameStates.EditorBuildPoints + KCTGameStates.EditorIntegrationPoints - newProgressBP) / buildRateCapped, 0, false));

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
            if (GUILayout.Button("Save Edits"))
            {
                _finishedShipBP = -1;
                Utilities.TrySaveShipEdits(ship);
            }
            if (GUILayout.Button("Cancel Edits"))
            {
                KCTDebug.Log("Edits cancelled.");
                _finishedShipBP = -1;
                ScrapYardWrapper.ProcessVessel(KCTGameStates.EditedVessel.ExtractedPartNodes);
                KCTGameStates.ClearVesselEditMode();

                HighLogic.LoadScene(GameScenes.SPACECENTER);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Simulate"))
            {
                _finishedShipBP = -1;
                _simulationConfigPosition.height = 1;
                EditorLogic.fetch.Lock(true, true, true, "KCTGUILock");
                GUIStates.ShowSimConfig = true;

                bool isHumanRated;
                double effCost = Utilities.GetEffectiveCost(EditorLogic.fetch.ship.Parts, out isHumanRated);
                double bp = Utilities.GetVesselBuildPoints(effCost);
                KCTGameStates.LaunchedVessel = new BuildListVessel(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, effCost, bp, EditorLogic.FlagURL, isHumanRated);
                KCTGameStates.LaunchedVessel.LCID = ship.LC.ID;
            }
            GUILayout.EndHorizontal();

            if (KCTGameStates.LaunchedVessel != null && !KCTGameStates.LaunchedVessel.AreTanksFull() &&
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

            RenderMergeSection(ship);
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
