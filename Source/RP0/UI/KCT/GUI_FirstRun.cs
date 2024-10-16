using UnityEngine;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private static Rect _firstRunWindowPosition = new Rect((Screen.width - 540) / 2, Screen.height / 5, 540, 50);
        private static bool _dontShowFirstRunAgain = false;
        public static void ResetShowFirstRunAgain() { _dontShowFirstRunAgain = false; }

        private static int _lastSteps1 = -1, _lastSteps2 = -1;

        public static void DrawFirstRun(int windowID)
        {
            if (IsPrimarilyDisabled || SpaceCenterManagement.Instance.DontShowFirstRunAgain)
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
            if (GUILayout.Button($"Go to Tracking Station", HighLogic.Skin.button))
            {
                EnterTS();
            }
            GUILayout.Label("");

            if (!SpaceCenterManagement.Instance.StartedProgram)
            {
                GUILayout.Label($"{step++}) Choose your starting Programs.");
                GUILayout.Label("These provide you with funds over time, and allow you to select relevant contracts in Mission Control. Go to the Admin Building to select them. Note that if you're selecting a crewed program like X-Planes, you'll need to hire some nauts!");
                if (GUILayout.Button($"Go to Administration", HighLogic.Skin.button))
                {
                    EnterAdmin();
                }
                GUILayout.Label("");
            }

            if (!SpaceCenterManagement.Instance.AcceptedContract)
            {
                GUILayout.Label($"{step++}) Accept a Contract.");
                GUILayout.Label("With a Program selected, you now have access to the Contracts associated with that Program. Programs have some number of optional and some number of required contracts. Optional contracts aren't necessary to complete the program, but award Confidence.");
                if (GUILayout.Button($"Go to Mission Control", HighLogic.Skin.button))
                {
                    EnterMC();
                }
                GUILayout.Label("");
            }

            if (!SpaceCenterManagement.Instance.StarterLCBuilding)
            {
                GUILayout.Label($"{step++}) Build a starting Launch Complex.");
                GUILayout.Label("With a contract accepted, now it's time to create and integrate a vessel to complete it. If it's a rocket, you'll need a launch complex to launch it. Go to the VAB and make your rocket, then click New the Integration Info (was KCT) window. The LC properties will be set to support that vessel. Once you have LCs built you can also modify them the same way.");
                if (GUILayout.Button($"Go to VAB", HighLogic.Skin.button))
                {
                    EnterVAB();
                }
                GUILayout.Label("");
            }
            
            if (!SpaceCenterManagement.Instance.HiredStarterApplicants)
            {
                GUILayout.Label($"{step++}) Assign your {SpaceCenterManagement.Instance.Applicants} starting Applicants");
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
                    SpaceCenterManagement.Instance.DontShowFirstRunAgain = true;
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
            GameEvents.onGUIAdministrationFacilityDespawn.Add(SCMEvents.Instance.OnExitAdmin);
            GUIStates.ShowFirstRun = false;
        }

        private static void EnterMC()
        {
            GameEvents.onGUIMissionControlSpawn.Fire();
            GameEvents.onGUIMissionControlDespawn.Add(SCMEvents.Instance.OnExitMC);
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
