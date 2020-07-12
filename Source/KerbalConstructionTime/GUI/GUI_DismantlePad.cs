using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        public static void DrawDismantlePadWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Are you sure you want to dismantle the currently selected launch pad? This cannot be undone!");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Yes"))
            {
                if (KCTGameStates.ActiveKSC.LaunchPadCount < 2) return;

                KCT_LaunchPad lpToDel = KCTGameStates.ActiveKSC.ActiveLPInstance;
                if (!lpToDel.Delete(out string err))
                {
                    ScreenMessages.PostScreenMessage("Dismantle failed: " + err, 5f, ScreenMessageStyle.UPPER_CENTER);
                }

                GUIStates.ShowDismantlePad = false;
                GUIStates.ShowBuildList = true;
            }

            if (GUILayout.Button("No"))
            {
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowDismantlePad = false;
                GUIStates.ShowBuildList = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            CenterWindow(ref _centralWindowPosition);
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
