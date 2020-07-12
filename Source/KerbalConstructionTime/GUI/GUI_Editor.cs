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

        private static int _rateIndexHolder = 0;
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
            double buildTime = KCTGameStates.EditorBuildTime + KCTGameStates.EditorIntegrationTime;
            BuildListVessel.ListType type = EditorLogic.fetch.launchSiteName == "LaunchPad" ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Time at ");
            if (BuildRateForDisplay == null) BuildRateForDisplay = Utilities.GetBuildRate(0, type, null).ToString();
            BuildRateForDisplay = GUILayout.TextField(BuildRateForDisplay, GUILayout.Width(75));
            GUILayout.Label(" BP/s:");

            List<double> rates;
            if (type == BuildListVessel.ListType.VAB) rates = Utilities.BuildRatesVAB(null);
            else rates = Utilities.BuildRatesSPH(null);

            if (double.TryParse(BuildRateForDisplay, out double bR))
            {
                if (GUILayout.Button("*", GUILayout.ExpandWidth(false)))
                {
                    _rateIndexHolder = (_rateIndexHolder + 1) % rates.Count;
                    bR = rates[_rateIndexHolder];
                    if (bR > 0)
                        BuildRateForDisplay = bR.ToString();
                    else
                    {
                        _rateIndexHolder = (_rateIndexHolder + 1) % rates.Count;
                        bR = rates[_rateIndexHolder];
                        BuildRateForDisplay = bR.ToString();
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Label(MagiCore.Utilities.GetFormattedTime(buildTime / bR));
            }
            else
            {
                GUILayout.EndHorizontal();
                GUILayout.Label("Invalid Build Rate");
            }

            if (KCTGameStates.EditorRolloutTime > 0)
            {
                bR = Utilities.GetVABBuildRateSum(KCTGameStates.ActiveKSC);
                GUILayout.Label($"Rollout Time: {MagiCore.Utilities.GetFormattedTime(KCTGameStates.EditorRolloutTime / bR)}");
            }

            if (KCTGameStates.EditorIntegrationCosts > 0)
                GUILayout.Label($"Integration Cost: {Math.Round(KCTGameStates.EditorIntegrationCosts, 1)}");

            if (KCTGameStates.EditorRolloutCosts > 0)
                GUILayout.Label($"Rollout Cost: {Math.Round(KCTGameStates.EditorRolloutCosts, 1)}");

            if (!KCTGameStates.Settings.OverrideLaunchButton)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Build"))
                {
                    Utilities.AddVesselToBuildList();
                    Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                }
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Show/Hide Build List"))
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
                _finishedShipBP = Utilities.GetBuildTime(ship.ExtractedPartNodes);
                ship.BuildPoints = _finishedShipBP;
                ship.IntegrationPoints = MathParser.ParseIntegrationTimeFormula(ship);
            }

            double origBP = ship.IsFinished ? _finishedShipBP : ship.BuildPoints;
            origBP += ship.IntegrationPoints;
            double buildTime = KCTGameStates.EditorBuildTime + KCTGameStates.EditorIntegrationTime;
            double difference = Math.Abs(buildTime - origBP);
            double progress;
            if (ship.IsFinished) progress = origBP;
            else progress = ship.Progress;
            double newProgress = Math.Max(0, progress - (1.1 * difference));
            GUILayout.Label($"Original: {Math.Max(0, Math.Round(100 * (progress / origBP), 2))}%");
            GUILayout.Label($"Edited: {Math.Round(100 * newProgress / buildTime, 2)}%");

            BuildListVessel.ListType type = EditorLogic.fetch.launchSiteName == "LaunchPad" ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Time at ");
            if (BuildRateForDisplay == null) BuildRateForDisplay = Utilities.GetBuildRate(0, type, null).ToString();
            BuildRateForDisplay = GUILayout.TextField(BuildRateForDisplay, GUILayout.Width(75));
            GUILayout.Label(" BP/s:");
            List<double> rates = new List<double>();
            if (ship.Type == BuildListVessel.ListType.VAB) rates = Utilities.BuildRatesVAB(null);
            else rates = Utilities.BuildRatesSPH(null);
            if (double.TryParse(BuildRateForDisplay, out double bR))
            {
                if (GUILayout.Button("*", GUILayout.ExpandWidth(false)))
                {
                    _rateIndexHolder = (_rateIndexHolder + 1) % rates.Count;
                    bR = rates[_rateIndexHolder];
                    BuildRateForDisplay = bR.ToString();
                }
                GUILayout.EndHorizontal();
                GUILayout.Label(MagiCore.Utilities.GetFormattedTime(Math.Abs(buildTime - newProgress) / bR));
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
                Utilities.AddFunds(ship.GetTotalCost(), TransactionReasons.VesselRollout);
                BuildListVessel newShip = Utilities.AddVesselToBuildList();
                if (newShip == null)
                {
                    Utilities.SpendFunds(ship.GetTotalCost(), TransactionReasons.VesselRollout);
                    return;
                }

                ship.RemoveFromBuildList();
                newShip.Progress = newProgress;
                newShip.RushBuildClicks = ship.RushBuildClicks;
                KCTDebug.Log($"Finished? {ship.IsFinished}");
                if (ship.IsFinished)
                    newShip.CannotEarnScience = true;

                GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);

                KCTGameStates.EditorShipEditingMode = false;

                InputLockManager.RemoveControlLock("KCTEditExit");
                InputLockManager.RemoveControlLock("KCTEditLoad");
                InputLockManager.RemoveControlLock("KCTEditNew");
                InputLockManager.RemoveControlLock("KCTEditLaunch");
                EditorLogic.fetch.Unlock("KCTEditorMouseLock");
                KCTDebug.Log("Edits saved.");

                HighLogic.LoadScene(GameScenes.SPACECENTER);
            }
            if (GUILayout.Button("Cancel Edits"))
            {
                KCTDebug.Log("Edits cancelled.");
                _finishedShipBP = -1;
                KCTGameStates.EditorShipEditingMode = false;

                InputLockManager.RemoveControlLock("KCTEditExit");
                InputLockManager.RemoveControlLock("KCTEditLoad");
                InputLockManager.RemoveControlLock("KCTEditNew");
                InputLockManager.RemoveControlLock("KCTEditLaunch");
                EditorLogic.fetch.Unlock("KCTEditorMouseLock");

                ScrapYardWrapper.ProcessVessel(KCTGameStates.EditedVessel.ExtractedPartNodes);

                HighLogic.LoadScene(GameScenes.SPACECENTER);
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
