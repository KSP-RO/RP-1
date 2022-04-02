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

            KSCItem ksc = KCTGameStates.KSCs.Find(k => k.LaunchComplexes.Count > 1);
            if (ksc == null)
            {
                GUILayout.Label($"{step++}) Choose a starting Launch Complex:");
                if (GUILayout.Button($"1t Capacity (for small rockets). Also gives √{PresetManager.Instance.ActivePreset.GeneralSettings.SmallLCExtraFunds:N0}", HighLogic.Skin.button))
                {
                    LCItem starterLC = new LCItem(LCItem.StartingLC1, KCTGameStates.ActiveKSC);
                    starterLC.IsOperational = true;
                    KCTGameStates.ActiveKSC.LaunchComplexes.Add(starterLC);
                    Utilities.AddFunds(PresetManager.Instance.ActivePreset.GeneralSettings.SmallLCExtraFunds, TransactionReasons.None);
                }
                if (GUILayout.Button($"15t Capacity (min: {LCItem.CalcMassMin(15):N0}t)", HighLogic.Skin.button))
                {
                    LCItem starterLC = new LCItem(LCItem.StartingLC15, KCTGameStates.ActiveKSC);
                    starterLC.IsOperational = true;
                    KCTGameStates.ActiveKSC.LaunchComplexes.Add(starterLC);
                }
            }
            
            if (!IsPrimarilyDisabled && KCTGameStates.UnassignedPersonnel > 0 &&
                GUILayout.Button($"{step++}) Assign your {KCTGameStates.UnassignedPersonnel} space center applicants", HighLogic.Skin.button))
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
