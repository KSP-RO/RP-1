using UnityEngine;
using RP0;

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

                _activeLCMergeScroll = GUILayout.BeginScrollView(_activeLCMergeScroll, UIHolder.Height(5 * 26 + 5), UIHolder.MaxHeight(1 * Screen.height / 4));

                LCItem lc = KCTGameStates.EditorShipEditingMode ? KCTGameStates.EditedVessel.LC : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
                foreach (BuildListVessel vessel in lc.Warehouse)
                {
                    if (vessel.shipID != ship.shipID && !KCTGameStates.MergedVessels.Exists(x => x.shipID == vessel.shipID) && GUILayout.Button(vessel.shipName))
                    {
                        vessel.RecalculateFromNode();
                        ShipConstruct mergedShip = vessel.CreateShipConstructAndRelease();
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
