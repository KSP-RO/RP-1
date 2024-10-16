using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using ROUtils.DataTypes;
using ROUtils;

namespace RP0
{
    public static partial class KCT_GUI
    {
        public enum VesselPadStatus { InStorage, RollingOut, RolledOut, RollingBack, Recovering };
        public enum RenameType { None, Vessel, Pad, LaunchComplex };

        public static Rect BuildListWindowPosition = new Rect(Screen.width - 400, 40, 500, 1);
        public static Rect EditorBuildListWindowPosition = new Rect(Screen.width - 400, 40, 500, 1);

        private static List<string> _launchSites = new List<string>();
        private static int _mouseOnRolloutButton = -1;
        private static int _mouseOnAirlaunchButton = -1;
        private static bool _isIntegrationSelected, _isConstructionSelected, _isResearchSelected, _isCombinedSelected;
        private static Vector2 _launchSiteScrollView;
        private static Guid _selectedVesselId = new Guid();
        private static bool _isSelectingLaunchSiteForVessel = true;

        private static double _accumulatedTimeBefore;

        private static GUIStyle _redText, _yellowText, _greenText, _blobText, _yellowButton, _redButton, _greenButton;
        private static GUIContent _emptyTexture, _settingsTexture, _planeTexture, _rocketTexture, _techTexture, _constructTexture, 
            _reconTexture, _rolloutTexture, _rollbackTexture, _airlaunchTexture, _recoveryTexture, _hangarTexture, _repairTexture;
        private const int _width1 = 120;
        private const int _width2 = 100;
        private const int _butW = 20;

        public static void SelectList(string list)
        {
            BuildListWindowPosition.height = EditorBuildListWindowPosition.height = 1;
            switch (list)
            {
                case "Integration":
                    _isIntegrationSelected = !_isIntegrationSelected;
                    _isConstructionSelected = false;
                    _isResearchSelected = false;
                    _isCombinedSelected = false;
                    break;
                case "Construction":
                    _isIntegrationSelected = false;
                    _isConstructionSelected = !_isConstructionSelected;
                    _isResearchSelected = false;
                    _isCombinedSelected = false;
                    break;
                case "Research":
                    _isIntegrationSelected = false;
                    _isConstructionSelected = false;
                    _isResearchSelected = !_isResearchSelected;
                    _isCombinedSelected = false;
                    break;
                case "Combined":
                    _isCombinedSelected = !_isCombinedSelected;
                    _isIntegrationSelected = false;
                    _isConstructionSelected = false;
                    _isResearchSelected = false;
                    break;
                default:
                    _isIntegrationSelected = _isConstructionSelected = _isResearchSelected = _isCombinedSelected = false;
                    break;
            }
        }

        public static void ResetBLWindow(bool deselectList = true)
        {
            BuildListWindowPosition.height = EditorBuildListWindowPosition.height = 1;
            BuildListWindowPosition.width = EditorBuildListWindowPosition.width = 500;
            //if (deselectList)
            //    SelectList("None");
        }

        public static void InitBuildListVars()
        {
            RP0Debug.Log("InitBuildListVars");
            _redText = new GUIStyle(GUI.skin.label);
            _redText.normal.textColor = XKCDColors.KSPNotSoGoodOrange;
            _yellowText = new GUIStyle(GUI.skin.label);
            _yellowText.normal.textColor = XKCDColors.KSPMellowYellow;
            _greenText = new GUIStyle(GUI.skin.label);
            _greenText.normal.textColor = Color.green;
            _blobText = new GUIStyle(GUI.skin.label);
            _blobText.fontSize = 30;
            _blobText.fixedHeight = 20;
            _blobText.alignment = TextAnchor.MiddleCenter;

            _yellowButton = new GUIStyle(GUI.skin.button);
            _yellowButton.normal.textColor = XKCDColors.KSPMellowYellow;
            _yellowButton.hover.textColor = XKCDColors.KSPMellowYellow;
            _yellowButton.active.textColor = XKCDColors.KSPMellowYellow;
            _redButton = new GUIStyle(GUI.skin.button);
            _redButton.normal.textColor = XKCDColors.KSPNotSoGoodOrange;
            _redButton.hover.textColor = XKCDColors.KSPNotSoGoodOrange;
            _redButton.active.textColor = XKCDColors.KSPNotSoGoodOrange;

            _greenButton = new GUIStyle(GUI.skin.button);
            _greenButton.normal.textColor = Color.green;
            _greenButton.hover.textColor = Color.green;
            _greenButton.active.textColor = Color.green;

            _airlaunchTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_airlaunch16", false));
            _constructTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_construct16", false));
            _planeTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_flight16", false));
            _hangarTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_hangar16", false));
            _recoveryTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_landing16", false));
            _reconTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_recon16", false));
            _rocketTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_rocket16", false));
            _rollbackTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_rollback16", false));
            _rolloutTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_rollout16", false));
            _settingsTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_settings16", false));
            _techTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_tech16", false));
            _repairTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/Resources/KCT_repair", false));
            _emptyTexture = new GUIContent("");
        }

        public static void DrawBuildListWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Next:", _windowSkin.label);
            ISpaceCenterProject buildItem = KCTUtilities.GetNextThingToFinish();
            if (buildItem != null)
            {
                string txt = buildItem.GetItemName(), locTxt = "VAB";
                if (buildItem.GetProjectType() == ProjectType.None)
                {
                    locTxt = string.Empty;
                }
                else if (buildItem.GetProjectType() == ProjectType.Reconditioning)
                {
                    ReconRolloutProject reconRoll = buildItem as ReconRolloutProject;
                    if (reconRoll.RRType == ReconRolloutProject.RolloutReconType.Reconditioning)
                    {
                        txt = "Reconditioning";
                        locTxt = reconRoll.launchPadID;
                    }
                    else if (reconRoll.RRType == ReconRolloutProject.RolloutReconType.Rollout)
                    {
                        VesselProject associated = reconRoll.LC.Warehouse.FirstOrDefault(vp => vp.shipID == reconRoll.AssociatedIdAsGuid);
                        txt = $"{associated.shipName} Rollout";
                        locTxt = reconRoll.launchPadID;
                    }
                    else if (reconRoll.RRType == ReconRolloutProject.RolloutReconType.Rollback)
                    {
                        VesselProject associated = reconRoll.LC.Warehouse.FirstOrDefault(vp => vp.shipID == reconRoll.AssociatedIdAsGuid);
                        txt = $"{associated.shipName} Rollback";
                        locTxt = reconRoll.launchPadID;
                    }
                    else if (reconRoll.RRType == ReconRolloutProject.RolloutReconType.Recovery)
                    {
                        VesselProject associated = reconRoll.LC.Warehouse.FirstOrDefault(vp => vp.shipID == reconRoll.AssociatedIdAsGuid);
                        txt = $"{associated.shipName} Recovery";
                        locTxt = associated.LC.Name;
                    }
                    else
                    {
                        locTxt = "Storage";
                    }
                }
                else if (buildItem.GetProjectType() == ProjectType.AirLaunch)
                {
                    ReconRolloutProject ar = buildItem as ReconRolloutProject;
                    VesselProject associated = ar.AssociatedVP;
                    if (associated != null)
                    {
                        if (ar.RRType == ReconRolloutProject.RolloutReconType.AirlaunchMount)
                            txt = $"{associated.shipName} Mounting";
                        else
                            txt = $"{associated.shipName} Unmounting";
                    }
                    else
                        txt = "Airlaunch Operations";

                    locTxt = ar.LC.Name;

                }
                else if (buildItem.GetProjectType() == ProjectType.VAB || buildItem.GetProjectType() == ProjectType.SPH)
                {
                    VesselProject vp = buildItem as VesselProject;
                    locTxt = vp == null || vp.LC == null ? "Vessel" : vp.LC.Name;
                }
                else if (buildItem.GetProjectType() == ProjectType.TechNode)
                {
                    locTxt = "Tech";
                }
                else if (buildItem.GetProjectType() == ProjectType.KSC)
                {
                    locTxt = "KSC";
                }
                else if (buildItem.GetProjectType() == ProjectType.Crew)
                {
                    locTxt = txt;
                    txt = "Training";
                }

                GUILayout.Label(txt);
                GUILayout.Label(locTxt, _windowSkin.label);
                GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(buildItem.GetTimeLeft(), txt+locTxt+buildItem.GetItemName()));

                if (!HighLogic.LoadedSceneIsEditor && TimeWarp.CurrentRateIndex == 0 && GUILayout.Button(new GUIContent($"Warp to{Environment.NewLine}Complete", $"√ Gain/Loss:\n{SpaceCenterManagement.Instance.GetBudgetDelta(buildItem.GetTimeLeft()):N0}")))
                {
                    KCTWarpController.Create(null); // warp to next item
                }
                else if (!HighLogic.LoadedSceneIsEditor && TimeWarp.CurrentRateIndex > 0 && GUILayout.Button($"Stop{Environment.NewLine}Warp"))
                {
                    KCTWarpController.Instance?.StopWarp();
                    TimeWarp.SetRate(0, true);  // If the controller doesn't exist, stop warp anyway.
                }

                if (KCTSettings.Instance.AutoKACAlarms && KACWrapper.APIReady && buildItem.GetTimeLeft() > 30)    //don't check if less than 30 seconds to completion. Might fix errors people are seeing
                {
                    double UT = Planetarium.GetUniversalTime();
                    if (!KCTUtilities.IsApproximatelyEqual(SpaceCenterManagement.Instance.KACAlarmUT - UT, buildItem.GetTimeLeft()))
                    {
                        RP0Debug.Log("KAC Alarm being created!");
                        SpaceCenterManagement.Instance.KACAlarmUT = buildItem.GetTimeLeft() + UT;
                        KACWrapper.KACAPI.KACAlarm alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.ID == SpaceCenterManagement.Instance.KACAlarmId);
                        if (alarm == null)
                        {
                            alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.Name.StartsWith("RP-1: "));
                        }
                        if (alarm != null)
                        {
                            RP0Debug.Log("Removing existing alarm");
                            KACWrapper.KAC.DeleteAlarm(alarm.ID);
                        }
                        txt = "RP-1: ";
                        if (buildItem.GetProjectType() == ProjectType.Reconditioning)
                        {
                            ReconRolloutProject reconRoll = buildItem as ReconRolloutProject;
                            if (reconRoll.RRType == ReconRolloutProject.RolloutReconType.Reconditioning)
                            {
                                txt += $"{reconRoll.launchPadID} Reconditioning";
                            }
                            else if (reconRoll.RRType == ReconRolloutProject.RolloutReconType.Rollout)
                            {
                                VesselProject associated = reconRoll.LC.Warehouse.FirstOrDefault(vp => vp.shipID == reconRoll.AssociatedIdAsGuid);
                                txt += $"{associated.shipName} rollout at {reconRoll.launchPadID}";
                            }
                            else if (reconRoll.RRType == ReconRolloutProject.RolloutReconType.Rollback)
                            {
                                VesselProject associated = reconRoll.LC.Warehouse.FirstOrDefault(vp => vp.shipID == reconRoll.AssociatedIdAsGuid);
                                txt += $"{associated.shipName} rollback at {reconRoll.launchPadID}";
                            }
                            else
                            {
                                txt += $"{buildItem.GetItemName()} Complete";
                            }
                        }
                        else
                            txt += $"{buildItem.GetItemName()} Complete";
                        SpaceCenterManagement.Instance.KACAlarmId = KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Raw, txt, SpaceCenterManagement.Instance.KACAlarmUT);
                        RP0Debug.Log($"Alarm created with ID: {SpaceCenterManagement.Instance.KACAlarmId}");
                    }
                }
            }
            else
            {
                GUILayout.Label("No Active Projects");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();


            bool integrationSelectedNew = GUILayout.Toggle(_isIntegrationSelected, "Integration", GUI.skin.button);
            if (integrationSelectedNew != _isIntegrationSelected)
                SelectList("Integration");

            bool constructionSelectedNew = false;
            if (KSPUtils.CurrentGameIsCareer())
                constructionSelectedNew = GUILayout.Toggle(_isConstructionSelected, "Construction", GUI.skin.button);
            if (constructionSelectedNew != _isConstructionSelected)
                SelectList("Construction");

            bool techSelectedNew = false;
            if (KSPUtils.CurrentGameHasScience())
                techSelectedNew = GUILayout.Toggle(_isResearchSelected, "Research", GUI.skin.button);
            if (techSelectedNew != _isResearchSelected)
                SelectList("Research");

            bool combinedSelectedNew = GUILayout.Toggle(_isCombinedSelected, "Combined", GUI.skin.button);
            if (combinedSelectedNew != _isCombinedSelected)
                SelectList("Combined");

            //if (GUILayout.Button("Upgrades", AvailablePoints > 0 ? _greenButton : GUI.skin.button))
            //{
            //    GUIStates.ShowUpgradeWindow = true;
            //    GUIStates.ShowBuildList = false;
            //    GUIStates.ShowBLPlus = false;
            //    _LCIndex = KerbalConstructionTimeData.Instance.ActiveKSC.ActiveLaunchComplexID;
            //}
            bool hasIdleEngineers = false;
            // This reimplements FreeEngineers for speed, since we also have to check LCs for idle
            int engCount = SpaceCenterManagement.Instance.ActiveSC.Engineers;
            foreach (var lc in SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes)
            {
                if (!lc.IsOperational || lc.Engineers == 0)
                    continue;

                engCount -= lc.Engineers;

                if (!lc.IsActive)
                {
                    hasIdleEngineers = true;
                    break;
                }
            }
            if (!hasIdleEngineers && engCount > 0)
                hasIdleEngineers = true;

            if (GUILayout.Button(new GUIContent("Staff", hasIdleEngineers ? "Some engineers are idle!" : (SpaceCenterManagement.Instance.Applicants > 0 ? "Applicants can be hired for free!" : "Hire/fire/reassign staff")),
                hasIdleEngineers ? _yellowButton : (SpaceCenterManagement.Instance.Applicants > 0 ? _greenButton : GUI.skin.button)))
            {
                GUIStates.ShowPersonnelWindow = true;
                //GUIStates.ShowBuildList = false;
                //GUIStates.ShowBLPlus = false;
                _LCIndex = SpaceCenterManagement.Instance.ActiveSC.LCIndex;
                _currentPersonnelHover = PersonnelButtonHover.None;
            }
            if (GUILayout.Button("Plans"))
            {
                GUIStates.ShowBuildPlansWindow = !GUIStates.ShowBuildPlansWindow;
            }
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                if (GUILayout.Button(_settingsTexture, GUILayout.ExpandWidth(false)))
                {
                    GUIStates.ShowBuildList = false;
                    GUIStates.ShowBLPlus = false;
                    ShowSettings();
                }
            }
            GUILayout.EndHorizontal();

            if (_isIntegrationSelected)
            {
                RenderBuildList();
            }
            else if (_isConstructionSelected)
            {
                RenderConstructionList();
            }
            else if (_isResearchSelected)
            {
                RenderTechList();
            }
            else if (_isCombinedSelected)
            {
                RenderCombinedList();
            }

            GUILayout.EndVertical();

            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();

            ref Rect pos = ref (HighLogic.LoadedSceneIsEditor ? ref EditorBuildListWindowPosition : ref BuildListWindowPosition);
            ClampWindow(ref pos, strict: true);
        }

        private static void RenderConstructionList()
        {
            LCSpaceCenter ksc = SpaceCenterManagement.Instance.ActiveSC;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:");
            GUILayout.Label("Progress:", GUILayout.Width(_width1 / 2 + 30));
            GUILayout.Label(KCTSettings.Instance.UseDates ? "Completes:" : "Time Left:", GUILayout.Width(_width1));
            GUILayout.Space(20);
            GUILayout.EndHorizontal();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(350));

            if (ksc.Constructions.Count == 0)
                GUILayout.Label("No constructions under way at this space center.");

            int cancelID = -1;
            double totalCost = 0d;
            for (int i = 0; i < ksc.Constructions.Count; i++)
            {
                ConstructionProject constr = ksc.Constructions[i];
                double rCost = constr.RemainingCost;
                totalCost += rCost;
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("X", GUILayout.Width(_butW)))
                {
                    cancelID = i;
                    DialogGUIBase[] options = new DialogGUIBase[2];
                    options[0] = new DialogGUIButton("Yes", () => { CancelConstruction(cancelID); });
                    options[1] = new DialogGUIButton("No", () => { });
                    MultiOptionDialog diag = new MultiOptionDialog("cancelConstructionPopup", $"Are you sure you want to stop building {constr.GetItemName()}?\n\nYou have already spent <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1> {constr.spentRushCost:N0} funds on this construction ({(constr.spentCost / constr.cost):P0} of the total).", "Cancel Construction?", null, 300, options);
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
                }

                double buildRate = constr.GetBuildRate();
                DrawTypeIcon(constr);
                KCTUtilities.GetConstructionTooltip(constr, i, out string costTooltip, out string identifier);
                GUILayout.Label(new GUIContent(constr.GetItemName(), "name" + costTooltip));
                GUILayout.Label(new GUIContent($"{constr.GetFractionComplete():P2}", "progress" + costTooltip), GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));
                if (buildRate > 0d)
                {
                    double seconds = constr.GetTimeLeft();
                    GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(seconds, identifier), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                }
                else
                {
                    GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(double.MaxValue, identifier), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                }

                if (!HighLogic.LoadedSceneIsEditor && buildRate > 0d)
                {
                    if (GUILayout.Button(new GUIContent("Warp", $"√ Gain/Loss:\n{SpaceCenterManagement.Instance.GetBudgetDelta(constr.GetTimeLeft()):N0}"), GUILayout.Width(45)))
                    {
                        KCTWarpController.Create(constr);
                    }
                }
                else
                {
                    GUILayout.Space(45);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("     Work rate:", GUILayout.Width(90));
                GUILayout.Label(new GUIContent(constr.workRate.ToString("P0"), $"rate{identifier}¶Daily cost multiplier: {constr.RushMultiplier:P0}"), GetLabelRightAlignStyle(), GUILayout.Width(40));
                
                float newWorkRate = GUILayout.HorizontalSlider(constr.workRate, 0f, 1.5f, GUILayout.Width(150));
                constr.workRate = Mathf.RoundToInt(newWorkRate * 20f) * 0.05f;

                GUILayout.Label("Remaining Cost:", GUILayout.Width(100));
                GUILayout.Label($"√{rCost:N0}", GetLabelRightAlignStyle());

                
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Cost/day:");
            double costday = 0d;
            foreach(var c in ksc.Constructions)
            {
                double br = c.GetBuildRate();
                if (br > 0d)
                {
                    costday += br * 86400d / c.BP * -CurrencyUtils.Funds(c.FacilityType == SpaceCenterFacility.LaunchPad ? TransactionReasonsRP0.StructureConstructionLC : TransactionReasonsRP0.StructureConstruction, -c.cost * c.RushMultiplier);
                }
            }
            GUILayout.Label($"√{costday:N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Remaining cost of all constructions:");
            GUILayout.Label($"√{totalCost:N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();
        }

        private static void RenderTechList()
        {
            _accumulatedTimeBefore = 0d;

            PersistentObservableList<ResearchProject> techList = SpaceCenterManagement.Instance.TechList;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:");
            GUILayout.Label("Progress:", GUILayout.Width(_width1 / 2));
            GUILayout.Label(KCTSettings.Instance.UseDates ? "Completes:" : "Time Left:", GUILayout.Width(_width1));
            GUILayout.Space(70);
            GUILayout.EndHorizontal();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(350));

            if (techList.Count == 0)
                GUILayout.Label("No tech nodes are being researched!\nBegin research by unlocking tech in the R&D building.");
            bool forceRecheck = false;
            int cancelID = -1;
            for (int i = 0; i < techList.Count; i++)
            {
                ResearchProject t = techList[i];
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("X", GUILayout.Width(_butW)))
                {
                    forceRecheck = true;
                    cancelID = i;
                    DialogGUIBase[] options = new DialogGUIBase[2];
                    options[0] = new DialogGUIButton("Yes", () => { CancelTechNode(cancelID); });
                    options[1] = new DialogGUIButton("No", () => { });
                    MultiOptionDialog diag = new MultiOptionDialog("cancelNodePopup", $"Are you sure you want to stop researching {t.techName}?\n\nThis will also cancel any dependent techs."
                        + (Crew.CrewHandler.Instance?.GetTrainingCoursesForTech(t.techID) ?? string.Empty), "Cancel Node?", null, 300, options);
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
                }

                // Can move up if item above is not a parent.
                List<string> parentList = Database.TechNameToParents[t.techID];
                bool canMoveUp = i > 0 && (parentList == null || !parentList.Contains(techList[i - 1].techID));

                // Can move down if item below is not a child.
                List<string> nextParentList = i < techList.Count - 1 ? Database.TechNameToParents[techList[i + 1].techID] : null;
                bool canMoveDown = nextParentList == null || !nextParentList.Contains(t.techID);

                if (i > 0 && t.BuildRate != techList[0].BuildRate)
                {
                    GUI.enabled = canMoveUp;
                    if (i > 0 && GUILayout.Button("^", GUILayout.Width(_butW)))
                    {
                        techList.RemoveAt(i);
                        if (GameSettings.MODIFIER_KEY.GetKey())
                        {
                            // Find furthest postion tech can be moved to.
                            int newLocation = i - 1;
                            while (newLocation >= 0)
                            {
                                if (parentList != null && parentList.Contains(techList[newLocation].techID))
                                    break;
                                --newLocation;
                            }
                            ++newLocation;

                            techList.Insert(newLocation, t);
                        }
                        else
                        {
                            techList.Insert(i - 1, t);
                        }
                        forceRecheck = true;
                    }
                    GUI.enabled = true;
                }

                if ((i == 0 && t.BuildRate != techList[techList.Count - 1].BuildRate) || t.BuildRate != techList[techList.Count - 1].BuildRate)
                {
                    GUI.enabled = canMoveDown;
                    if (i < techList.Count - 1 && GUILayout.Button("v", GUILayout.Width(_butW)))
                    {
                        techList.RemoveAt(i);
                        if (GameSettings.MODIFIER_KEY.GetKey())
                        {
                            // Find furthest postion tech can be moved to.
                            int newLocation = i + 1;
                            while (newLocation < techList.Count)
                            {
                                nextParentList = Database.TechNameToParents[techList[newLocation].techID];
                                if (nextParentList != null && nextParentList.Contains(t.techID))
                                    break;
                                ++newLocation;
                            }

                            techList.Insert(newLocation, t);
                        }
                        else
                        {
                            techList.Insert(i + 1, t);
                        }
                        forceRecheck = true;
                    }
                    GUI.enabled = true;
                }

                if (forceRecheck)
                {
                    forceRecheck = false;
                    SpaceCenterManagement.Instance.UpdateTechTimes();
                }

                string blockingPrereq = t.GetBlockingTech();

                DrawTypeIcon(t);
                GUILayout.Label(t.techName);
                GUILayout.Label($"{t.GetFractionComplete():P2}", GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));
                if (t.BuildRate > 0)
                {
                    DrawYearBasedMult(t, 0);
                    if (blockingPrereq == null)
                    {
                        double seconds = t.TimeLeft;
                        GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(seconds, t.GetItemName()), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                        _accumulatedTimeBefore += seconds;
                    }
                    else
                        GUILayout.Label("Waiting for PreReq", GUILayout.Width(_width1));
                }
                else
                {
                    DrawYearBasedMult(t, _accumulatedTimeBefore);
                    double seconds = t.GetTimeLeftEst(_accumulatedTimeBefore);
                    GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(seconds, t.GetItemName(), _accumulatedTimeBefore, true), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                    _accumulatedTimeBefore += seconds;
                }
                if (t.BuildRate > 0 && blockingPrereq == null)
                {
                    if (!HighLogic.LoadedSceneIsEditor && GUILayout.Button("Warp", GUILayout.Width(45)))
                    {
                        KCTWarpController.Create(t);
                    }
                    else if (HighLogic.LoadedSceneIsEditor)
                        GUILayout.Space(45);
                }
                else
                    GUILayout.Space(45);

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private static int CompareBuildItems(ISpaceCenterProject a, ISpaceCenterProject b)
        {
            return (_timeBeforeItem.ValueOrDefault(a) + _estTimeForItem[a]).CompareTo(_timeBeforeItem.ValueOrDefault(b) + _estTimeForItem[b]);
        }

        private static List<ISpaceCenterProject> _allItems = new List<ISpaceCenterProject>();
        private static Dictionary<ISpaceCenterProject, double> _timeBeforeItem = new Dictionary<ISpaceCenterProject, double>();
        private static Dictionary<ISpaceCenterProject, double> _estTimeForItem = new Dictionary<ISpaceCenterProject, double>();
        private static void RenderCombinedList()
        {
            double accTime;
            foreach (var k in SpaceCenterManagement.Instance.KSCs)
            {
                foreach (var l in k.LaunchComplexes)
                {
                    accTime = l.GetBlockingProjectTimeLeft();
                    l.accumEffic = l.Efficiency;
                    foreach (var b in l.BuildList)
                    {
                        // FIXME handle multiple rates
                        _timeBeforeItem[b] = accTime;
                        accTime += b.GetTimeLeftEst(accTime, l.accumEffic, out l.accumEffic);
                        _allItems.Add(b);
                    }
                    _allItems.AddRange(l.Recon_Rollout);
                    _allItems.AddRange(l.VesselRepairs);
                }
                accTime = 0d;
                foreach (var c in k.Constructions)
                {
                    _timeBeforeItem[c] = accTime;
                    _allItems.Add(c);
                }
            }
            accTime = 0d;
            foreach (var t in SpaceCenterManagement.Instance.TechList)
            {
                _timeBeforeItem[t] = accTime;
                accTime += t.GetTimeLeftEst(accTime);
                _allItems.Add(t);
            }
            _allItems.AddRange(Crew.CrewHandler.Instance.TrainingCourses);
            
            if (SpaceCenterManagement.Instance.fundTarget.IsValid)
                _allItems.Add(SpaceCenterManagement.Instance.fundTarget);

            if (SpaceCenterManagement.Instance.staffTarget.IsValid)
                _allItems.Add(SpaceCenterManagement.Instance.staffTarget);

            // Precalc times and then sort
            foreach (var b in _allItems)
                _estTimeForItem[b] = b.GetTimeLeftEst(_timeBeforeItem.ValueOrDefault(b));
            _allItems.Sort(CompareBuildItems);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(250));
            GUILayout.Label("Progress:");
            GUILayout.Space(18);
            GUILayout.Label(KCTSettings.Instance.UseDates ? "Completes:" : "Time Left:", GUILayout.Width(_width2));
            GUILayout.EndHorizontal();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(350 - GUI.skin.label.lineHeight * 5));

            for (int i = 0; i < _allItems.Count; i++)
            {
                ISpaceCenterProject t = _allItems[i];
                if (t.IsComplete())
                    continue;

                GUILayout.BeginHorizontal();
                if ((t is HireStaffProject || t is FundTargetProject) &&
                    GUILayout.Button("X", GUILayout.Width(_butW)))
                {
                    (t as HireStaffProject)?.Clear();
                    (t as FundTargetProject)?.Clear();
                }

                DrawTypeIcon(t);
                VesselProject vp;
                if (t is ReconRolloutProject r)
                {
                    if (r.RRType == ReconRolloutProject.RolloutReconType.Reconditioning)
                        GUILayout.Label($"{r.LC.Name}: {r.GetItemName()} {r.launchPadID}");
                    else if ((vp = r.AssociatedVP) != null)
                    {
                        if (r.RRType == ReconRolloutProject.RolloutReconType.Rollout)
                            GUILayout.Label($"{vp.LC.Name}: Rollout {vp.shipName} to {r.launchPadID}");
                        else if(r.RRType == ReconRolloutProject.RolloutReconType.AirlaunchMount || r.RRType == ReconRolloutProject.RolloutReconType.AirlaunchUnmount)
                            GUILayout.Label($"{r.GetItemName()}: {vp.shipName}");
                        else
                            GUILayout.Label($"{vp.LC.Name}: {r.GetItemName()} {vp.shipName}");
                    }
                    else
                        GUILayout.Label(r.GetItemName());
                }
                else if (t is VesselRepairProject)
                {
                    GUILayout.Label(t.GetItemName());
                }
                else if (t is VesselProject b)
                    GUILayout.Label($"{b.LC.Name}: {b.GetItemName()}");
                else if (t is ConstructionProject constr)
                {
                    KCTUtilities.GetConstructionTooltip(constr, i, out string costTooltip, out string identifier);
                    GUILayout.Label(new GUIContent(t.GetItemName(), "name" + costTooltip));
                }
                else if (t is Crew.TrainingCourse course)
                {
                    var sb = StringBuilderCache.Acquire();
                    sb.Append("Astronauts:");
                    foreach (var pcm in course.Students)
                        sb.Append("\n").Append(pcm.displayName);
                    GUILayout.Label(new GUIContent(course.GetItemName(), sb.ToStringAndRelease()));
                }
                else
                    GUILayout.Label(t.GetItemName());

                GUILayout.Label($"{t.GetFractionComplete():P2}", GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));

                double timeBeforeItem = _timeBeforeItem.ValueOrDefault(t);
                if (t is ResearchProject tech)
                    DrawYearBasedMult(tech, timeBeforeItem);
                else
                    GUILayout.Space(18);

                if (t.GetBuildRate() > 0d)
                {
                    if (t is ResearchProject rp && rp.GetBlockingTech() != null)
                    {
                        GUILayout.Label("Waiting for PreReq", GUILayout.Width(_width1));
                    }
                    else
                        GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(t.GetTimeLeft(), "combined" + i), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                }
                else if (t is VesselProject b && !b.LC.IsOperational)
                    GUILayout.Label("(site reconstructing)", GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                else
                    GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(_estTimeForItem[t], "combined" + i, timeBeforeItem, true), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.Label("__________________________________________________");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Storage");
            GUILayout.EndHorizontal();

            _scrollPos2 = GUILayout.BeginScrollView(_scrollPos2, GUILayout.Height(GUI.skin.label.lineHeight * 5));

            int idx = 0;
            foreach (var lc in SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes)
            {
                foreach (var b in lc.Warehouse)
                    RenderWarehouseRow(b, idx++, true);
            }
            if(idx == 0)
                GUILayout.Label("No vessels in storage!");

            GUILayout.EndScrollView();
            _allItems.Clear();
            _timeBeforeItem.Clear();
            _estTimeForItem.Clear();
        }

        private static void DrawYearBasedMult(ResearchProject t, double offset)
        {
            double mult = offset == 0 ? t.YearBasedRateMult : t.CalculateYearBasedRateMult(offset);

            string rateDesc;
            if (mult < 0.5)
            {
                _blobText.normal.textColor = Color.red;
                rateDesc = "Speculative R&D";
            }
            else if (mult < 0.85)
            {
                _blobText.normal.textColor = XKCDColors.Orange;
                rateDesc = "Bleeding edge R&D";
            }
            else if (mult > 1.15)
            {
                _blobText.normal.textColor = Color.green;
                rateDesc = "Catching up with competition";
            }
            else 
            { 
                _blobText.normal.textColor = Color.yellow;
                rateDesc = "State-of-the art R&D";
            }

            string txt = $"{rateDesc}\nResearch rate: {mult:F2}x";
            GUILayout.Label(new GUIContent("•", txt), _blobText, GUILayout.Width(15));
        }

        private static GUIContent GetTypeIcon(ISpaceCenterProject b)
        {
            switch (b.GetProjectType())
            {
                case ProjectType.VAB:
                    return _rocketTexture;

                case ProjectType.SPH:
                    return _planeTexture;

                case ProjectType.Reconditioning:
                    if (b is ReconRolloutProject r)
                    {
                        switch (r.RRType)
                        {
                            case ReconRolloutProject.RolloutReconType.Reconditioning:
                                return _reconTexture;
                            case ReconRolloutProject.RolloutReconType.Recovery:
                                return _recoveryTexture;
                            case ReconRolloutProject.RolloutReconType.Rollback:
                                return _rollbackTexture;
                            case ReconRolloutProject.RolloutReconType.Rollout:
                                return _rolloutTexture;
                        }
                    }
                    return _rocketTexture;

                case ProjectType.AirLaunch:
                    if (b is ReconRolloutProject a && a.RRType == ReconRolloutProject.RolloutReconType.AirlaunchMount)
                        return _airlaunchTexture;
                    return _hangarTexture;

                case ProjectType.KSC:
                    return _constructTexture;

                case ProjectType.TechNode:
                    return _techTexture;

                case ProjectType.VesselRepair:
                    return _repairTexture;
            }

            return _emptyTexture;
        }

        private static void DrawTypeIcon(ISpaceCenterProject b)
        {
            GUILayout.Label(GetTypeIcon(b), GUILayout.ExpandWidth(false));
        }

        private static void RenderBuildList()
        {
            LaunchComplex activeLC = SpaceCenterManagement.EditorShipEditingMode ? SpaceCenterManagement.Instance.EditedVessel.LC : SpaceCenterManagement.Instance.ActiveSC.ActiveLC;

            RenderBuildlistHeader();

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(375));

            if (activeLC.LCType == LaunchComplexType.Pad)
            {
                RenderRepairs();
                RenderRollouts();
            }
            RenderVesselsBeingBuilt(activeLC);
            RenderWarehouse();

            GUILayout.EndScrollView();

            RenderLaunchComplexControls();
            RenderLaunchPadControls();
        }

        private static void RenderBuildlistHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:");
            GUILayout.Label("Progress:", GUILayout.Width(_width1 / 2));
            GUILayout.Label(KCTSettings.Instance.UseDates ? "Completes:" : "Time Left:", GUILayout.Width(_width2));
            GUILayout.EndHorizontal();
        }

        private static void RenderRollouts()
        {
            LaunchComplex activeLC = SpaceCenterManagement.EditorShipEditingMode ? SpaceCenterManagement.Instance.EditedVessel.LC : SpaceCenterManagement.Instance.ActiveSC.ActiveLC;
            foreach (ReconRolloutProject reconditioning in activeLC.Recon_Rollout.FindAll(r => r.RRType == ReconRolloutProject.RolloutReconType.Reconditioning))
            {
                GUILayout.BeginHorizontal();
                double tLeft = reconditioning.GetTimeLeft();
                if (!HighLogic.LoadedSceneIsEditor && reconditioning.GetBuildRate() > 0 &&
                    GUILayout.Button(new GUIContent("Warp To", $"√ Gain/Loss:\n{SpaceCenterManagement.Instance.GetBudgetDelta(tLeft):N0}"), GUILayout.Width((_butW + 4) * 3)))
                {
                    KCTWarpController.Create(reconditioning);
                }
                DrawTypeIcon(reconditioning);
                GUILayout.Label($"Reconditioning: {reconditioning.launchPadID}");
                GUILayout.Label($"{reconditioning.GetFractionComplete():P2}", GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));
                GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(tLeft, "recon" + reconditioning.launchPadID), GetLabelRightAlignStyle(), GUILayout.Width(_width2));

                GUILayout.EndHorizontal();
            }
        }

        private static void RenderRepairs()
        {
            LaunchComplex activeLC = SpaceCenterManagement.EditorShipEditingMode ? SpaceCenterManagement.Instance.EditedVessel.LC : SpaceCenterManagement.Instance.ActiveSC.ActiveLC;
            foreach (VesselRepairProject repair in activeLC.VesselRepairs)
            {
                GUILayout.BeginHorizontal();
                double tLeft = repair.GetTimeLeft();
                if (!HighLogic.LoadedSceneIsEditor && repair.GetBuildRate() > 0 &&
                    GUILayout.Button(new GUIContent("Warp To", $"√ Gain/Loss:\n{SpaceCenterManagement.Instance.GetBudgetDelta(tLeft):N0}"), GUILayout.Width((_butW + 4) * 3)))
                {
                    KCTWarpController.Create(repair);
                }

                if (GUILayout.Button("X", GUILayout.Width(_butW)))
                {
                    DialogGUIBase[] options = new DialogGUIBase[2];
                    options[0] = new DialogGUIButton("Yes", () => activeLC.VesselRepairs.Remove(repair));
                    options[1] = new DialogGUIButton("No", () => { });
                    MultiOptionDialog diag = new MultiOptionDialog("scrapVesselPopup", $"Are you sure you want to cancel this repair?",
                        "Cancel Repair", null, options: options);
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
                }

                DrawTypeIcon(repair);
                GUILayout.Label($"Repair: {repair.shipName}");
                GUILayout.Label($"{repair.GetFractionComplete():P2}", GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));
                GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(tLeft, "repair" + repair.associatedID), GetLabelRightAlignStyle(), GUILayout.Width(_width2));

                GUILayout.EndHorizontal();
            }
        }

        private static void RenderVesselsBeingBuilt(LaunchComplex lc)
        {
            _accumulatedTimeBefore = 0d;
            lc.accumEffic = lc.Efficiency;
            if (lc.BuildList.Count == 0)
            {
                if (HighLogic.LoadedSceneIsEditor)
                    GUILayout.Label("No vessels integrating!");
                else
                    GUILayout.Label($"No vessels integrating! Go to the {(SpaceCenterManagement.Instance.ActiveSC.ActiveLC.LCType == LaunchComplexType.Pad ? "VAB" : "SPH")} to add more.");
            }
            bool recalc = false;
            for (int i = 0; i < lc.BuildList.Count; i++)
            {
                VesselProject b = lc.BuildList[i];
                if (!b.AllPartsValid)
                    continue;
                GUILayout.BeginHorizontal();

                if (HighLogic.LoadedSceneIsEditor)
                {
                    if (GUILayout.Button("X", GUILayout.Width(_butW)))
                    {
                        _selectedVesselId = b.shipID;
                        DialogGUIBase[] options = new DialogGUIBase[2];
                        options[0] = new DialogGUIButton("Yes", ScrapVessel);
                        options[1] = new DialogGUIButton("No", () => { });
                        MultiOptionDialog diag = new MultiOptionDialog("scrapVesselPopup", $"Are you sure you want to scrap this vessel? You will regain "
                            + CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.VesselPurchase, b.cost, 0f, 0f).GetCostLineOverride(false, false) +".",
                            "Scrap Vessel", null, options: options);
                        PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
                    }
                }
                else
                {
                    if (GUILayout.Button("*", GUILayout.Width(_butW)))
                    {
                        if (_selectedVesselId == b.shipID)
                            GUIStates.ShowBLPlus = !GUIStates.ShowBLPlus;
                        else
                            GUIStates.ShowBLPlus = true;
                        _selectedVesselId = b.shipID;
                    }
                }

                if (i > 0 && GUILayout.Button("^", GUILayout.Width(_butW)))
                {
                    lc.BuildList.RemoveAt(i);
                    lc.BuildList.Insert(GameSettings.MODIFIER_KEY.GetKey() ? 0 : i - 1, b);
                    recalc = true;
                }

                if (i < lc.BuildList.Count - 1 && GUILayout.Button("v", GUILayout.Width(_butW)))
                {
                    recalc = true;
                    lc.BuildList.RemoveAt(i);
                    if (GameSettings.MODIFIER_KEY.GetKey())
                    {
                        lc.BuildList.Add(b);
                    }
                    else
                    {
                        lc.BuildList.Insert(i + 1, b);
                    }
                }

                DrawTypeIcon(b);
                GUILayout.Label(b.shipName);
                GUILayout.Label($"{b.GetFractionComplete():P2}", GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));
                if (b.BuildRate > 0)
                {
                    double seconds = b.GetTimeLeft(out lc.accumEffic);
                    GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(seconds, b.shipID.ToString()), GetLabelRightAlignStyle(), GUILayout.Width(_width2));
                    _accumulatedTimeBefore += seconds;
                }
                else
                {
                    if (_accumulatedTimeBefore == 0d)
                        _accumulatedTimeBefore = lc.GetBlockingProjectTimeLeft();
                    double seconds = b.GetTimeLeftEst(_accumulatedTimeBefore, lc.accumEffic, out lc.accumEffic);
                    GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(seconds, b.shipID.ToString(), _accumulatedTimeBefore, true), GetLabelRightAlignStyle(), GUILayout.Width(_width2));
                    _accumulatedTimeBefore += seconds;
                }
                GUILayout.EndHorizontal();
            }
            if (recalc)
            {
                for (int i = lc.BuildList.Count; i-- > 0;)
                    lc.BuildList[i].UpdateBuildRate();
            }
        }

        private static void RenderWarehouse()
        {
            LaunchComplex activeLC = SpaceCenterManagement.EditorShipEditingMode ? SpaceCenterManagement.Instance.EditedVessel.LC : SpaceCenterManagement.Instance.ActiveSC.ActiveLC;
            bool isPad = activeLC.LCType == LaunchComplexType.Pad;
            GUILayout.Label("__________________________________________________");
            GUILayout.BeginHorizontal();
            GUILayout.Label(isPad ? _rocketTexture : _planeTexture, GUILayout.ExpandWidth(false));
            GUILayout.Label("Storage");
            GUILayout.EndHorizontal();
            if (HighLogic.LoadedSceneIsFlight && 
                (isPad ? KCTUtilities.IsVabRecoveryAvailable(FlightGlobals.ActiveVessel) : KCTUtilities.IsSphRecoveryAvailable(FlightGlobals.ActiveVessel) ) &&
                GUILayout.Button("Recover Active Vessel To Warehouse"))
            {
                if (!KCTUtilities.RecoverActiveVesselToStorage(isPad ? ProjectType.VAB : ProjectType.SPH))
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "vesselRecoverErrorPopup", "Error!", "There was an error while recovering the ship. Sometimes reloading the scene and trying again works. Sometimes a vessel just can't be recovered this way and you must use the stock recover system.", KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), false, HighLogic.UISkin).HideGUIsWhilePopup();
                }
            }
            if (activeLC.Warehouse.Count == 0)
            {
                GUILayout.Label("No vessels in storage!\nThey will be stored here when they are complete.");
            }

            for (int i = 0; i < activeLC.Warehouse.Count; i++)
            {
                VesselProject b = activeLC.Warehouse[i];
                RenderWarehouseRow(b, i, false);
            }
        }

        private static void RenderWarehouseRow(VesselProject b, int listIdx, bool isCombinedList)
        {
            if (!b.AllPartsValid)
                return;

            LaunchComplex vesselLC = b.LC;

            bool isPad = vesselLC != SpaceCenterManagement.Instance.ActiveSC.Hangar;

            string launchSite = b.launchSite;
            if (launchSite == "LaunchPad" && isPad)
            {
                if (b.launchSiteIndex >= 0 && b.launchSiteIndex < b.LC.LaunchPads.Count)
                    launchSite = b.LC.LaunchPads[b.launchSiteIndex].name;
                else
                    launchSite = b.LC.ActiveLPInstance.name;
            }
            ReconRolloutProject rollout = null, rollback = null, recovery = null, padRollout = null;
            string vpID = b.shipID.ToString();
            foreach (var rr in vesselLC.Recon_Rollout)
            {
                if (rr.associatedID == vpID)
                {
                    switch (rr.RRType)
                    {
                        case ReconRolloutProject.RolloutReconType.Recovery: recovery = rr; break;
                        case ReconRolloutProject.RolloutReconType.Rollback: rollback = rr; break;
                        case ReconRolloutProject.RolloutReconType.Rollout: rollout = rr; break;
                        // any other type is wrong.
                    }
                }
                else if (isPad && rr.RRType == ReconRolloutProject.RolloutReconType.Rollout && rr.launchPadID == launchSite)
                    padRollout = rr; // something else is being rollout out to this launchsite.
            }
            ReconRolloutProject airlaunchPrep = !isPad ? vesselLC.Recon_Rollout.FirstOrDefault(r => r.associatedID == vpID) : null;

            ISpaceCenterProject typeIcon = rollout ?? rollback ?? recovery ?? null;
            typeIcon = typeIcon ?? airlaunchPrep;
            typeIcon = typeIcon ?? b;

            VesselPadStatus padStatus = VesselPadStatus.InStorage;
            if (recovery != null)
                padStatus = VesselPadStatus.Recovering;
            if ( isPad && rollback != null)
                padStatus = VesselPadStatus.RollingBack;

            GUIStyle textColor = GUI.skin.label;
            string status = "In Storage";
            if (rollout != null)
            {
                padStatus = VesselPadStatus.RollingOut;
                status = $"Rolling Out to {launchSite}";
                textColor = _yellowText;
                if (rollout.IsComplete())
                {
                    padStatus = VesselPadStatus.RolledOut;
                    status = $"At {launchSite}";
                    textColor = _greenText;
                }
            }
            else if (rollback != null)
            {
                status = $"Rolling Back from {launchSite}";
                textColor = _yellowText;
            }
            else if (recovery != null)
            {
                status = "Recovering";
                textColor = _redText;
            }
            else if (airlaunchPrep != null)
            {
                if (airlaunchPrep.IsComplete())
                {
                    status = "Ready";
                    textColor = _greenText;
                }
                else
                {
                    status = airlaunchPrep.GetItemName();
                    textColor = _yellowText;
                }
            }
            if (!b.LC.IsOperational)
                textColor = _redText;

            GUILayout.BeginHorizontal();
            if (b.LC.IsOperational && !HighLogic.LoadedSceneIsEditor && (padStatus != VesselPadStatus.Recovering))
            {
                if (GUILayout.Button("*", GUILayout.Width(_butW)))
                {
                    if (_selectedVesselId == b.shipID)
                        GUIStates.ShowBLPlus = !GUIStates.ShowBLPlus;
                    else
                        GUIStates.ShowBLPlus = true;
                    _selectedVesselId = b.shipID;
                }
            }
            else
                GUILayout.Space(_butW + 4);

            DrawTypeIcon(typeIcon);
            GUILayout.Label(b.shipName, textColor);
            
            if (!b.LC.IsOperational)
            {
                GUILayout.Label("(site reconstructing)", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
                return;
            }

            GUILayout.Label($"{status}   ", textColor, GUILayout.ExpandWidth(false));
            
            if (recovery != null)
            {
                GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(recovery.GetTimeLeft(), "recovery"+ vpID), GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
            }
            else
            {
                if (isPad)
                {
                    LCLaunchPad foundPad = null;
                    LaunchPadState lpState = LaunchPadState.None;
                    if (rollout == null && rollback == null)
                    {
                        if (isCombinedList)
                        {
                            foundPad = vesselLC.FindFreeLaunchPad();
                            if (foundPad != null)
                                lpState = LaunchPadState.Free;
                            else
                                lpState = vesselLC.GetBestLaunchPadState();
                        }
                        else
                        {
                            lpState = vesselLC.ActiveLPInstance.State;
                            if (padRollout == null && vesselLC.GetReconRollout(ReconRolloutProject.RolloutReconType.None, launchSite) == null)
                            {
                                foundPad = vesselLC.ActiveLPInstance;
                            }
                        }
                    }

                    if (!HighLogic.LoadedSceneIsEditor && lpState > LaunchPadState.Nonoperational) //rollout if the pad isn't busy
                    {
                        List<string> facilityChecks = new List<string>();
                        bool meetsChecks = b.MeetsFacilityRequirements(facilityChecks);

                        GUIStyle btnColor = _greenButton;
                        if (lpState == LaunchPadState.Destroyed)
                            btnColor = _redButton;
                        else if (lpState <  LaunchPadState.Free)
                            btnColor = _yellowButton;
                        else if (!meetsChecks)
                            btnColor = _yellowButton;
                        ReconRolloutProject tmpRollout = new ReconRolloutProject(b, ReconRolloutProject.RolloutReconType.Rollout, vpID, launchSite);
                        if (tmpRollout.cost > 0d)
                            GUILayout.Label($"√{-CurrencyUtils.Funds(TransactionReasonsRP0.RocketRollout, -tmpRollout.cost):N0}");
                        GUIContent rolloutText = listIdx == _mouseOnRolloutButton ? RP0DTUtils.GetColonFormattedTimeWithTooltip(tmpRollout.GetTimeLeft(), "rollout"+ vpID) : new GUIContent("Rollout");
                        if (GUILayout.Button(rolloutText, btnColor, GUILayout.ExpandWidth(false)))
                        {
                            if (foundPad != null && lpState == LaunchPadState.Free)
                            {
                                if (meetsChecks)
                                {
                                    b.launchSiteIndex = vesselLC.LaunchPads.IndexOf(foundPad);
                                    vesselLC.Recon_Rollout.Add(tmpRollout);
                                }
                                else
                                {
                                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchEditorChecksPopup", "Cannot Launch!", "Warning! This vessel did not pass the editor checks! Until you upgrade this launch complex it cannot be launched. Listed below are the failed checks:\n" + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()), "Acknowledged", false, HighLogic.UISkin).HideGUIsWhilePopup();
                                }
                            }
                            else if (lpState == LaunchPadState.Destroyed)
                            {
                                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotRollOutDestroyedPopup", "Cannot Roll out!", "You must repair the launchpad before you can roll out to it!", "Acknowledged", false, HighLogic.UISkin).HideGUIsWhilePopup();
                            }
                            else if (lpState == LaunchPadState.Reconditioning)
                            {
                                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotRollOutReconditioningPopup", "Cannot Roll out!", "You must finish reconditioning at least one pad before you can roll out to it!", "Acknowledged", false, HighLogic.UISkin).HideGUIsWhilePopup();
                            }
                            else if (lpState != LaunchPadState.Free)
                            {
                                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotRollOutBusyPopup", "Cannot Roll out!", isCombinedList ? "All pads are in use by other vessels" : "This launchpad is in use by another vessel.", "Acknowledged", false, HighLogic.UISkin).HideGUIsWhilePopup();
                            }
                        }
                        if (Event.current.type == EventType.Repaint)
                            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                                _mouseOnRolloutButton = listIdx;
                            else if (listIdx == _mouseOnRolloutButton)
                                _mouseOnRolloutButton = -1;
                    }
                    else if (!HighLogic.LoadedSceneIsEditor && rollback == null && rollout != null && !rollout.IsComplete() &&
                             GUILayout.Button(RP0DTUtils.GetColonFormattedTimeWithTooltip(rollout.GetTimeLeft(), "rollout"+ vpID), GUILayout.ExpandWidth(false)))    //swap rollout to rollback
                    {
                        rollout.SwitchDirection();
                    }
                    else if (!HighLogic.LoadedSceneIsEditor && rollback != null && !rollback.IsComplete())
                    {
                        if (rollout == null && padRollout == null)
                        {
                            if (GUILayout.Button(RP0DTUtils.GetColonFormattedTimeWithTooltip(rollback.GetTimeLeft(), "rollback"+ vpID), GUILayout.ExpandWidth(false)))    //switch rollback back to rollout
                                rollback.SwitchDirection();
                        }
                        else
                        {
                            GUILayout.Label(RP0DTUtils.GetColonFormattedTimeWithTooltip(rollback.GetTimeLeft(), "rollback"+ vpID), GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                        }
                    }
                    else if (HighLogic.LoadedScene != GameScenes.TRACKSTATION &&
                             (rollout != null && rollout.IsComplete()))
                    {
                        LCLaunchPad pad = vesselLC.LaunchPads.Find(lp => lp.name == rollout.launchPadID);
                        bool operational = pad != null && !pad.IsDestroyed && pad.isOperational;
                        GUIStyle btnColor = _greenButton;
                        string launchTxt = "Launch";
                        if (!operational)
                        {
                            launchTxt = pad == null ? "No Pad" : "Repairs Required";
                            btnColor = _redButton;
                        }
                        else if (KCTUtilities.ReconditioningActive(vesselLC, rollout.launchPadID))
                        {
                            launchTxt = "Reconditioning";
                            btnColor = _yellowButton;
                        }
                        if (GameSettings.MODIFIER_KEY.GetKey() && GUILayout.Button("Roll Back", GUILayout.ExpandWidth(false)))
                        {
                            rollout.SwitchDirection();
                        }
                        else if (!GameSettings.MODIFIER_KEY.GetKey() && GUILayout.Button(launchTxt, btnColor, GUILayout.ExpandWidth(false)))
                        {
                            if (b.launchSiteIndex >= 0)
                            {
                                vesselLC.SwitchLaunchPad(b.launchSiteIndex);
                            }
                            b.launchSiteIndex = vesselLC.ActiveLaunchPadIndex;

                            List<string> facilityChecks = new List<string>();
                            if (b.MeetsFacilityRequirements(facilityChecks))
                            {
                                if (pad == null)
                                {
                                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchNoPad", "Cannot Launch!", "Somehow this vessel is not associated with a launch pad!", "Acknowledged", false, HighLogic.UISkin).HideGUIsWhilePopup();
                                }
                                else if (!operational)
                                {
                                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchRepairPopup", "Cannot Launch!", "You must repair the launchpad before you can launch a vessel from it!", "Acknowledged", false, HighLogic.UISkin).HideGUIsWhilePopup();
                                }
                                else if (vesselLC.GetReconditioning(launchSite) is ReconRolloutProject recon)
                                {
                                    ScreenMessage message = new ScreenMessage($"Cannot launch while launch pad is being reconditioned. It will be finished in {RP0DTUtils.GetFormattedTime(recon.GetTimeLeft(), 0, false)}", 4f, ScreenMessageStyle.UPPER_CENTER);
                                    ScreenMessages.PostScreenMessage(message);
                                }
                                else
                                {
                                    SpaceCenterManagement.Instance.LaunchedVessel = b;
                                    if (ShipConstruction.FindVesselsLandedAt(HighLogic.CurrentGame.flightState, pad.launchSiteName).Count == 0)
                                    {
                                        GUIStates.ShowBLPlus = false;
                                        if (!b.IsCrewable())
                                            b.Launch();
                                        else
                                        {
                                            GUIStates.ShowBuildList = false;

                                            SpaceCenterManagement.ToolbarControl?.SetFalse();

                                            _centralWindowPosition.height = 1;
                                            AssignInitialCrew();
                                            GUIStates.ShowShipRoster = true;
                                        }
                                    }
                                    else
                                    {
                                        GUIStates.ShowBuildList = false;
                                        GUIStates.ShowClearLaunch = true;
                                    }
                                }
                            }
                            else
                            {
                                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchEditorChecksPopup", "Cannot Launch!", "Warning! This vessel did not pass the editor checks! Until you upgrade this launch complex it cannot be launched. Listed below are the failed checks:\n" + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()), "Acknowledged", false, HighLogic.UISkin).HideGUIsWhilePopup();
                            }
                        }
                    }
                }
                else
                {
                    if (!HighLogic.LoadedSceneIsEditor)
                    {
                        if (airlaunchPrep == null && AirlaunchTechLevel.AnyUnlocked())
                        {
                            var tmpPrep = new ReconRolloutProject(b, ReconRolloutProject.RolloutReconType.AirlaunchMount, vpID);
                            if (tmpPrep.cost > 0d)
                                GUILayout.Label($"√{-CurrencyUtils.Funds(TransactionReasonsRP0.AirLaunchRollout, -tmpPrep.cost):N0}");
                            GUIContent airlaunchText = listIdx == _mouseOnAirlaunchButton ? RP0DTUtils.GetColonFormattedTimeWithTooltip(tmpPrep.GetTimeLeft(), "airlaunch"+ vpID) : new GUIContent("Prep for airlaunch");
                            if (GUILayout.Button(airlaunchText, GUILayout.ExpandWidth(false)))
                            {
                                AirlaunchTechLevel lvl = AirlaunchTechLevel.GetCurrentLevel();
                                if (!lvl.CanLaunchVessel(b, out string failedReason))
                                {
                                    ScreenMessages.PostScreenMessage($"Vessel failed validation: {failedReason}", 6f, ScreenMessageStyle.UPPER_CENTER);
                                }
                                else
                                {
                                    vesselLC.Recon_Rollout.Add(tmpPrep);
                                }
                            }
                            if (Event.current.type == EventType.Repaint)
                                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                                    _mouseOnAirlaunchButton = listIdx;
                                else if (listIdx == _mouseOnAirlaunchButton)
                                    _mouseOnAirlaunchButton = -1;
                        }
                        else if (airlaunchPrep != null)
                        {
                            GUIContent btnText = airlaunchPrep.IsComplete() ? new GUIContent("Unmount") : RP0DTUtils.GetColonFormattedTimeWithTooltip(airlaunchPrep.GetTimeLeft(), "airlaunch"+airlaunchPrep.associatedID);
                            if (GUILayout.Button(btnText, GUILayout.ExpandWidth(false)))
                            {
                                airlaunchPrep.SwitchDirection();
                            }
                        }
                    }

                    string launchBtnText = airlaunchPrep != null ? "Airlaunch" : "Launch";
                    if (HighLogic.LoadedScene != GameScenes.TRACKSTATION && (airlaunchPrep == null || airlaunchPrep.IsComplete()) &&
                        GUILayout.Button(launchBtnText, GUILayout.ExpandWidth(false)))
                    {
                        List<string> facilityChecks = new List<string>();
                        if (b.MeetsFacilityRequirements(facilityChecks))
                        {
                            bool operational = KCTUtilities.IsLaunchFacilityIntact(ProjectType.SPH);
                            if (!operational)
                            {
                                ScreenMessages.PostScreenMessage("You must repair the runway prior to launch!", 4f, ScreenMessageStyle.UPPER_CENTER);
                            }
                            else
                            {
                                GUIStates.ShowBLPlus = false;
                                SpaceCenterManagement.Instance.LaunchedVessel = b;

                                if (ShipConstruction.FindVesselsLandedAt(HighLogic.CurrentGame.flightState, "Runway").Count == 0)
                                {
                                    if (airlaunchPrep != null)
                                    {
                                        GUIStates.ShowBuildList = false;
                                        GUIStates.ShowAirlaunch = true;
                                    }
                                    else if (!b.IsCrewable())
                                    {
                                        b.Launch();
                                    }
                                    else
                                    {
                                        GUIStates.ShowBuildList = false;
                                        SpaceCenterManagement.ToolbarControl?.SetFalse();
                                        _centralWindowPosition.height = 1;
                                        AssignInitialCrew();
                                        GUIStates.ShowShipRoster = true;
                                    }
                                }
                                else
                                {
                                    GUIStates.ShowBuildList = false;
                                    GUIStates.ShowClearLaunch = true;
                                    GUIStates.ShowAirlaunch = airlaunchPrep != null;
                                }
                            }
                        }
                        else
                        {
                            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchEditorChecksPopup", "Cannot Launch!", "Warning! This vessel did not pass the editor checks! Until you upgrade this launch complex (the Hangar) it cannot be launched. Listed below are the failed checks:\n" + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()), "Acknowledged", false, HighLogic.UISkin).HideGUIsWhilePopup();
                        }
                    }
                }
            }

            GUILayout.EndHorizontal();
        }

        private static void RenderLaunchComplexControls()
        {
            LaunchComplex activeLC = SpaceCenterManagement.EditorShipEditingMode ? SpaceCenterManagement.Instance.EditedVessel.LC : SpaceCenterManagement.Instance.ActiveSC.ActiveLC;

            GUILayout.BeginHorizontal();
            // Don't allow switching in edit mode
            int lcCount = SpaceCenterManagement.EditorShipEditingMode ? 1 : SpaceCenterManagement.Instance.ActiveSC.LaunchComplexCount;
            if (lcCount > 1 && GUILayout.Button("<<", GUILayout.ExpandWidth(false)))
            {
                SpaceCenterManagement.Instance.ActiveSC.SwitchToPrevLaunchComplex();
            }
            GUILayout.FlexibleSpace();
            string lcText = $"{activeLC.Name} ({activeLC.SupportedMassAsPrettyText})";
            string lcTooltip = $"Size limit: {activeLC.SupportedSizeAsPrettyText}\nHuman-Rated: {(activeLC.IsHumanRated ? "Yes" : "No")}";
            GUILayout.Label(new GUIContent(lcText, lcTooltip));

            if (GUILayout.Button(new GUIContent("Rename", "Rename Complex"), GUILayout.ExpandWidth(false)))
            {
                _renameType = RenameType.LaunchComplex;
                _newName = activeLC.Name;
                GUIStates.ShowDismantlePad = false;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowDismantleLC = false;
                GUIStates.ShowNewPad = false;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowLCResources = false;
                GUIStates.ShowRename = true;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
                _centralWindowPosition.width = 300;
            }
            bool canModify = activeLC.CanDismantle && !GUIStates.ShowPersonnelWindow;
            const string modifyFailTooltip = "Currently in use! No projects can be underway or\nvessels at pads/airlaunching, though vessels can be in storage.";
            const string staffWindowFailTooltip = "Staff window open";
            if (!HighLogic.LoadedSceneIsEditor && activeLC.LCType == LaunchComplexType.Pad && GUILayout.Button(new GUIContent("Dismantle", canModify ? "Dismantle this launch complex. All stored vessels will be scrapped." : GUIStates.ShowPersonnelWindow ? staffWindowFailTooltip : modifyFailTooltip),
                canModify ? GUI.skin.button : _redButton, GUILayout.ExpandWidth(false)))
            {
                if (canModify)
                {
                    GUIStates.ShowDismantlePad = false;
                    GUIStates.ShowModifyLC = false;
                    GUIStates.ShowDismantleLC = true;
                    GUIStates.ShowNewPad = false;
                    GUIStates.ShowNewLC = false;
                    GUIStates.ShowLCResources = false;
                    GUIStates.ShowRename = false;
                    GUIStates.ShowBuildList = false;
                    GUIStates.ShowBLPlus = false;
                    _centralWindowPosition.width = 300;
                    _centralWindowPosition.height = 1;
                }
                else
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("KCTCantModify", GUIStates.ShowPersonnelWindow ? staffWindowFailTooltip : modifyFailTooltip, "Can't Dismantle", null, new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), () => { })), false, HighLogic.UISkin).HideGUIsWhilePopup();
                }
            }
            GUILayout.FlexibleSpace();
            if (lcCount > 1 && GUILayout.Button(">>", GUILayout.ExpandWidth(false)))
            {
                SpaceCenterManagement.Instance.ActiveSC.SwitchToNextLaunchComplex();
            }
            GUILayout.EndHorizontal();
        }

        private static void RenderLaunchPadControls()
        {
            LaunchComplex activeLC = SpaceCenterManagement.EditorShipEditingMode ? SpaceCenterManagement.Instance.EditedVessel.LC : SpaceCenterManagement.Instance.ActiveSC.ActiveLC;

            GUILayout.BeginHorizontal();
            bool oldRushing = activeLC.IsRushing;
            activeLC.IsRushing = GUILayout.Toggle(activeLC.IsRushing, new GUIContent("Rush",
                $"Enable rush integration.\nRate: {Database.SettingsSC.RushRateMult:N1}x\nSalary cost: {Database.SettingsSC.RushSalaryMult:N1}x{(activeLC.LCType == LaunchComplexType.Pad ? "\nLC will not gain efficiency" : string.Empty)}"));
            if (oldRushing != activeLC.IsRushing)
                KCTUtilities.ChangeEngineers(activeLC, 0); // fire event to recalc salaries.

            LCLaunchPad activePad = activeLC.ActiveLPInstance;
            
            GUILayout.Space(15);

            if (activePad == null)
            {
                // Hangar, no pads to switch
                GUILayout.EndHorizontal();
                return;
            }

            int lpCount = activeLC.LaunchPadCount;
            if (lpCount > 1 && GUILayout.Button("<<", GUILayout.ExpandWidth(false)))
            {
                activeLC.SwitchToPrevLaunchPad();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(new GUIContent(activePad.name, "Uses Launch Complex limits"));

            if (GUILayout.Button(new GUIContent("Rename", "Rename pad"), GUILayout.ExpandWidth(false)))
            {
                _renameType = RenameType.Pad;
                _newName = activePad.name;
                GUIStates.ShowDismantlePad = false;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowDismantleLC = false;
                GUIStates.ShowNewPad = false;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowLCResources = false;
                GUIStates.ShowRename = true;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
            }
            if (GUILayout.Button(new GUIContent("Location", "Choose KerbalKonstructs launch site"), GUILayout.ExpandWidth(false)))
            {
                _launchSites = KCTUtilities.GetLaunchSites(true);
                if (_launchSites.Any())
                {
                    _isSelectingLaunchSiteForVessel = false;
                    GUIStates.ShowLaunchSiteSelector = true;
                    _centralWindowPosition.width = 300;
                }
                else
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("KCTNoLaunchsites", "No launch sites available!", "No Launch Sites", null, new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), () => { })), false, HighLogic.UISkin).HideGUIsWhilePopup();
                }
            }
            if (GUILayout.Button(new GUIContent("New", "Build a new launch pad"), GUILayout.ExpandWidth(false)))
            {
                _newName = $"LaunchPad {(activeLC.LaunchPads.Count + 1)}";
                GUIStates.ShowDismantlePad = false;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowDismantleLC = false;
                GUIStates.ShowNewPad = true;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowLCResources = false;
                GUIStates.ShowRename = false;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
                _centralWindowPosition.width = 300;
                _centralWindowPosition.height = 1;
            }
            if (lpCount > 1 && GUILayout.Button(new GUIContent("Dismantle", "Permanently dismantle the launch pad. Can be used to lower maintenance costs by getting rid of unused pads."), GUILayout.ExpandWidth(false)))
            {
                _centralWindowPosition.height = 1;
                GUIStates.ShowDismantlePad = true;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowDismantleLC = false;
                GUIStates.ShowNewPad = false;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowLCResources = false;
                GUIStates.ShowRename = false;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
            }
            GUILayout.FlexibleSpace();
            if (lpCount > 1 && GUILayout.Button(">>", GUILayout.ExpandWidth(false)))
            {
                activeLC.SwitchToNextLaunchPad();
            }
            GUILayout.EndHorizontal();
        }

        public static void CancelTechNode(int index, bool initialCancel = true)
        {
            if (initialCancel)
            {
                SpaceCenterManagement.TechListIgnoreUpdates = true;
                KCTUtilities.RemoveResearchedPartsFromExperimental();
            }

            if (SpaceCenterManagement.Instance.TechList.Count > index)
            {
                ResearchProject node = SpaceCenterManagement.Instance.TechList[index];
                RP0Debug.Log($"Cancelling tech: {node.techName}");

                // cancel children
                for (int i = 0; i < SpaceCenterManagement.Instance.TechList.Count; i++)
                {
                    List<string> parentList = Database.TechNameToParents[SpaceCenterManagement.Instance.TechList[i].techID];
                    if (parentList.Contains(node.techID))
                    {
                        CancelTechNode(i, false);
                        // recheck list in case multiple levels of children were deleted.
                        i = -1;
                        index = SpaceCenterManagement.Instance.TechList.FindIndex(t => t.techID == node.techID);
                    }
                }

                if (KSPUtils.CurrentGameHasScience())
                {
                    bool valBef = SpaceCenterManagement.IsRefundingScience;
                    SpaceCenterManagement.IsRefundingScience = true;
                    try
                    {
                        ResearchAndDevelopment.Instance.AddScience(node.scienceCost, TransactionReasons.RnDTechResearch);
                    }
                    finally
                    {
                        SpaceCenterManagement.IsRefundingScience = valBef;
                    }
                }
                SpaceCenterManagement.Instance.TechList.RemoveAt(index);
                Crew.CrewHandler.Instance?.OnTechCanceled(node.techID);

                if (initialCancel) // do this only once
                {
                    SpaceCenterManagement.Instance.TechListUpdated();
                    SpaceCenterManagement.TechListIgnoreUpdates = false;
                    KCTUtilities.AddResearchedPartsToExperimental();
                }
            }
        }

        public static void CancelConstruction(int index)
        {
            if (SpaceCenterManagement.Instance.ActiveSC.Constructions.Count > index)
            {
                ConstructionProject item = SpaceCenterManagement.Instance.ActiveSC.Constructions[index];
                RP0Debug.Log($"Cancelling construction: {item.GetItemName()}");
                item.Cancel();
            }
        }

        private static void DrawBLPlusWindow(int windowID)
        {
            Rect parentPos = HighLogic.LoadedSceneIsEditor ? EditorBuildListWindowPosition : BuildListWindowPosition;
            _blPlusPosition.yMin = parentPos.yMin;
            _blPlusPosition.height = 225;
            VesselProject b = KCTUtilities.FindVPByID(SpaceCenterManagement.Instance.ActiveSC.ActiveLC, _selectedVesselId);
            GUILayout.BeginVertical();
            string launchSite = b.launchSite;

            if (launchSite == "LaunchPad")
            {
                if (b.launchSiteIndex >= 0 && b.launchSiteIndex < b.LC.LaunchPads.Count)
                    launchSite = b.LC.LaunchPads[b.launchSiteIndex].name;
                else
                    launchSite = b.LC.ActiveLPInstance.name;
            }
            string vpID = b.shipID.ToString();
            ReconRolloutProject rollout = b.LC.GetReconRollout(ReconRolloutProject.RolloutReconType.Rollout, launchSite);
            ReconRolloutProject rollback = rollout == null ? b.LC.GetReconRollout(ReconRolloutProject.RolloutReconType.Rollback, launchSite) : null;
            bool isRollingOut = rollout != null && rollout.associatedID == vpID;
            bool isRollingBack = rollback != null && rollback.associatedID == vpID;

            // Only allow selecting launch site for planes.
            // Rockets use whatever location is set for their pad.
            if (b.Type == ProjectType.SPH && b.LC.Recon_Rollout.Find(a => a.associatedID == vpID) == null && GUILayout.Button("Select LaunchSite"))
            {
                _launchSites = KCTUtilities.GetLaunchSites(b.Type == ProjectType.VAB);
                if (_launchSites.Any())
                {
                    GUIStates.ShowBLPlus = false;
                    GUIStates.ShowLaunchSiteSelector = true;
                    _centralWindowPosition.width = 300;
                }
                else
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("KCTNoLaunchsites", "No launch sites available to choose from. Try visiting an editor first.", "No Launch Sites", null, new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), () => { })), false, HighLogic.UISkin).HideGUIsWhilePopup();
                }
            }

            if (!isRollingOut && !isRollingBack && GUILayout.Button("Scrap"))
            {
                GUIStates.ShowBLPlus = false;
                DialogGUIBase[] options = new DialogGUIBase[2];
                options[0] = new DialogGUIButton("Yes", ScrapVessel);
                options[1] = new DialogGUIButton("No", () => { });
                MultiOptionDialog diag = new MultiOptionDialog("scrapVesselPopup", $"Are you sure you want to scrap this vessel? You will regain "
                            + CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.VesselPurchase, b.cost, 0f, 0f).GetCostLineOverride(false, false) + ".",
                            "Scrap Vessel", null, options: options);
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
                ResetBLWindow(false);
            }

            if (!isRollingOut && !isRollingBack && GUILayout.Button("Edit"))
            {
                GUIStates.ShowBLPlus = false;
                EditorWindowPosition.height = 1;
                string tempFile = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/Ships/temp.craft";
                b.UpdateNodeAndSave(tempFile);
                SpaceCenterManagement.Instance.EditedVessel = b;
                GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                SpaceCenterManagement.EditorShipEditingMode = true;
                SpaceCenterManagement.Instance.MergingAvailable = b.IsFinished;

                InputLockManager.SetControlLock(ControlTypes.EDITOR_EXIT, "KCTEditExit");
                InputLockManager.SetControlLock(ControlTypes.EDITOR_NEW, "KCTEditNew");
                InputLockManager.SetControlLock(ControlTypes.EDITOR_LAUNCH, "KCTEditLaunch");

                EditorDriver.StartAndLoadVessel(tempFile, b.Type == ProjectType.SPH ? EditorFacility.SPH : EditorFacility.VAB);
            }

            if (GUILayout.Button("Rename"))
            {
                _centralWindowPosition.width = 360;
                _centralWindowPosition.x = (Screen.width - 360) / 2;
                _centralWindowPosition.height = 1;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
                GUIStates.ShowNewPad = false;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowLCResources = false;
                GUIStates.ShowRename = true;
                _newName = b.shipName;
                _renameType = RenameType.Vessel;
            }

            if (GUILayout.Button("Duplicate"))
            {
                KCTUtilities.TryAddVesselToBuildList(b.CreateCopy(), skipPartChecks: true);
            }

            if (GUILayout.Button("Add to Plans"))
            {
                AddVesselToPlansList(b.CreateCopy());
            }

            ReconRolloutProject vpRollout = b.LC.Recon_Rollout.Find(rr => rr.RRType == ReconRolloutProject.RolloutReconType.Rollout && rr.associatedID == vpID);
            if (vpRollout != null && GUILayout.Button("Rollback"))
            {
                vpRollout.SwitchDirection();
                GUIStates.ShowBLPlus = false;
            }

            if (!b.IsFinished && b.BuildRate > 0 && GUILayout.Button(new GUIContent("Warp To", $"√ Gain/Loss:\n{SpaceCenterManagement.Instance.GetBudgetDelta(b.GetTimeLeft()):N0}")))
            {
                KCTWarpController.Create(b);
                GUIStates.ShowBLPlus = false;
            }

            if (!isRollingOut && !isRollingBack && !b.IsFinished && GUILayout.Button("Move to Top"))
            {
                if (_isIntegrationSelected)
                {
                    LaunchComplex lc = b.LC;
                    if (lc.BuildList.Remove(b))
                    {
                        lc.BuildList.Insert(0, b);
                        lc.RecalculateBuildRates();
                    }
                }
            }

            if (GUILayout.Button("Close"))
            {
                GUIStates.ShowBLPlus = false;
            }

            GUILayout.EndVertical();

            float width = _blPlusPosition.width;
            _blPlusPosition.x = parentPos.x - width;
            _blPlusPosition.width = width;
        }

        public static void DrawLaunchSiteChooser(int windowID)
        {
            LaunchComplex activeLC = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;

            GUILayout.BeginVertical();
            _launchSiteScrollView = GUILayout.BeginScrollView(_launchSiteScrollView, GUILayout.Height((float)Math.Min(Screen.height * 0.75, 25 * _launchSites.Count + 10)));

            foreach (string launchsite in _launchSites)
            {
                if (GUILayout.Button(launchsite))
                {
                    if (_isSelectingLaunchSiteForVessel)
                    {
                        //Set the chosen vessel's launch site to the selected site
                        VesselProject vp = KCTUtilities.FindVPByID(null, _selectedVesselId);
                        vp.launchSite = launchsite;
                    }
                    else
                    {
                        activeLC.ActiveLPInstance.launchSiteName = launchsite;
                        _isSelectingLaunchSiteForVessel = true; // reset
                    }
                    GUIStates.ShowLaunchSiteSelector = false;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            CenterWindow(ref _centralWindowPosition);
        }

        private static void ScrapVessel()
        {
            VesselProject b = KCTUtilities.FindVPByID(null, _selectedVesselId);
            if (b == null)
            {
                RP0Debug.Log("Tried to remove a vessel that doesn't exist!");
                return;
            }
            KCTUtilities.ScrapVessel(b);
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
