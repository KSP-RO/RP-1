using System.Collections.Generic;
using UniLinq;
using ToolbarControl_NS;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static Rect _buildPlansWindowPosition = new Rect(Screen.width - 300, 40, 300, 1);
        private static Vector2 _buildPlansScrollPos;

        private static int _planToDelete;

        private static void DrawBuildPlansWindow(int id)
        {
            int butW = 20;

            GUILayout.BeginVertical();
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (EditorLogic.fetch.ship != null && EditorLogic.fetch.ship.Parts != null && EditorLogic.fetch.ship.Parts.Count > 0)
                {
                    if (EditorLogic.fetch.ship.shipName == "Untitled Space Craft" || EditorLogic.fetch.ship.shipName == "")
                    {
                        if (GUILayout.Button("Cannot Add a Plan Without a Valid Name", GUILayout.Height(2 * 22)))
                        {
                            if (EditorLogic.fetch.ship.shipName == "Untitled Space Craft")
                            {
                                var message = new ScreenMessage("Vessel must have a name other than 'Untitled Space Craft'.", 4f, ScreenMessageStyle.UPPER_CENTER);
                                ScreenMessages.PostScreenMessage(message);
                            }
                            else
                            {
                                var message = new ScreenMessage("Vessel must have a name", 4f, ScreenMessageStyle.UPPER_CENTER);
                                ScreenMessages.PostScreenMessage(message);
                            }
                        }
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Add To Building Plans", GUILayout.Height(2 * 22)))
                        {
                            AddVesselToPlansList();
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Button("No vessel available", GUILayout.Height(2 * 22));
                }
            }
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Available Building Plans");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();

            BuildListWindowPosition.height = EditorBuildListWindowPosition.height = 1;

            GUILayout.EndHorizontal();
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name:");
                GUILayout.EndHorizontal();
                _buildPlansScrollPos = GUILayout.BeginScrollView(_buildPlansScrollPos, GUILayout.Height(250));

                if (KCTGameStates.Plans.Count == 0)
                {
                    GUILayout.Label("No vessels in plans.");
                }
                for (int i = 0; i < KCTGameStates.Plans.Count; i++)
                {
                    BuildListVessel b = KCTGameStates.Plans.Values[i];
                    if (!b.AllPartsValid)
                        continue;
                    GUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("X", _redButton, GUILayout.Width(butW)))
                        {
                            _planToDelete = i;
                            InputLockManager.SetControlLock(ControlTypes.EDITOR_SOFT_LOCK, "KCTPopupLock");
                            _selectedVesselId = b.Id;
                            DialogGUIBase[] options = new DialogGUIBase[2];
                            options[0] = new DialogGUIButton("Yes", RemoveVesselFromPlans);
                            options[1] = new DialogGUIButton("No", RemoveInputLocks);
                            MultiOptionDialog diag = new MultiOptionDialog("scrapVesselPopup", "Are you sure you want to remove this vessel from the plans?", "Delete plan", null, options: options);
                            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
                        }

                        if (GUILayout.Button(b.ShipName))
                        {
                            Utilities.TryAddVesselToBuildList(b.CreateCopy(true), skipPartChecks: true, KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance);
                        }
                    }

                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close"))
                {
                    GUIStates.ShowBuildPlansWindow = false;
                }
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        // Following is mostly duplicating the AddVesselToBuildList set of methods
        public static void AddVesselToPlansList()
        {
            AddVesselToPlansList(EditorLogic.fetch.launchSiteName);
        }

        public static void AddVesselToPlansList(string launchSite)
        {
            if (string.IsNullOrEmpty(launchSite))
            {
                launchSite = EditorLogic.fetch.launchSiteName;
            }
            BuildListVessel blv = new BuildListVessel(EditorLogic.fetch.ship, launchSite, EditorLogic.FlagURL)
            {
                ShipName = EditorLogic.fetch.shipNameField.text,
                LCID = System.Guid.Empty
            };

            var v = new VesselBuildValidator
            {
                SuccessAction = AddVesselToPlansList,
                CheckAvailableFunds = false,
                BypassFacilityRequirements = true
            };
            v.ProcessVessel(blv);
        }

        public static void AddVesselToPlansList(BuildListVessel blv)
        {
            ScreenMessage message;
            if (KCTGameStates.Plans.ContainsKey(blv.ShipName))
            {
                KCTGameStates.Plans.Remove(blv.ShipName);
                message = new ScreenMessage($"Replacing previous plan for {blv.ShipName} in the {blv.LC.Name} Building Plans list.", 4f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(message);
            }
            KCTGameStates.Plans.Add(blv.ShipName, blv);

            KCTDebug.Log($"Added {blv.ShipName} to plans list at KSC {KCTGameStates.ActiveKSC.KSCName}. Cost: {blv.Cost}");
            KCTDebug.Log($"Launch site is {blv.LaunchSite}");
            string text = $"Added {blv.ShipName} to plans list.";
            message = new ScreenMessage(text, 4f, ScreenMessageStyle.UPPER_CENTER);
            ScreenMessages.PostScreenMessage(message);
        }

        private static void RemoveVesselFromPlans()
        {
            InputLockManager.RemoveControlLock("KCTPopupLock");
            KCTGameStates.Plans.RemoveAt(_planToDelete);
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
