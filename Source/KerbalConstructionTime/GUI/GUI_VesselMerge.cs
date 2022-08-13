using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static Vector2 _activeLCMergeScroll;
        private static bool _showMergeSelectionList = false;

        private static void RenderMergeSection(BuildListVessel ship)
        {
            if (!_showMergeSelectionList && KCTGameStates.MergingAvailable && GUILayout.Button("Merge Built Vessel"))
            {
                _showMergeSelectionList = true;
            }

            if (_showMergeSelectionList && KCTGameStates.MergingAvailable)
            {
                if (GUILayout.Button("Hide Merge Selection"))
                {
                    _showMergeSelectionList = false;
                }

                GUILayout.BeginVertical();
                GUILayout.Label("Choose a vessel");

                _activeLCMergeScroll = GUILayout.BeginScrollView(_activeLCMergeScroll, GUILayout.Height(5 * 26 + 5), GUILayout.MaxHeight(1 * Screen.height / 4));

                foreach (BuildListVessel vessel in KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.Warehouse)
                {
                    if (vessel.Id != ship.Id && !KCTGameStates.MergedVessels.Exists(x => x.Id == vessel.Id) && GUILayout.Button(vessel.ShipName))
                    {
                        vessel.RecalculateFromNode();
                        ShipConstruct mergedShip = new ShipConstruct();
                        mergedShip.LoadShip(vessel.ShipNode);
                        EditorLogic.fetch.SpawnConstruct(mergedShip);

                        KCTGameStates.MergedVessels.Add(vessel);
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }
    }
}
