﻿using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static Rect _firstRunWindowPosition = new Rect((Screen.width - 150) / 2, Screen.height / 5, 300, 50);
        private static bool _dontShowFirstRunAgain = false;
        public static void ResetShowFirstRunAgain() { _dontShowFirstRunAgain = false; }

        private static int _lastSteps1 = -1, _lastSteps2 = -1;

        public static void DrawFirstRun(int windowID)
        {
            if (IsPrimarilyDisabled)
            {
                GUIStates.ShowFirstRun = false;
                return;
            }

            if(_lastSteps1 != _lastSteps2)
                _firstRunWindowPosition.height = 50;

            GUILayout.BeginVertical();
            GUILayout.Label("Follow the steps below to get set up.");

            int step = 1;
            //if (PresetManager.Instance.Presets.Count > 1 && GUILayout.Button($"{step++}) Choose a Preset", HighLogic.Skin.button))
            //{
            //    ShowSettings();
            //}

            GUILayout.Label($"{step++}) If you want to play from a different site than Cape Canaveral, switch to the Tracking Station and select a new site.");
            GUILayout.Label("");

            if (!KCTGameStates.StartedProgram)
            {
                GUILayout.Label($"{step++}) Choose your starting Programs.");
                GUILayout.Label("These provide you with funds over time, and allow you to select relevant contracts in Mission Control. Go to the Admin Building to select them.");
                GUILayout.Label("");
            }

            if (!KCTGameStates.StarterLCBuilding)
            {
                GUILayout.Label($"{step++}) Build a starting Launch Complex. You can build new LCs in Space Center Management in the Operations tab, by clicking New.");
                GUILayout.Label("The New LC dialog defaults to a reasonable starting LC for small rockets, though a larger LC (15t, 22x3x3m) is needed later and experience players can start directly with that (or with something else entirely).");
                if (GUILayout.Button($"Build a new LC", HighLogic.Skin.button))
                {
                    GUIStates.ShowNewLC = true;
                }
                GUILayout.Label("");
            }
            
            if (!KCTGameStates.HiredStarterApplicants)
            {
                GUILayout.Label($"{step++}) Assign your {KCTGameStates.UnassignedPersonnel} starting Applicants");
                if (GUILayout.Button($"Go to Staffing", HighLogic.Skin.button))
                {
                    GUIStates.ShowPersonnelWindow = true;
                }
                GUILayout.Label("");
            }

            _dontShowFirstRunAgain = GUILayout.Toggle(_dontShowFirstRunAgain, "Don't show again");
            if (GUILayout.Button("Understood", HighLogic.Skin.button))
            {
                if (_dontShowFirstRunAgain)
                {
                    KCTGameStates.StarterLCBuilding = true;
                    KCTGameStates.HiredStarterApplicants = true;
                    KCTGameStates.StartedProgram = true;
                }
                GUIStates.ShowFirstRun = false;
            }

            if (_lastSteps1 < 0)
                _lastSteps1 = step;

            _lastSteps2 = _lastSteps1;
            _lastSteps1 = step;

            GUILayout.EndVertical();
            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
        }

        private static void CreateStartingPad(LCItem.LCData lcTemplate)
        {
            LCItem starterLC = new LCItem(lcTemplate, KCTGameStates.ActiveKSC)
            {
                IsOperational = true
            };
            KCTGameStates.ActiveKSC.LaunchComplexes.Add(starterLC);
            KCTEvents.OnLCConstructionComplete.Fire(null, starterLC);
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
