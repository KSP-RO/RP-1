using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static Rect _firstRunWindowPosition = new Rect((Screen.width - 150) / 2, Screen.height / 5, 150, 50);

        public static void DrawFirstRun(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Follow the steps below to get set up.");

            int step = 1;
            if (PresetManager.Instance.Presets.Count > 1 && GUILayout.Button($"{step++}) Choose a Preset", HighLogic.Skin.button))
            {
                ShowSettings();
            }

            GUILayout.Label($"{step++}) If you want to play from a different site than Cape Canaveral, switch to the Tracking Station and select a new site.");

            if (!IsPrimarilyDisabled && KCTGameStates.UnassignedPersonnel > 0 &&
                GUILayout.Button($"{step++}) Assign your {KCTGameStates.UnassignedPersonnel} space center personnel", HighLogic.Skin.button))
            {
                GUIStates.ShowPersonnelWindow = true;
            }

            if (GUILayout.Button("Understood", HighLogic.Skin.button))
            {
                GUIStates.ShowFirstRun = false;
            }

            GUILayout.EndVertical();
            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
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
