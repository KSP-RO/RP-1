using RP0;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private const int _firstRunWindowWidth = 540;
        
        private static Rect _firstRunWindowPosition = new Rect((Screen.width - _firstRunWindowWidth * UIHolder.UIScale) / 2, Screen.height / 5, _firstRunWindowWidth * UIHolder.UIScale, 1);
        private static bool _dontShowFirstRunAgain = false;
        public static void ResetShowFirstRunAgain() { _dontShowFirstRunAgain = false; }

        private static int _lastSteps1 = -1, _lastSteps2 = -1;

        public static void DrawFirstRun(int windowID)
        {
            // reset fixed height to not have overflow on very long Labels
            var multilineStyle = GetMultilineStyle(GUI.skin.label);

            if (IsPrimarilyDisabled)
            {
                GUIStates.ShowFirstRun = false;
                return;
            }

            if(_lastSteps1 != _lastSteps2)
                _firstRunWindowPosition.height = 50 * UIHolder.UIScale;

            GUILayout.BeginVertical();
            GUILayout.Label("Follow the steps below to get set up.", multilineStyle, UIHolder.MaxWidth(_firstRunWindowWidth));

            int step = 1;
            //if (PresetManager.Instance.Presets.Count > 1 && GUILayout.Button($"{step++}) Choose a Preset", HighLogic.Skin.button))
            //{
            //    ShowSettings();
            //}

            GUILayout.Label($"{step++}) If you want to play from a different site than Cape Canaveral, switch to the Tracking Station and select a new site.", multilineStyle, UIHolder.MaxWidth(_firstRunWindowWidth));
            if (GUILayout.Button($"Go to Tracking Station"))
            {
                EnterTS();
            }
            GUILayout.Label("");

            if (!KerbalConstructionTimeData.Instance.StartedProgram)
            {
                GUILayout.Label($"{step++}) Choose your starting Programs.", multilineStyle, UIHolder.MaxWidth(_firstRunWindowWidth));
                GUILayout.Label("These provide you with funds over time, and allow you to select relevant contracts in Mission Control. Go to the Admin Building to select them.", multilineStyle, UIHolder.MaxWidth(_firstRunWindowWidth));
                if (GUILayout.Button($"Go to Administration"))
                {
                    EnterAdmin();
                }
                GUILayout.Label("");
            }

            if (!KerbalConstructionTimeData.Instance.AcceptedContract)
            {
                GUILayout.Label($"{step++}) Accept a Contract.", multilineStyle, UIHolder.MaxWidth(_firstRunWindowWidth));
                GUILayout.Label("With a Program selected, you now have access to the Contracts associated with that Program. Programs have some number of optional and some number of required contracts. Optional contracts aren't necessary to complete the program, but award Confidence.", multilineStyle, UIHolder.MaxWidth(_firstRunWindowWidth));
                if (GUILayout.Button($"Go to Mission Control"))
                {
                    EnterMC();
                }
                GUILayout.Label("");
            }

            if (!KerbalConstructionTimeData.Instance.StarterLCBuilding)
            {
                GUILayout.Label($"{step++}) Build a starting Launch Complex.", multilineStyle, UIHolder.MaxWidth(_firstRunWindowWidth));
                GUILayout.Label("With a contract accepted, now it's time to build a vessel to complete it. If it's a rocket, you'll need a launch complex to launch it. Go to the VAB and make your rocket, then click New the Integration Info (was KCT) window. The LC properties will be set to support that vessel. Once you have LCs built you can also modify them the same way, either a simple Upgrade or a bigger Reconstruction", multilineStyle, UIHolder.MaxWidth(_firstRunWindowWidth));
                if (GUILayout.Button($"Go to VAB"))
                {
                    EnterVAB();
                }
                GUILayout.Label("");
            }
            
            if (!KerbalConstructionTimeData.Instance.HiredStarterApplicants)
            {
                GUILayout.Label($"{step++}) Assign your {KerbalConstructionTimeData.Instance.Applicants} starting Applicants", multilineStyle, UIHolder.MaxWidth(480));
                if (GUILayout.Button($"Go to Staffing"))
                {
                    GUIStates.ShowPersonnelWindow = true;
                }
                GUILayout.Label("");
            }

            _dontShowFirstRunAgain = GUILayout.Toggle(_dontShowFirstRunAgain, "Don't show again");
            if (GUILayout.Button("Understood"))
            {
                if (_dontShowFirstRunAgain)
                {
                    KerbalConstructionTimeData.Instance.StarterLCBuilding = true;
                    KerbalConstructionTimeData.Instance.HiredStarterApplicants = true;
                    KerbalConstructionTimeData.Instance.StartedProgram = true;
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

        private static void EnterVAB()
        {
            EditorFacility editorFacility = EditorFacility.None;
            if (ShipConstruction.ShipConfig != null)
            {
                editorFacility = ShipConstruction.ShipType;
            }
            int startupBehaviour;
            if (editorFacility != EditorFacility.VAB)
            {
                startupBehaviour = 0;
            }
            else
            {
                startupBehaviour = 1;
            }
            EditorDriver.StartupBehaviour = (EditorDriver.StartupBehaviours)startupBehaviour;
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            EditorDriver.StartEditor(EditorFacility.VAB);
        }

        private static void EnterAdmin()
        {
            GameEvents.onGUIAdministrationFacilitySpawn.Fire();
            GameEvents.onGUIAdministrationFacilityDespawn.Add(KCTEvents.Instance.OnExitAdmin);
            GUIStates.ShowFirstRun = false;
        }

        private static void EnterMC()
        {
            GameEvents.onGUIMissionControlSpawn.Fire();
            GameEvents.onGUIMissionControlDespawn.Add(KCTEvents.Instance.OnExitMC);
            GUIStates.ShowFirstRun = false;
        }

        private static void EnterTS()
        {
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            HighLogic.LoadScene(GameScenes.TRACKSTATION);
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
