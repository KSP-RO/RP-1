using UnityEngine;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private static Vector2 _activeLCMergeScroll;
        private static bool _showMergeSelectionList = false;

        private static void RenderMergeSection(VesselProject ship)
        {
            if (!_showMergeSelectionList && SpaceCenterManagement.Instance.MergingAvailable && GUILayout.Button("Merge Built Vessel"))
            {
                _showMergeSelectionList = true;
            }

            if (_showMergeSelectionList && SpaceCenterManagement.Instance.MergingAvailable)
            {
                if (GUILayout.Button("Hide Merge Selection"))
                {
                    _showMergeSelectionList = false;
                }

                GUILayout.BeginVertical();
                GUILayout.Label("Choose a vessel");

                _activeLCMergeScroll = GUILayout.BeginScrollView(_activeLCMergeScroll, GUILayout.Height(5 * 26 + 5), GUILayout.MaxHeight(1 * Screen.height / 4));

                LaunchComplex lc = SpaceCenterManagement.EditorShipEditingMode ? SpaceCenterManagement.Instance.EditedVessel.LC : SpaceCenterManagement.Instance.ActiveSC.ActiveLC;
                foreach (VesselProject vessel in lc.Warehouse)
                {
                    if (vessel.shipID != ship.shipID && !SpaceCenterManagement.Instance.MergedVessels.Exists(x => x.shipID == vessel.shipID) && GUILayout.Button(vessel.shipName))
                    {
                        vessel.RecalculateFromNode();
                        ShipConstruct mergedShip = vessel.CreateShipConstructAndRelease();
                        EditorLogic.fetch.SpawnConstruct(mergedShip);

                        SpaceCenterManagement.Instance.MergedVessels.Add(vessel);
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }
    }
}
