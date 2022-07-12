﻿using System;
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
                * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.EfficiencyEngineers * KCTGameStates.EfficiencyEngineers;
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
                double buildRateCapped = Math.Min(bR, Utilities.GetBuildRateCap(buildPoints, KCTGameStates.EditorShipMass, KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance)
                    * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.EfficiencyEngineers * KCTGameStates.EfficiencyEngineers);
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
                GUILayout.Label($"Integration Cost: {Math.Round(KCTGameStates.EditorIntegrationCosts, 1)}");

            if (KCTGameStates.EditorRolloutCosts > 0)
                GUILayout.Label($"Rollout Cost: {Math.Round(KCTGameStates.EditorRolloutCosts, 1)}");

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
        }

        private static void RenderEditMode()
        {
            BuildListVessel ship = KCTGameStates.EditedVessel;
            if (_finishedShipBP < 0 && ship.IsFinished)
            {
                // If ship is finished, then both build and integration times can be refreshed with newly calculated values
                _finishedShipBP = Utilities.GetBuildPoints(ship.ExtractedPartNodes);
                ship.BuildPoints = _finishedShipBP;
                ship.IntegrationPoints = MathParser.ParseIntegrationTimeFormula(ship);
            }

            Utilities.GetShipEditProgress(ship, out double newProgressBP, out double originalCompletionPercent, out double newCompletionPercent);
            GUILayout.Label($"Original: {Math.Max(0, originalCompletionPercent):P2}");
            GUILayout.Label($"Edited: {newCompletionPercent:P2}");

            double rate = Utilities.GetBuildRate(0, ship.Type, ship.LC, KCTGameStates.EditorIsHumanRated, 0)
                * ship.LC.EfficiencyEngineers * KCTGameStates.EfficiencyEngineers;

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
                double buildRateCapped = Math.Min(bR, Utilities.GetBuildRateCap(KCTGameStates.EditorBuildPoints + KCTGameStates.EditorIntegrationPoints, KCTGameStates.EditorShipMass, ship.LC)
                    * KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.EfficiencyEngineers * KCTGameStates.EfficiencyEngineers);
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
                double bp = Utilities.GetBuildPoints(effCost);
                KCTGameStates.LaunchedVessel = new BuildListVessel(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, effCost, bp, EditorLogic.FlagURL, isHumanRated);
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
