﻿using System.Collections.Generic;
using System.Linq;
using ToolbarControl_NS;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static Rect _buildPlansWindowPosition = new Rect(Screen.width - 300, 40, 300, 1);
        private static GUIStyle _buildPlansbutton;
        private static Texture2D _background;
        private static GUIContent _upContent;
        private static GUIContent _hoverContent;
        private static Rect _rect;
        private static float _scale;
        private static GUIContent _content;

        private static bool _isVABSelectedInPlans;
        private static bool _isSPHSelectedInPlans;
        private static SortedList<string, BuildListVessel> _plansList = null;
        private static int _planToDelete;
        private static Texture2D _up;
        private static Texture2D _hover;

        internal static void InitBuildPlans()
        {
            _buildPlansbutton = new GUIStyle(HighLogic.Skin.button);
            _buildPlansbutton.margin = new RectOffset(0, 0, 0, 0);
            _buildPlansbutton.padding = new RectOffset(0, 0, 0, 0);
            _buildPlansbutton.border = new RectOffset(0, 0, 0, 0);
            _buildPlansbutton.normal = _buildPlansbutton.hover;
            _buildPlansbutton.active = _buildPlansbutton.hover;

            _background = new Texture2D(2, 2);
            Color[] color = new Color[4];
            color[0] = new Color(1, 1, 1, 0);
            color[1] = color[0];
            color[2] = color[0];
            color[3] = color[0];
            _background.SetPixels(color);

            _buildPlansbutton.normal.background = _background;
            _buildPlansbutton.hover.background = _background;
            _buildPlansbutton.onHover.background = _background;
            _buildPlansbutton.active.background = _background;
            _buildPlansbutton.onActive.background = _background;

            _up = new Texture2D(2, 2);
            _hover = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref _up, KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/Icons/KCT_add_normal");
            ToolbarControl.LoadImageFromFile(ref _hover, KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/Icons/KCT_add_hover");
            //up = GameDatabase.Instance.GetTexture("RP-0/PluginData/Icons/KCT_add_normal", false);
            //hover = GameDatabase.Instance.GetTexture("RP-0/PluginData/Icons/KCT_add_hover", false);

            PositionAndSizeIcon();
        }

        private static void PositionAndSizeIcon()
        {
            Texture2D upTex = Texture2D.Instantiate(_up);
            Texture2D hoverTex = Texture2D.Instantiate(_hover);

            int offset = 0;
            bool steamPresent = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "KSPSteamCtrlr");
            bool mechjebPresent = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "MechJeb2");
            if (steamPresent)
                offset = 46;
            if (mechjebPresent)
                offset = 140;
            _scale = GameSettings.UI_SCALE;

            _rect = new Rect(Screen.width - (304 + offset) * _scale, 0, 42 * _scale, 38 * _scale);
            {
                TextureScale.Bilinear(upTex, (int)(_up.width * _scale), (int)(_up.height * _scale));
                TextureScale.Bilinear(hoverTex, (int)(_hover.width * _scale), (int)(_hover.height * _scale));
            }
            _upContent = new GUIContent("", upTex, "");
            _hoverContent = new GUIContent("", hoverTex, "");
        }

        private static void DoBuildPlansList()
        {
            if (_rect.Contains(Mouse.screenPos))
                _content = _hoverContent;
            else
                _content = _upContent;
            if (_scale != GameSettings.UI_SCALE)
            {
                PositionAndSizeIcon();
            }
            // When this is true, and the mouse is NOT over the toggle, the toggle code is making the toggle active
            // which is showing the corners of the button as unfilled
            GUIStates.ShowBuildPlansWindow = GUI.Toggle(_rect, GUIStates.ShowBuildPlansWindow, _content, _buildPlansbutton);
        }

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
                                var message = new ScreenMessage("[KCT] Vessel must have a name other than 'Untitled Space Craft'.", 4f, ScreenMessageStyle.UPPER_CENTER);
                                ScreenMessages.PostScreenMessage(message);
                            } else
                            {
                                var message = new ScreenMessage("[KCT] Vessel must have a name", 4f, ScreenMessageStyle.UPPER_CENTER);
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
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Build", GUILayout.Height(2 * 22)))
                        {
                            Utilities.AddVesselToBuildList();
                            Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
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

            bool isVABSelectedNew = GUILayout.Toggle(_isVABSelectedInPlans, "VAB", GUI.skin.button);
            bool isSPHSelectedNew = GUILayout.Toggle(_isSPHSelectedInPlans, "SPH", GUI.skin.button);
            if (isVABSelectedNew != _isVABSelectedInPlans)
            {
                _isVABSelectedInPlans = isVABSelectedNew;
                _isSPHSelectedInPlans = false;
                SelectList("VAB");
            }
            else if (isSPHSelectedNew != _isSPHSelectedInPlans)
            {
                _isSPHSelectedInPlans = isSPHSelectedNew;
                _isVABSelectedInPlans = false;
                SelectList("SPH");
            }

            GUILayout.EndHorizontal();
            {
                if (isVABSelectedNew)
                    _plansList = KCTGameStates.ActiveKSC.VABPlans;
                else if (isSPHSelectedNew)
                    _plansList = KCTGameStates.ActiveKSC.SPHPlans;
                if (_isVABSelectedInPlans || _isSPHSelectedInPlans)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Name:");
                    GUILayout.EndHorizontal();
                    _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(250));

                    if (_plansList == null || _plansList.Count == 0)
                    {
                        GUILayout.Label("No vessels in plans.");
                    }
                    for (int i = 0; i < _plansList.Count; i++)
                    {
                        BuildListVessel b = _plansList.Values[i];
                        if (!b.AllPartsValid)
                            continue;
                        GUILayout.BeginHorizontal();
                        {
                            if (GUILayout.Button("X", _redButton,  GUILayout.Width(butW)))
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
                                Utilities.AddVesselToBuildList(b.CreateCopy(true));
                            }
                        }

                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();
                }
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
        public static BuildListVessel AddVesselToPlansList()
        {
            return AddVesselToPlansList(EditorLogic.fetch.launchSiteName);
        }

        public static BuildListVessel AddVesselToPlansList(string launchSite)
        {
            if (string.IsNullOrEmpty(launchSite))
            {
                launchSite = EditorLogic.fetch.launchSiteName;
            }
            double effCost = Utilities.GetEffectiveCost(EditorLogic.fetch.ship.Parts);
            double bp = Utilities.GetBuildTime(effCost);
            BuildListVessel blv = new BuildListVessel(EditorLogic.fetch.ship, launchSite, effCost, bp, EditorLogic.FlagURL)
            {
                ShipName = EditorLogic.fetch.shipNameField.text
            };
            return AddVesselToPlansList(blv);
        }

        public static BuildListVessel AddVesselToPlansList(BuildListVessel blv)
        {
            ScreenMessage message;
            if (Utilities.CurrentGameIsCareer())
            {
                //Check upgrades
                //First, mass limit
                List<string> facilityChecks = blv.MeetsFacilityRequirements(true);
                if (facilityChecks.Count != 0)
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "editorChecksFailedPopup", "Failed editor checks!",
                        "Warning! This vessel did not pass the editor checks! It will still be added to the plans, but you will not be able to launch it without upgrading. Listed below are the failed checks:\n"
                        + string.Join("\n", facilityChecks.ToArray()), "Acknowledged", false, HighLogic.UISkin);
                }
            }
            string type = "";
            if (blv.Type == BuildListVessel.ListType.VAB)
            {
                if (KCTGameStates.ActiveKSC.VABPlans.ContainsKey(blv.ShipName))
                {
                    KCTGameStates.ActiveKSC.VABPlans.Remove(blv.ShipName);
                    message = new ScreenMessage($"[KCT] Replacing previous plan for {blv.ShipName} in the VAB Building Plans list.", 4f, ScreenMessageStyle.UPPER_CENTER);
                    ScreenMessages.PostScreenMessage(message);
                }
                KCTGameStates.ActiveKSC.VABPlans.Add(blv.ShipName, blv);
                type = "VAB";
            }
            else if (blv.Type == BuildListVessel.ListType.SPH)
            {
                if (KCTGameStates.ActiveKSC.SPHPlans.ContainsKey(blv.ShipName))
                {
                    KCTGameStates.ActiveKSC.SPHPlans.Remove(blv.ShipName);
                    message = new ScreenMessage($"[KCT] Replacing previous plan for {blv.ShipName} in the SPH Building Plans list.", 4f, ScreenMessageStyle.UPPER_CENTER);
                    ScreenMessages.PostScreenMessage(message);
                }
                    KCTGameStates.ActiveKSC.SPHPlans.Add(blv.ShipName, blv);
                type = "SPH";
            }

            ScrapYardWrapper.ProcessVessel(blv.ExtractedPartNodes);

            KCTDebug.Log($"Added {blv.ShipName} to {type} build list at KSC {KCTGameStates.ActiveKSC.KSCName}. Cost: {blv.Cost}");
            KCTDebug.Log($"Launch site is {blv.LaunchSite}");
            message = new ScreenMessage($"[KCT] Added {blv.ShipName} to {type} build list.", 4f, ScreenMessageStyle.UPPER_CENTER);
            ScreenMessages.PostScreenMessage(message);
            return blv;
        }

        private static void RemoveVesselFromPlans()
        {
            InputLockManager.RemoveControlLock("KCTPopupLock");
            _plansList.RemoveAt(_planToDelete);
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
