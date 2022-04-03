using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        public enum VesselPadStatus { InStorage, RollingOut, RolledOut, RollingBack, Recovering };
        public enum RenameType { None, Vessel, Pad, LaunchComplex };

        public static Rect BuildListWindowPosition = new Rect(Screen.width - 400, 40, 400, 1);
        public static Rect EditorBuildListWindowPosition = new Rect(Screen.width - 400, 40, 400, 1);

        private static List<string> _launchSites = new List<string>();
        private static int _mouseOnRolloutButton = -1;
        private static int _mouseOnAirlaunchButton = -1;
        private static bool _isOperationsSelected, _isConstructionSelected, _isResearchSelected, _isCombinedSelected;
        private static Vector2 _launchSiteScrollView;
        private static Guid _selectedVesselId = new Guid();
        private static bool _isSelectingLaunchSiteForVessel = true;

        private static double _accumulatedTimeBefore;

        private static GUIStyle _redText, _yellowText, _greenText, _blobText, _yellowButton, _redButton, _greenButton;
        private static GUIContent _settingsTexture, _planeTexture, _rocketTexture, _techTexture, _constructTexture, 
            _reconTexture, _rolloutTexture, _rollbackTexture, _airlaunchTexture, _recoveryTexture, _hangarTexture;
        private const int _width1 = 120;
        private const int _width2 = 100;
        private const int _butW = 20;

        public static void SelectList(string list)
        {
            BuildListWindowPosition.height = EditorBuildListWindowPosition.height = 1;
            switch (list)
            {
                case "Operations":
                    _isOperationsSelected = !_isOperationsSelected;
                    _isConstructionSelected = false;
                    _isResearchSelected = false;
                    _isCombinedSelected = false;
                    break;
                case "Construction":
                    _isOperationsSelected = false;
                    _isConstructionSelected = !_isConstructionSelected;
                    _isResearchSelected = false;
                    _isCombinedSelected = false;
                    break;
                case "Research":
                    _isOperationsSelected = false;
                    _isConstructionSelected = false;
                    _isResearchSelected = !_isResearchSelected;
                    _isCombinedSelected = false;
                    break;
                case "Combined":
                    _isCombinedSelected = !_isCombinedSelected;
                    _isOperationsSelected = false;
                    _isConstructionSelected = false;
                    _isResearchSelected = false;
                    break;
                default:
                    _isOperationsSelected = _isConstructionSelected = _isResearchSelected = _isCombinedSelected = false;
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
            KCTDebug.Log("InitBuildListVars");
            _redText = new GUIStyle(GUI.skin.label);
            _redText.normal.textColor = Color.red;
            _yellowText = new GUIStyle(GUI.skin.label);
            _yellowText.normal.textColor = Color.yellow;
            _greenText = new GUIStyle(GUI.skin.label);
            _greenText.normal.textColor = Color.green;
            _blobText = new GUIStyle(GUI.skin.label);
            _blobText.fontSize = 30;
            _blobText.fixedHeight = 20;
            _blobText.alignment = TextAnchor.MiddleCenter;

            _yellowButton = new GUIStyle(GUI.skin.button);
            _yellowButton.normal.textColor = Color.yellow;
            _yellowButton.hover.textColor = Color.yellow;
            _yellowButton.active.textColor = Color.yellow;
            _redButton = new GUIStyle(GUI.skin.button);
            _redButton.normal.textColor = Color.red;
            _redButton.hover.textColor = Color.red;
            _redButton.active.textColor = Color.red;

            _greenButton = new GUIStyle(GUI.skin.button);
            _greenButton.normal.textColor = Color.green;
            _greenButton.hover.textColor = Color.green;
            _greenButton.active.textColor = Color.green;

            _airlaunchTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_airlaunch16", false));
            _constructTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_construct16", false));
            _planeTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_flight16", false));
            _hangarTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_hangar16", false));
            _recoveryTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_landing16", false));
            _reconTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_recon16", false));
            _rocketTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_rocket16", false));
            _rollbackTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_rollback16", false));
            _rolloutTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_rollout16", false));
            _settingsTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_settings16", false));
            _techTexture = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/Resources/KCT_tech16", false));
        }

        public static void DrawBuildListWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Next:", _windowSkin.label);
            IKCTBuildItem buildItem = Utilities.GetNextThingToFinish();
            if (buildItem != null)
            {
                string txt = buildItem.GetItemName(), locTxt = "VAB";
                if (buildItem.GetListType() == BuildListVessel.ListType.Reconditioning)
                {
                    ReconRollout reconRoll = buildItem as ReconRollout;
                    if (reconRoll.RRType == ReconRollout.RolloutReconType.Reconditioning)
                    {
                        txt = "Reconditioning";
                        locTxt = reconRoll.LaunchPadID;
                    }
                    else if (reconRoll.RRType == ReconRollout.RolloutReconType.Rollout)
                    {
                        BuildListVessel associated = reconRoll.LC.Warehouse.FirstOrDefault(blv => blv.Id.ToString() == reconRoll.AssociatedID);
                        txt = $"{associated.ShipName} Rollout";
                        locTxt = reconRoll.LaunchPadID;
                    }
                    else if (reconRoll.RRType == ReconRollout.RolloutReconType.Rollback)
                    {
                        BuildListVessel associated = reconRoll.LC.Warehouse.FirstOrDefault(blv => blv.Id.ToString() == reconRoll.AssociatedID);
                        txt = $"{associated.ShipName} Rollback";
                        locTxt = reconRoll.LaunchPadID;
                    }
                    else if (reconRoll.RRType == ReconRollout.RolloutReconType.Recovery)
                    {
                        BuildListVessel associated = reconRoll.LC.Warehouse.FirstOrDefault(blv => blv.Id.ToString() == reconRoll.AssociatedID);
                        txt = $"{associated.ShipName} Recovery";
                        locTxt = associated.LC.Name;
                    }
                    else
                    {
                        locTxt = "Storage";
                    }
                }
                else if (buildItem.GetListType() == BuildListVessel.ListType.AirLaunch)
                {
                    AirlaunchPrep ar = buildItem as AirlaunchPrep;
                    BuildListVessel associated = ar.AssociatedBLV;
                    if (associated != null)
                    {
                        if (ar.Direction == AirlaunchPrep.PrepDirection.Mount)
                            txt = $"{associated.ShipName} Mounting";
                        else
                            txt = $"{associated.ShipName} Unmounting";
                    }
                    else
                        txt = "Airlaunch Operations";

                    locTxt = ar.LC.Name;

                }
                else if (buildItem.GetListType() == BuildListVessel.ListType.VAB || buildItem.GetListType() == BuildListVessel.ListType.SPH)
                {
                    BuildListVessel blv = buildItem as BuildListVessel;
                    locTxt = blv == null || blv.LC == null ? "Vessel" : blv.LC.Name;
                }
                else if (buildItem.GetListType() == BuildListVessel.ListType.TechNode)
                {
                    locTxt = "Tech";
                }
                else if (buildItem.GetListType() == BuildListVessel.ListType.KSC)
                {
                    locTxt = "KSC";
                }

                GUILayout.Label(txt);
                GUILayout.Label(locTxt, _windowSkin.label);
                GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(buildItem.GetTimeLeft(), txt+locTxt+buildItem.GetItemName()));

                if (!HighLogic.LoadedSceneIsEditor && TimeWarp.CurrentRateIndex == 0 && GUILayout.Button(new GUIContent($"Warp to{Environment.NewLine}Complete", $"Salary Cost:\n√{(buildItem.GetTimeLeft() / 86400d * KCTGameStates.GetTotalMaintenanceAndSalaryPerDay()):N0}")))
                {
                    KCTWarpController.Create(buildItem);
                }
                else if (!HighLogic.LoadedSceneIsEditor && TimeWarp.CurrentRateIndex > 0 && GUILayout.Button($"Stop{Environment.NewLine}Warp"))
                {
                    KCTWarpController.Instance?.StopWarp();
                    TimeWarp.SetRate(0, true);  // If the controller doesn't exist, stop warp anyway.
                }

                if (KCTGameStates.Settings.AutoKACAlarms && KACWrapper.APIReady && buildItem.GetTimeLeft() > 30)    //don't check if less than 30 seconds to completion. Might fix errors people are seeing
                {
                    double UT = Utilities.GetUT();
                    if (!Utilities.IsApproximatelyEqual(KCTGameStates.KACAlarmUT - UT, buildItem.GetTimeLeft()))
                    {
                        KCTDebug.Log("KAC Alarm being created!");
                        KCTGameStates.KACAlarmUT = buildItem.GetTimeLeft() + UT;
                        KACWrapper.KACAPI.KACAlarm alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.ID == KCTGameStates.KACAlarmId);
                        if (alarm == null)
                        {
                            alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.Name.StartsWith("RP-1: "));
                        }
                        if (alarm != null)
                        {
                            KCTDebug.Log("Removing existing alarm");
                            KACWrapper.KAC.DeleteAlarm(alarm.ID);
                        }
                        txt = "RP-1: ";
                        if (buildItem.GetListType() == BuildListVessel.ListType.Reconditioning)
                        {
                            ReconRollout reconRoll = buildItem as ReconRollout;
                            if (reconRoll.RRType == ReconRollout.RolloutReconType.Reconditioning)
                            {
                                txt += $"{reconRoll.LaunchPadID} Reconditioning";
                            }
                            else if (reconRoll.RRType == ReconRollout.RolloutReconType.Rollout)
                            {
                                BuildListVessel associated = reconRoll.LC.Warehouse.FirstOrDefault(blv => blv.Id.ToString() == reconRoll.AssociatedID);
                                txt += $"{associated.ShipName} rollout at {reconRoll.LaunchPadID}";
                            }
                            else if (reconRoll.RRType == ReconRollout.RolloutReconType.Rollback)
                            {
                                BuildListVessel associated = reconRoll.LC.Warehouse.FirstOrDefault(blv => blv.Id.ToString() == reconRoll.AssociatedID);
                                txt += $"{associated.ShipName} rollback at {reconRoll.LaunchPadID}";
                            }
                            else
                            {
                                txt += $"{buildItem.GetItemName()} Complete";
                            }
                        }
                        else
                            txt += $"{buildItem.GetItemName()} Complete";
                        KCTGameStates.KACAlarmId = KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Raw, txt, KCTGameStates.KACAlarmUT);
                        KCTDebug.Log($"Alarm created with ID: {KCTGameStates.KACAlarmId}");
                    }
                }
            }
            else
            {
                GUILayout.Label("No Active Projects");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();


            bool operationsSelectedNew = GUILayout.Toggle(_isOperationsSelected, "Operations", GUI.skin.button);
            if (operationsSelectedNew != _isOperationsSelected)
                SelectList("Operations");

            bool constructionSelectedNew = false;
            if (Utilities.CurrentGameIsCareer())
                constructionSelectedNew = GUILayout.Toggle(_isConstructionSelected, "Construction", GUI.skin.button);
            if (constructionSelectedNew != _isConstructionSelected)
                SelectList("Construction");

            bool techSelectedNew = false;
            if (Utilities.CurrentGameHasScience())
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
            //    _LCIndex = KCTGameStates.ActiveKSC.ActiveLaunchComplexID;
            //}
            bool hasIdleEngineers = false;
            // This reimplements FreeEngineers for speed, since we also have to check LCs for idle
            int engCount = KCTGameStates.ActiveKSC.Engineers;
            foreach (var lc in KCTGameStates.ActiveKSC.LaunchComplexes)
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
            if (!hasIdleEngineers && engCount > 0 && KCTGameStates.ActiveKSC.Constructions.Count == 0)
                hasIdleEngineers = true;

            if (GUILayout.Button(new GUIContent("Staff", hasIdleEngineers ? "Some engineers are idle!" : (KCTGameStates.UnassignedPersonnel > 0 ? "Applicants can be hired for free!" : "Hire/fire/reassign staff")),
                hasIdleEngineers ? _yellowButton : (KCTGameStates.UnassignedPersonnel > 0 ? _greenButton : GUI.skin.button)))
            {
                GUIStates.ShowPersonnelWindow = true;
                //GUIStates.ShowBuildList = false;
                //GUIStates.ShowBLPlus = false;
                _LCIndex = KCTGameStates.ActiveKSC.ActiveLaunchComplexIndex;
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

            if (_isOperationsSelected)
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
            _accumulatedTimeBefore = 0;

            KSCItem ksc = KCTGameStates.ActiveKSC;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:");
            GUILayout.Label("Progress:", GUILayout.Width(_width1 / 2));
            GUILayout.Label("Time Left:", GUILayout.Width(_width1));
            GUILayout.Space(70);
            GUILayout.EndHorizontal();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(250));

            if (ksc.Constructions.Count == 0)
                GUILayout.Label("No KSC upgrade projects are currently underway.");

            bool forceRecheck = false;
            int cancelID = -1;
            for (int i = 0; i < ksc.Constructions.Count; i++)
            {
                ConstructionBuildItem constr = ksc.Constructions[i];
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("X", GUILayout.Width(_butW)))
                {
                    InputLockManager.SetControlLock(ControlTypes.KSC_ALL, "KCTPopupLock");

                    forceRecheck = true;
                    cancelID = i;
                    DialogGUIBase[] options = new DialogGUIBase[2];
                    options[0] = new DialogGUIButton("Yes", () => { CancelConstruction(cancelID); });
                    options[1] = new DialogGUIButton("No", RemoveInputLocks);
                    MultiOptionDialog diag = new MultiOptionDialog("cancelConstructionPopup", $"Are you sure you want to stop building {constr.GetItemName()}?\n\nYou have already spent <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1> {constr.SpentCost:N0} funds on this construction ({(constr.SpentCost / constr.Cost):P0} of the total).", "Cancel Construction?", null, 300, options);
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
                }

                double buildRate = constr.GetBuildRate();
                if (i > 0 && buildRate != ksc.Constructions[0].GetBuildRate())
                {
                    if (i > 0 && GUILayout.Button("^", GUILayout.Width(_butW)))
                    {
                        ksc.Constructions.RemoveAt(i);
                        ksc.Constructions.Insert(GameSettings.MODIFIER_KEY.GetKey() ? 0 : i - 1, constr);
                        forceRecheck = true;
                    }
                }

                if (buildRate != ksc.Constructions[ksc.Constructions.Count - 1].GetBuildRate())
                {
                    if (i < ksc.Constructions.Count - 1 && GUILayout.Button("v", GUILayout.Width(_butW)))
                    {
                        ksc.Constructions.RemoveAt(i);
                        ksc.Constructions.Insert(GameSettings.MODIFIER_KEY.GetKey() ? 0 : i + 1, constr);
                        forceRecheck = true;
                    }
                }

                if (forceRecheck)
                {
                    forceRecheck = false;
                    ksc.RecalculateBuildRates(false);
                }
                DrawTypeIcon(constr);
                string identifier = constr.GetItemName() + i;
                string costTooltip = $"{identifier}¶Remaining Cost: √{(constr.Cost - constr.SpentCost):N0}";
                GUILayout.Label(new GUIContent(constr.GetItemName(), "name" + costTooltip));
                GUILayout.Label(new GUIContent($"{constr.GetFractionComplete():P2}", "progress" + costTooltip), GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));
                if (buildRate > 0d)
                {
                    double seconds = constr.GetTimeLeft();
                    GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(seconds, identifier), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                    _accumulatedTimeBefore += seconds;
                }
                else
                {
                    double seconds = constr.GetTimeLeftEst(_accumulatedTimeBefore);
                    GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(seconds, constr.GetItemName()+i, _accumulatedTimeBefore, true), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                    _accumulatedTimeBefore += seconds;
                }
                if (!HighLogic.LoadedSceneIsEditor && buildRate > 0d && GUILayout.Button("Warp", GUILayout.Width(45)))
                {
                    KCTWarpController.Create(constr);
                }
                else if (HighLogic.LoadedSceneIsEditor)
                    GUILayout.Space(45);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Cost/day for Materials:");
            double costday = 0d;
            if (ksc.Constructions.Count > 0)
            {
                var c = ksc.Constructions[0];
                double br = c.GetBuildRate();
                if (br > 0d)
                {
                    costday = br * 86400d / c.BP * c.Cost;
                }
            }
            GUILayout.Label($"√{costday:N0}", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();
        }

        private static void RenderTechList()
        {
            _accumulatedTimeBefore = 0d;

            KCTObservableList<TechItem> techList = KCTGameStates.TechList;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:");
            GUILayout.Label("Progress:", GUILayout.Width(_width1 / 2));
            GUILayout.Label("Time Left:", GUILayout.Width(_width1));
            GUILayout.Space(70);
            GUILayout.EndHorizontal();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(250));

            if (techList.Count == 0)
                GUILayout.Label("No tech nodes are being researched!\nBegin research by unlocking tech in the R&D building.");
            bool forceRecheck = false;
            int cancelID = -1;
            for (int i = 0; i < techList.Count; i++)
            {
                TechItem t = techList[i];
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("X", GUILayout.Width(_butW)))
                {
                    InputLockManager.SetControlLock(ControlTypes.KSC_ALL, "KCTPopupLock");
                    forceRecheck = true;
                    cancelID = i;
                    DialogGUIBase[] options = new DialogGUIBase[2];
                    options[0] = new DialogGUIButton("Yes", () => { CancelTechNode(cancelID); });
                    options[1] = new DialogGUIButton("No", RemoveInputLocks);
                    MultiOptionDialog diag = new MultiOptionDialog("cancelNodePopup", $"Are you sure you want to stop researching {t.TechName}?\n\nThis will also cancel any dependent techs.", "Cancel Node?", null, 300, options);
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
                }

                // Can move up if item above is not a parent.
                List<string> parentList = KerbalConstructionTimeData.techNameToParents[t.TechID];
                bool canMoveUp = i > 0 && (parentList == null || !parentList.Contains(techList[i - 1].TechID));

                // Can move down if item below is not a child.
                List<string> nextParentList = i < techList.Count - 1 ? KerbalConstructionTimeData.techNameToParents[techList[i + 1].TechID] : null;
                bool canMoveDown = nextParentList == null || !nextParentList.Contains(t.TechID);

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
                                if (parentList != null && parentList.Contains(techList[newLocation].TechID))
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
                                nextParentList = KerbalConstructionTimeData.techNameToParents[techList[newLocation].TechID];
                                if (nextParentList != null && nextParentList.Contains(t.TechID))
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
                    KCTGameStates.UpdateTechTimes();
                }

                string blockingPrereq = t.GetBlockingTech(techList);

                DrawTypeIcon(t);
                GUILayout.Label(t.TechName);
                GUILayout.Label($"{t.GetFractionComplete():P2}", GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));
                if (t.BuildRate > 0)
                {
                    DrawYearBasedMult(t, 0);
                    if (blockingPrereq == null)
                    {
                        double seconds = t.TimeLeft;
                        GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(seconds, t.GetItemName()), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                        _accumulatedTimeBefore += seconds;
                    }
                    else
                        GUILayout.Label("Waiting for PreReq", GUILayout.Width(_width1));
                }
                else
                {
                    DrawYearBasedMult(t, _accumulatedTimeBefore);
                    double seconds = t.GetTimeLeftEst(_accumulatedTimeBefore);
                    GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(seconds, t.GetItemName(), _accumulatedTimeBefore, true), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
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

        private static int CompareBuildItems(IKCTBuildItem a, IKCTBuildItem b)
        {
            double offA, offB;
            _timeBeforeItem.TryGetValue(a, out offA);
            _timeBeforeItem.TryGetValue(b, out offB);
            return (offA + a.GetTimeLeftEst(offA)).CompareTo(offB + b.GetTimeLeftEst(offB));
        }

        private static List<IKCTBuildItem> _allItems = new List<IKCTBuildItem>();
        private static Dictionary<IKCTBuildItem, double> _timeBeforeItem = new Dictionary<IKCTBuildItem, double>();
        private static void RenderCombinedList()
        {
            double accTime;
            foreach (var k in KCTGameStates.KSCs)
            {
                foreach (var l in k.LaunchComplexes)
                {
                    if (l.IsOperational)
                    {
                        accTime = 0d;
                        foreach (var b in l.BuildList)
                        {
                            // FIXME handle multiple rates
                            _timeBeforeItem[b] = accTime;
                            accTime += b.GetTimeLeftEst(accTime);
                            _allItems.Add(b);
                        }
                        _allItems.AddRange(l.Recon_Rollout);
                        _allItems.AddRange(l.AirlaunchPrep);
                    }
                }
                accTime = 0d;
                foreach (var c in k.Constructions)
                {
                    _timeBeforeItem[c] = accTime;
                    accTime += c.GetTimeLeftEst(accTime);
                    _allItems.Add(c);
                }
            }
            accTime = 0d;
            foreach (var t in KCTGameStates.TechList)
            {
                _timeBeforeItem[t] = accTime;
                accTime += t.GetTimeLeftEst(accTime);
                _allItems.Add(t);
            }
            _allItems.Sort(CompareBuildItems);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:");
            GUILayout.Label("Progress:", GUILayout.Width(_width1 / 2));
            GUILayout.Label("Time Left:", GUILayout.Width(_width1));
            GUILayout.Space(70);
            GUILayout.EndHorizontal();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(250));

            for (int i = 0; i < _allItems.Count; i++)
            {
                IKCTBuildItem t = _allItems[i];
                if (t.IsComplete())
                    continue;

                GUILayout.BeginHorizontal();
                DrawTypeIcon(t);
                BuildListVessel blv;
                if (t is ReconRollout r)
                {
                    if (r.RRType == ReconRollout.RolloutReconType.Reconditioning)
                        GUILayout.Label($"{r.LC.Name}: {r.GetItemName()} {r.LaunchPadID}");
                    else if ((blv = r.AssociatedBLV) != null)
                    {
                        if (r.RRType == ReconRollout.RolloutReconType.Rollout)
                            GUILayout.Label($"{blv.LC.Name}: Rollout {blv.ShipName} to {r.LaunchPadID}");
                        else
                            GUILayout.Label($"{blv.LC.Name}: {r.GetItemName()} {blv.ShipName}");
                    }
                    else
                        GUILayout.Label(r.GetItemName());
                }
                else if (t is AirlaunchPrep a && (blv = a.AssociatedBLV) != null)
                    GUILayout.Label($"{a.GetItemName()}: {blv.ShipName}");
                else if (t is BuildListVessel b)
                    GUILayout.Label($"{b.LC.Name}: {b.GetItemName()}");
                else
                    GUILayout.Label(t.GetItemName());

                GUILayout.Label($"{t.GetFractionComplete():P2}", GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));

                double timeBeforeItem;
                _timeBeforeItem.TryGetValue(t, out timeBeforeItem);
                if (t is TechItem tech)
                    DrawYearBasedMult(tech, timeBeforeItem);
                else
                    GUILayout.Space(18);

                if (t.GetBuildRate() > 0d)
                    GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(t.GetTimeLeft(), "comgbined"+i), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                else
                    GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(t.GetTimeLeftEst(timeBeforeItem), "combined"+i, timeBeforeItem, true), GetLabelRightAlignStyle(), GUILayout.Width(_width1));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            _allItems.Clear();
            _timeBeforeItem.Clear();
        }

        private static void DrawYearBasedMult(TechItem t, double offset)
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

        private static GUIContent GetTypeIcon(IKCTBuildItem b)
        {
            switch (b.GetListType())
            {
                case BuildListVessel.ListType.VAB:
                    return _rocketTexture;

                case BuildListVessel.ListType.SPH:
                    return _planeTexture;

                case BuildListVessel.ListType.Reconditioning:
                    if (b is ReconRollout r)
                    {
                        switch (r.RRType)
                        {
                            case ReconRollout.RolloutReconType.Reconditioning:
                                return _reconTexture;
                            case ReconRollout.RolloutReconType.Recovery:
                                return _recoveryTexture;
                            case ReconRollout.RolloutReconType.Rollback:
                                return _rollbackTexture;
                            case ReconRollout.RolloutReconType.Rollout:
                                return _rolloutTexture;
                        }
                    }
                    return _rocketTexture;

                case BuildListVessel.ListType.AirLaunch:
                    if (b is AirlaunchPrep a && a.Direction == AirlaunchPrep.PrepDirection.Mount)
                        return _airlaunchTexture;
                    return _hangarTexture;

                case BuildListVessel.ListType.KSC:
                    return _constructTexture;

                case BuildListVessel.ListType.TechNode:
                    return _techTexture;
            }

            return _constructTexture;
        }

        private static void DrawTypeIcon(IKCTBuildItem b)
        {
            GUILayout.Label(GetTypeIcon(b), GUILayout.ExpandWidth(false));
        }

        private static void RenderBuildList()
        {
            LCItem activeLC = KCTGameStates.EditorShipEditingMode ? KCTGameStates.EditedVessel.LC : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

            RenderBuildlistHeader();
            if (activeLC.IsPad)
                RenderRollouts();

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(275));
            {
                RenderVesselsBeingBuilt(activeLC.BuildList);
                RenderWarehouse();
            }
            GUILayout.EndScrollView();

            RenderLaunchComplexControls();
            RenderLaunchPadControls();
        }

        private static void RenderBuildlistHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:");
            GUILayout.Label("Progress:", GUILayout.Width(_width1 / 2));
            GUILayout.Label("Time Left:", GUILayout.Width(_width2));
            GUILayout.EndHorizontal();
        }

        private static void RenderRollouts()
        {
            LCItem activeLC = KCTGameStates.EditorShipEditingMode ? KCTGameStates.EditedVessel.LC : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            foreach (ReconRollout reconditioning in activeLC.Recon_Rollout.FindAll(r => r.RRType == ReconRollout.RolloutReconType.Reconditioning))
            {
                GUILayout.BeginHorizontal();
                if (!HighLogic.LoadedSceneIsEditor && GUILayout.Button(new GUIContent("Warp To", $"Salary Cost: {(reconditioning.GetTimeLeft() / 86400d * KCTGameStates.GetTotalMaintenanceAndSalaryPerDay()):N0}"), GUILayout.Width((_butW + 4) * 3)))
                {
                    KCTWarpController.Create(reconditioning);
                }
                DrawTypeIcon(reconditioning);
                GUILayout.Label($"Reconditioning: {reconditioning.LaunchPadID}");
                GUILayout.Label($"{reconditioning.GetFractionComplete():P2}", GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));
                GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(reconditioning.GetTimeLeft(), "recon"+reconditioning.LaunchPadID), GetLabelRightAlignStyle(), GUILayout.Width(_width2));

                GUILayout.EndHorizontal();
            }
        }

        private static void RenderVesselsBeingBuilt(List<BuildListVessel> buildList)
        {
            _accumulatedTimeBefore = 0d;
            if (buildList.Count == 0)
            {
                if (HighLogic.LoadedSceneIsEditor)
                    GUILayout.Label("No vessels under construction!");
                else
                    GUILayout.Label($"No vessels under construction! Go to the {(KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.IsPad ? "VAB" : "SPH")} to build more.");
            }
            bool recalc = false;
            for (int i = 0; i < buildList.Count; i++)
            {
                BuildListVessel b = buildList[i];
                if (!b.AllPartsValid)
                    continue;
                GUILayout.BeginHorizontal();

                if (HighLogic.LoadedSceneIsEditor)
                {
                    if (GUILayout.Button("X", GUILayout.Width(_butW)))
                    {
                        InputLockManager.SetControlLock(ControlTypes.EDITOR_SOFT_LOCK, "KCTPopupLock");
                        _selectedVesselId = b.Id;
                        DialogGUIBase[] options = new DialogGUIBase[2];
                        options[0] = new DialogGUIButton("Yes", ScrapVessel);
                        options[1] = new DialogGUIButton("No", RemoveInputLocks);
                        MultiOptionDialog diag = new MultiOptionDialog("scrapVesselPopup", $"Are you sure you want to scrap this vessel? You will regain <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>{b.Cost}.", "Scrap Vessel", null, options: options);
                        PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
                    }
                }
                else
                {
                    if (GUILayout.Button("*", GUILayout.Width(_butW)))
                    {
                        if (_selectedVesselId == b.Id)
                            GUIStates.ShowBLPlus = !GUIStates.ShowBLPlus;
                        else
                            GUIStates.ShowBLPlus = true;
                        _selectedVesselId = b.Id;
                    }
                }

                if (i > 0 && GUILayout.Button("^", GUILayout.Width(_butW)))
                {
                    buildList.RemoveAt(i);
                    buildList.Insert(GameSettings.MODIFIER_KEY.GetKey() ? 0 : i - 1, b);
                    recalc = true;
                }

                if (i < buildList.Count - 1 && GUILayout.Button("v", GUILayout.Width(_butW)))
                {
                    recalc = true;
                    buildList.RemoveAt(i);
                    if (GameSettings.MODIFIER_KEY.GetKey())
                    {
                        buildList.Add(b);
                    }
                    else
                    {
                        buildList.Insert(i + 1, b);
                    }
                }

                DrawTypeIcon(b);
                GUILayout.Label(b.ShipName);
                GUILayout.Label($"{b.GetFractionComplete():P2}", GetLabelRightAlignStyle(), GUILayout.Width(_width1 / 2));
                if (b.BuildRate > 0)
                {
                    double seconds = b.TimeLeft;
                    GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(seconds, b.Id.ToString()), GetLabelRightAlignStyle(), GUILayout.Width(_width2));
                    _accumulatedTimeBefore += seconds; // FIXME what to do with multiple lines? Min() I guess?
                }
                else
                {
                    double seconds = b.GetTimeLeftEst(_accumulatedTimeBefore);
                    GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(seconds, b.Id.ToString(), _accumulatedTimeBefore, true), GetLabelRightAlignStyle(), GUILayout.Width(_width2));
                    _accumulatedTimeBefore += seconds;
                }
                GUILayout.EndHorizontal();
            }
            if (recalc)
            {
                for (int i = buildList.Count; i-- > 0;)
                    buildList[i].UpdateBuildRate();
            }
        }

        private static void RenderWarehouse()
        {
            LCItem activeLC = KCTGameStates.EditorShipEditingMode ? KCTGameStates.EditedVessel.LC : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            bool isPad = activeLC.IsPad;
            List<BuildListVessel> buildList = activeLC.Warehouse;
            GUILayout.Label("__________________________________________________");
            GUILayout.BeginHorizontal();
            GUILayout.Label(isPad ? _rocketTexture : _planeTexture, GUILayout.ExpandWidth(false));
            GUILayout.Label("Storage");
            GUILayout.EndHorizontal();
            if (HighLogic.LoadedSceneIsFlight && 
                (isPad ? Utilities.IsVabRecoveryAvailable(FlightGlobals.ActiveVessel) : Utilities.IsSphRecoveryAvailable(FlightGlobals.ActiveVessel) ) &&
                GUILayout.Button("Recover Active Vessel To Warehouse"))
            {
                if (!Utilities.RecoverActiveVesselToStorage(isPad ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH))
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "vesselRecoverErrorPopup", "Error!", "There was an error while recovering the ship. Sometimes reloading the scene and trying again works. Sometimes a vessel just can't be recovered this way and you must use the stock recover system.", "OK", false, HighLogic.UISkin);
                }
            }
            if (buildList.Count == 0)
            {
                GUILayout.Label("No vessels in storage!\nThey will be stored here when they are complete.");
            }

            for (int i = 0; i < buildList.Count; i++)
            {
                BuildListVessel b = buildList[i];
                RenderWarehouseRow(b, i);
            }
        }

        private static void RenderWarehouseRow(BuildListVessel b, int listIdx)
        {
            if (!b.AllPartsValid)
                return;

            LCItem activeLC = b.LC;

            bool isPad = activeLC != KCTGameStates.ActiveKSC.Hangar;

            string launchSite = b.LaunchSite;
            if (launchSite == "LaunchPad" && isPad)
            {
                if (b.LaunchSiteIndex >= 0)
                    launchSite = b.LC.LaunchPads[b.LaunchSiteIndex].name;
                else
                    launchSite = b.LC.ActiveLPInstance.name;
            }
            ReconRollout rollout = isPad ? activeLC.GetReconRollout(ReconRollout.RolloutReconType.Rollout, launchSite) : null;
            ReconRollout rollback = isPad ? activeLC.Recon_Rollout.FirstOrDefault(r => r.AssociatedID == b.Id.ToString() && r.RRType == ReconRollout.RolloutReconType.Rollback) : null;
            ReconRollout recovery = activeLC.Recon_Rollout.FirstOrDefault(r => r.AssociatedID == b.Id.ToString() && r.RRType == ReconRollout.RolloutReconType.Recovery);
            AirlaunchPrep airlaunchPrep = !isPad ? activeLC.AirlaunchPrep.FirstOrDefault(r => r.AssociatedID == b.Id.ToString()) : null;

            IKCTBuildItem typeIcon = rollout ?? rollback ?? recovery ?? null;
            typeIcon = typeIcon ?? airlaunchPrep;
            typeIcon = typeIcon ?? b;

            VesselPadStatus padStatus = VesselPadStatus.InStorage;
            if ( isPad && rollback != null)
                padStatus = VesselPadStatus.RollingBack;
            if (recovery != null)
                padStatus = VesselPadStatus.Recovering;

            GUIStyle textColor = GUI.skin.label;
            string status = "In Storage";
            if (rollout != null && rollout.AssociatedID == b.Id.ToString())
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

            GUILayout.BeginHorizontal();
            if (!HighLogic.LoadedSceneIsEditor && (padStatus == VesselPadStatus.InStorage || padStatus == VesselPadStatus.RolledOut))
            {
                if (GUILayout.Button("*", GUILayout.Width(_butW)))
                {
                    if (_selectedVesselId == b.Id)
                        GUIStates.ShowBLPlus = !GUIStates.ShowBLPlus;
                    else
                        GUIStates.ShowBLPlus = true;
                    _selectedVesselId = b.Id;
                }
            }
            else
                GUILayout.Space(_butW + 4);

            DrawTypeIcon(typeIcon);
            GUILayout.Label(b.ShipName, textColor);
            GUILayout.Label($"{status}   ", textColor, GUILayout.ExpandWidth(false));
            if (recovery != null)
            {
                GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(recovery.GetTimeLeft(), "recovery"+b.Id.ToString()), GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
            }
            else
            {
                if (isPad)
                {
                    bool siteHasActiveRolloutOrRollback = rollout != null || activeLC.GetReconRollout(ReconRollout.RolloutReconType.Rollback, launchSite) != null;
                    if (!HighLogic.LoadedSceneIsEditor && !siteHasActiveRolloutOrRollback) //rollout if the pad isn't busy
                    {
                        bool hasRecond = false;
                        List<string> facilityChecks = b.MeetsFacilityRequirements(false);
                        GUIStyle btnColor = _greenButton;
                        if (activeLC.ActiveLPInstance.IsDestroyed)
                            btnColor = _redButton;
                        else if (hasRecond = activeLC.GetReconditioning(activeLC.ActiveLPInstance.name) != null)
                            btnColor = _yellowButton;
                        else if (facilityChecks.Count != 0)
                            btnColor = _yellowButton;
                        ReconRollout tmpRollout = new ReconRollout(b, ReconRollout.RolloutReconType.Rollout, b.Id.ToString(), launchSite);
                        if (tmpRollout.Cost > 0d)
                            GUILayout.Label("√" + tmpRollout.Cost.ToString("N0"));
                        GUIContent rolloutText = listIdx == _mouseOnRolloutButton ? Utilities.GetColonFormattedTimeWithTooltip(tmpRollout.GetTimeLeft(), "rollout"+b.Id.ToString()) : new GUIContent("Rollout");
                        if (GUILayout.Button(rolloutText, btnColor, GUILayout.ExpandWidth(false)))
                        {
                            if (hasRecond)
                            {
                                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotRollOutReconditioningPopup", "Cannot Roll out!", "You must finish reconditioning the launchpad before you can roll out to it!", "Acknowledged", false, HighLogic.UISkin);
                            }
                            else
                            {
                                if (facilityChecks.Count == 0)
                                {
                                    if (!activeLC.ActiveLPInstance.IsDestroyed)
                                    {
                                        b.LaunchSiteIndex = activeLC.ActiveLaunchPadIndex;

                                        if (rollout != null)
                                        {
                                            rollout.SwapRolloutType();
                                        }
                                        // tmpRollout.launchPadID = KCT_GameStates.ActiveKSC.ActiveLaunchComplexInstance.ActiveLPInstance.name;
                                        activeLC.Recon_Rollout.Add(tmpRollout);
                                    }
                                    else
                                    {
                                        PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchRepairPopup", "Cannot Launch!", "You must repair the launchpad before you can launch a vessel from it!", "Acknowledged", false, HighLogic.UISkin);
                                    }
                                }
                                else
                                {
                                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchEditorChecksPopup", "Cannot Launch!", "Warning! This vessel did not pass the editor checks! Until you upgrade this launch complex it cannot be launched. Listed below are the failed checks:\n" + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()), "Acknowledged", false, HighLogic.UISkin);
                                }
                            }
                        }
                        if (Event.current.type == EventType.Repaint)
                            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                                _mouseOnRolloutButton = listIdx;
                            else if (listIdx == _mouseOnRolloutButton)
                                _mouseOnRolloutButton = -1;
                    }
                    else if (!HighLogic.LoadedSceneIsEditor && rollback == null &&
                             rollout != null && b.Id.ToString() == rollout.AssociatedID && !rollout.IsComplete() &&
                             GUILayout.Button(Utilities.GetColonFormattedTimeWithTooltip(rollout.GetTimeLeft(), "rollout"+b.Id.ToString()), GUILayout.ExpandWidth(false)))    //swap rollout to rollback
                    {
                        rollout.SwapRolloutType();
                    }
                    else if (!HighLogic.LoadedSceneIsEditor && rollback != null && !rollback.IsComplete())
                    {
                        if (rollout == null)
                        {
                            if (GUILayout.Button(Utilities.GetColonFormattedTimeWithTooltip(rollback.GetTimeLeft(), "rollback"+b.Id.ToString()), GUILayout.ExpandWidth(false)))    //switch rollback back to rollout
                                rollback.SwapRolloutType();
                        }
                        else
                        {
                            GUILayout.Label(Utilities.GetColonFormattedTimeWithTooltip(rollback.GetTimeLeft(), "rollback"+b.Id.ToString()), GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                        }
                    }
                    else if (HighLogic.LoadedScene != GameScenes.TRACKSTATION &&
                             (rollout != null && b.Id.ToString() == rollout.AssociatedID && rollout.IsComplete()))
                    {
                        KCT_LaunchPad pad = activeLC.LaunchPads.Find(lp => lp.name == launchSite);
                        bool operational = pad != null ? !pad.IsDestroyed : !activeLC.ActiveLPInstance.IsDestroyed;
                        GUIStyle btnColor = _greenButton;
                        string launchTxt = "Launch";
                        if (!operational)
                        {
                            launchTxt = "Repairs Required";
                            btnColor = _redButton;
                        }
                        else if (Utilities.ReconditioningActive(null, launchSite))
                        {
                            launchTxt = "Reconditioning";
                            btnColor = _yellowButton;
                        }
                        if (GameSettings.MODIFIER_KEY.GetKey() && GUILayout.Button("Roll Back", GUILayout.ExpandWidth(false)))
                        {
                            rollout.SwapRolloutType();
                        }
                        else if (!GameSettings.MODIFIER_KEY.GetKey() && GUILayout.Button(launchTxt, btnColor, GUILayout.ExpandWidth(false)))
                        {
                            if (b.LaunchSiteIndex >= 0)
                            {
                                activeLC.SwitchLaunchPad(b.LaunchSiteIndex);
                            }
                            b.LaunchSiteIndex = activeLC.ActiveLaunchPadIndex;

                            List<string> facilityChecks = b.MeetsFacilityRequirements(false);
                            if (facilityChecks.Count == 0)
                            {
                                if (!operational)
                                {
                                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchRepairPopup", "Cannot Launch!", "You must repair the launchpad before you can launch a vessel from it!", "Acknowledged", false, HighLogic.UISkin);
                                }
                                else if (Utilities.ReconditioningActive(null, launchSite))
                                {
                                    ScreenMessage message = new ScreenMessage($"Cannot launch while launch pad is being reconditioned. It will be finished in {Utilities.GetFormattedTime(activeLC.GetReconditioning(launchSite).GetTimeLeft(), 0, false)}", 4f, ScreenMessageStyle.UPPER_CENTER);
                                    ScreenMessages.PostScreenMessage(message);
                                }
                                else
                                {
                                    KCTGameStates.LaunchedVessel = b;
                                    KCTGameStates.LaunchedVessel.LCID = KCTGameStates.LaunchedVessel.LC.ID; // clear LC and force refind later.
                                    if (ShipConstruction.FindVesselsLandedAt(HighLogic.CurrentGame.flightState, b.LaunchSite).Count == 0)
                                    {
                                        GUIStates.ShowBLPlus = false;
                                        if (!IsCrewable(b.ExtractedParts))
                                            b.Launch();
                                        else
                                        {
                                            GUIStates.ShowBuildList = false;

                                            KCTGameStates.ToolbarControl?.SetFalse();

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
                                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchEditorChecksPopup", "Cannot Launch!", "Warning! This vessel did not pass the editor checks! Until you upgrade this launch complex it cannot be launched. Listed below are the failed checks:\n" + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()), "Acknowledged", false, HighLogic.UISkin);
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
                            var tmpPrep = new AirlaunchPrep(b, b.Id.ToString());
                            if (tmpPrep.Cost > 0d)
                                GUILayout.Label("√" + tmpPrep.Cost.ToString("N0"));
                            GUIContent airlaunchText = listIdx == _mouseOnAirlaunchButton ? Utilities.GetColonFormattedTimeWithTooltip(tmpPrep.GetTimeLeft(), "airlaunch"+b.Id.ToString()) : new GUIContent("Prep for airlaunch");
                            if (GUILayout.Button(airlaunchText, GUILayout.ExpandWidth(false)))
                            {
                                AirlaunchTechLevel lvl = AirlaunchTechLevel.GetCurrentLevel();
                                if (!lvl.CanLaunchVessel(b, out string failedReason))
                                {
                                    ScreenMessages.PostScreenMessage($"Vessel failed validation: {failedReason}", 6f, ScreenMessageStyle.UPPER_CENTER);
                                }
                                else
                                {
                                    activeLC.AirlaunchPrep.Add(tmpPrep);
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
                            GUIContent btnText = airlaunchPrep.IsComplete() ? new GUIContent("Unmount") : Utilities.GetColonFormattedTimeWithTooltip(airlaunchPrep.GetTimeLeft(), "airlaunch"+airlaunchPrep.AssociatedID);
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
                        List<string> facilityChecks = b.MeetsFacilityRequirements(false);
                        if (facilityChecks.Count == 0)
                        {
                            bool operational = Utilities.IsLaunchFacilityIntact(BuildListVessel.ListType.SPH);
                            if (!operational)
                            {
                                ScreenMessages.PostScreenMessage("You must repair the runway prior to launch!", 4f, ScreenMessageStyle.UPPER_CENTER);
                            }
                            else
                            {
                                GUIStates.ShowBLPlus = false;
                                KCTGameStates.LaunchedVessel = b;
                                KCTGameStates.LaunchedVessel.LCID = KCTGameStates.LaunchedVessel.LC.ID; // clear LC and force refind later.

                                if (ShipConstruction.FindVesselsLandedAt(HighLogic.CurrentGame.flightState, "Runway").Count == 0)
                                {
                                    if (airlaunchPrep != null)
                                    {
                                        GUIStates.ShowBuildList = false;
                                        GUIStates.ShowAirlaunch = true;
                                    }
                                    else if (!IsCrewable(b.ExtractedParts))
                                    {
                                        b.Launch();
                                    }
                                    else
                                    {
                                        GUIStates.ShowBuildList = false;
                                        KCTGameStates.ToolbarControl?.SetFalse();
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
                            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotLaunchEditorChecksPopup", "Cannot Launch!", "Warning! This vessel did not pass the editor checks! Until you upgrade this launch complex (the Hangar) it cannot be launched. Listed below are the failed checks:\n" + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()), "Acknowledged", false, HighLogic.UISkin);
                        }
                    }
                }
            }

            GUILayout.EndHorizontal();
        }

        private static void RenderLaunchComplexControls()
        {
            LCItem activeLC = KCTGameStates.EditorShipEditingMode ? KCTGameStates.EditedVessel.LC : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

            GUILayout.BeginHorizontal();
            // Don't allow switching in edit mode
            int lcCount = KCTGameStates.EditorShipEditingMode ? 1 : KCTGameStates.ActiveKSC.LaunchComplexCount;
            if (lcCount > 1 && GUILayout.Button("<<", GUILayout.ExpandWidth(false)))
            {
                KCTGameStates.ActiveKSC.SwitchToPrevLaunchComplex();
                if (HighLogic.LoadedSceneIsEditor)
                {
                    Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                }
            }
            GUILayout.FlexibleSpace();
            string padTxt = $"{activeLC.Name} ({activeLC.SupportedMassAsPrettyText})";
            string padDesc = $"Size limit: {activeLC.SupportedSizeAsPrettyText}";
            GUILayout.Label(new GUIContent(padTxt, padDesc));

            if (GUILayout.Button(new GUIContent("Rename", "Rename Complex"), GUILayout.ExpandWidth(false)))
            {
                _renameType = RenameType.LaunchComplex;
                _newName = activeLC.Name;
                GUIStates.ShowDismantlePad = false;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowDismantleLC = false;
                GUIStates.ShowNewPad = false;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowRename = true;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
                _centralWindowPosition.width = 300;
            }
            bool canModify = activeLC.CanModify;
            const string modifyFailTooltip = "Currently in use! No projects can be underway or\nvessels at pads/airlaunching, though vessels can be in storage.";
            if (!HighLogic.LoadedSceneIsEditor && !GUIStates.ShowPersonnelWindow && GUILayout.Button(new GUIContent("Modify", canModify ? ("Modify " + (activeLC.IsPad ? "launch complex limits" : "hangar limits")) : modifyFailTooltip), 
                canModify ? GUI.skin.button : _yellowButton, GUILayout.ExpandWidth(false)))
            {
                if (canModify)
                {
                    _lengthLimit = activeLC.SizeMax.z.ToString("N0");
                    _widthLimit = activeLC.SizeMax.x.ToString("N0");
                    _heightLimit = activeLC.SizeMax.y.ToString("N0");
                    _tonnageLimit = activeLC.MassMax.ToString("N0");
                    _isHumanRated = activeLC.IsHumanRated;
                    
                    GUIStates.ShowDismantlePad = false;
                    GUIStates.ShowModifyLC = true;
                    GUIStates.ShowDismantleLC = false;
                    GUIStates.ShowNewPad = false;
                    GUIStates.ShowNewLC = false;
                    GUIStates.ShowRename = false;
                    GUIStates.ShowBuildList = false;
                    GUIStates.ShowBLPlus = false;
                    _centralWindowPosition.width = 300;
                }
                else
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("KCTCantModify", modifyFailTooltip, "Can't Modify", null, new DialogGUIButton("OK", () => { })), false, HighLogic.UISkin);
                }
            }
            if (GUILayout.Button(new GUIContent("New", "Build a new launch complex"), GUILayout.ExpandWidth(false)))
            {
                _newName = $"Launch Complex {(KCTGameStates.ActiveKSC.LaunchComplexes.Count)}";
                _lengthLimit = "8";
                _widthLimit = "8";
                _heightLimit = "33";
                _tonnageLimit = "60";
                _isHumanRated = false;

                GUIStates.ShowDismantlePad = false;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowDismantleLC = false;
                GUIStates.ShowNewPad = false;
                GUIStates.ShowNewLC = true;
                GUIStates.ShowRename = false;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
                _centralWindowPosition.width = 300;
            }
            if (!HighLogic.LoadedSceneIsEditor && activeLC.IsPad && !GUIStates.ShowPersonnelWindow && GUILayout.Button(new GUIContent("Dismantle", canModify ? "Dismantle this launch complex. All stored vessels will be scrapped." : modifyFailTooltip),
                canModify ? GUI.skin.button : _yellowButton, GUILayout.ExpandWidth(false)))
            {
                if (canModify)
                {
                    GUIStates.ShowDismantlePad = false;
                    GUIStates.ShowModifyLC = false;
                    GUIStates.ShowDismantleLC = true;
                    GUIStates.ShowNewPad = false;
                    GUIStates.ShowNewLC = false;
                    GUIStates.ShowRename = false;
                    GUIStates.ShowBuildList = false;
                    GUIStates.ShowBLPlus = false;
                    _centralWindowPosition.width = 300;
                }
                else
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("KCTCantModify", modifyFailTooltip, "Can't Dismantle", null, new DialogGUIButton("OK", () => { })), false, HighLogic.UISkin);
                }
            }
            GUILayout.FlexibleSpace();
            if (lcCount > 1 && GUILayout.Button(">>", GUILayout.ExpandWidth(false)))
            {
                KCTGameStates.ActiveKSC.SwitchToNextLaunchComplex();
                if (HighLogic.LoadedSceneIsEditor)
                {
                    Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void RenderLaunchPadControls()
        {
            LCItem activeLC = KCTGameStates.EditorShipEditingMode ? KCTGameStates.EditedVessel.LC : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

            GUILayout.BeginHorizontal();
            bool oldRushing = activeLC.IsRushing;
            activeLC.IsRushing = GUILayout.Toggle(activeLC.IsRushing, new GUIContent("Rush",
                $"Enable rush building.\nRate: {LCItem.RushRateMult:N1}x\nCosts: Salary {LCItem.RushSalaryMult:N1}x,\n-{(1d - LCItem.RushEfficMult):P0} efficiency/day."));
            if (oldRushing != activeLC.IsRushing)
                Utilities.ChangeEngineers(activeLC, 0); // fire event to recalc salaries.

            KCT_LaunchPad activePad = activeLC.ActiveLPInstance;
            if (activePad == null)
            {
                GUILayout.EndHorizontal();
                return;
            }           
            
            GUILayout.Space(15);

            int lpCount = activeLC.LaunchPadCount;
            if (lpCount > 1 && GUILayout.Button("<<", GUILayout.ExpandWidth(false)))
            {
                activeLC.SwitchToPrevLaunchPad();
                if (HighLogic.LoadedSceneIsEditor)
                {
                    Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                }
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
                GUIStates.ShowRename = true;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
            }
            if (GUILayout.Button(new GUIContent("Location", "Choose KerbalKonstructs launch site"), GUILayout.ExpandWidth(false)))
            {
                _launchSites = Utilities.GetLaunchSites(true);
                if (_launchSites.Any())
                {
                    _isSelectingLaunchSiteForVessel = false;
                    GUIStates.ShowLaunchSiteSelector = true;
                    _centralWindowPosition.width = 300;
                }
                else
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("KCTNoLaunchsites", "No launch sites available!", "No Launch Sites", null, new DialogGUIButton("OK", () => { })), false, HighLogic.UISkin);
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
                GUIStates.ShowRename = false;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
                _centralWindowPosition.width = 300;
            }
            if (lpCount > 1 && GUILayout.Button(new GUIContent("Dismantle", "Permanently dismantle the launch pad. Can be used to lower maintenance costs by getting rid of unused pads."), GUILayout.ExpandWidth(false)))
            {
                GUIStates.ShowDismantlePad = true;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowDismantleLC = false;
                GUIStates.ShowNewPad = false;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowRename = false;
                GUIStates.ShowBuildList = false;
                GUIStates.ShowBLPlus = false;
            }
            GUILayout.FlexibleSpace();
            if (lpCount > 1 && GUILayout.Button(">>", GUILayout.ExpandWidth(false)))
            {
                activeLC.SwitchToNextLaunchPad();
                if (HighLogic.LoadedSceneIsEditor)
                {
                    Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                }
            }
            GUILayout.EndHorizontal();
        }

        public static void CancelTechNode(int index)
        {
            RemoveInputLocks();

            if (KCTGameStates.TechList.Count > index)
            {
                TechItem node = KCTGameStates.TechList[index];
                KCTDebug.Log($"Cancelling tech: {node.TechName}");

                // cancel children
                for (int i = 0; i < KCTGameStates.TechList.Count; i++)
                {
                    List<string> parentList = KerbalConstructionTimeData.techNameToParents[KCTGameStates.TechList[i].TechID];
                    if (parentList.Contains(node.TechID))
                    {
                        CancelTechNode(i);
                        // recheck list in case multiple levels of children were deleted.
                        i = -1;
                        index = KCTGameStates.TechList.FindIndex(t => t.TechID == node.TechID);
                    }
                }

                if (Utilities.CurrentGameHasScience())
                {
                    bool valBef = KCTGameStates.IsRefunding;
                    KCTGameStates.IsRefunding = true;
                    try
                    {
                        ResearchAndDevelopment.Instance.AddScience(node.ScienceCost, TransactionReasons.None);    //Should maybe do tech research as the reason
                    }
                    finally
                    {
                        KCTGameStates.IsRefunding = valBef;
                    }
                }
                node.DisableTech();
                KCTGameStates.TechList.RemoveAt(index);
            }
        }

        public static void CancelConstruction(int index)
        {
            RemoveInputLocks();

            if (KCTGameStates.ActiveKSC.Constructions.Count > index)
            {
                ConstructionBuildItem item = KCTGameStates.ActiveKSC.Constructions[index];
                KCTDebug.Log($"Cancelling construction: {item.GetItemName()}");
                item.Cancel();
            }
        }

        private static void DrawBLPlusWindow(int windowID)
        {
            LCItem activeLC = KCTGameStates.EditorShipEditingMode ? KCTGameStates.EditedVessel.LC : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

            Rect parentPos = HighLogic.LoadedSceneIsEditor ? EditorBuildListWindowPosition : BuildListWindowPosition;
            _blPlusPosition.yMin = parentPos.yMin;
            _blPlusPosition.height = 225;
            BuildListVessel b = Utilities.FindBLVesselByID(activeLC, _selectedVesselId);
            GUILayout.BeginVertical();
            string launchSite = b.LaunchSite;

            if (launchSite == "LaunchPad")
            {
                if (b.LaunchSiteIndex >= 0)
                    launchSite = b.KSC.ActiveLaunchComplexInstance.LaunchPads[b.LaunchSiteIndex].name;
                else
                    launchSite = b.KSC.ActiveLaunchComplexInstance.ActiveLPInstance.name;
            }
            ReconRollout rollout = activeLC.GetReconRollout(ReconRollout.RolloutReconType.Rollout, launchSite);
            bool onPad = rollout != null && rollout.IsComplete() && rollout.AssociatedID == b.Id.ToString();
            //This vessel is rolled out onto the pad

            // 1.4 Addition
            if (!onPad && GUILayout.Button("Select LaunchSite"))
            {
                _launchSites = Utilities.GetLaunchSites(b.Type == BuildListVessel.ListType.VAB);
                if (_launchSites.Any())
                {
                    GUIStates.ShowBLPlus = false;
                    GUIStates.ShowLaunchSiteSelector = true;
                    _centralWindowPosition.width = 300;
                }
                else
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("KCTNoLaunchsites", "No launch sites available to choose from. Try visiting an editor first.", "No Launch Sites", null, new DialogGUIButton("OK", () => { })), false, HighLogic.UISkin);
                }
            }

            if (!onPad && GUILayout.Button("Scrap"))
            {
                InputLockManager.SetControlLock(ControlTypes.KSC_ALL, "KCTPopupLock");
                DialogGUIBase[] options = new DialogGUIBase[2];
                options[0] = new DialogGUIButton("Yes", ScrapVessel);
                options[1] = new DialogGUIButton("No", RemoveInputLocks);
                MultiOptionDialog diag = new MultiOptionDialog("scrapVesselConfirmPopup", $"Are you sure you want to scrap this vessel? You will regain <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>{b.Cost}.", "Scrap Vessel", null, 300, options);
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
                GUIStates.ShowBLPlus = false;
                ResetBLWindow(false);
            }

            if (!onPad && GUILayout.Button("Edit"))
            {
                GUIStates.ShowBLPlus = false;
                EditorWindowPosition.height = 1;
                string tempFile = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/Ships/temp.craft";
                b.ShipNode.Save(tempFile);
                GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                KCTGameStates.EditedVessel = b;
                KCTGameStates.EditedVessel.LCID = KCTGameStates.EditedVessel.LC.ID; // clear LC and force refind later.
                KCTGameStates.EditorShipEditingMode = true;
                KCTGameStates.MergingAvailable = b.IsFinished;

                InputLockManager.SetControlLock(ControlTypes.EDITOR_EXIT, "KCTEditExit");
                InputLockManager.SetControlLock(ControlTypes.EDITOR_NEW, "KCTEditNew");
                InputLockManager.SetControlLock(ControlTypes.EDITOR_LAUNCH, "KCTEditLaunch");

                EditorDriver.StartAndLoadVessel(tempFile, b.Type == BuildListVessel.ListType.VAB ? EditorFacility.VAB : EditorFacility.SPH);
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
                GUIStates.ShowRename = true;
                _newName = b.ShipName;
                _renameType = RenameType.Vessel;
            }

            if (GUILayout.Button("Duplicate"))
            {
                Utilities.TryAddVesselToBuildList(b.CreateCopy(true), skipPartChecks: true);
            }

            if (GUILayout.Button("Add to Plans"))
            {
                AddVesselToPlansList(b.CreateCopy(true));
            }

            if (activeLC.Recon_Rollout.Find(rr => rr.RRType == ReconRollout.RolloutReconType.Rollout && rr.AssociatedID == b.Id.ToString()) != null && GUILayout.Button("Rollback"))
            {
                activeLC.Recon_Rollout.Find(rr => rr.RRType == ReconRollout.RolloutReconType.Rollout && rr.AssociatedID == b.Id.ToString()).SwapRolloutType();
                GUIStates.ShowBLPlus = false;
            }

            if (!b.IsFinished && GUILayout.Button(new GUIContent("Warp To", $"Salary Cost: {(b.GetTimeLeft() / 86400d * KCTGameStates.GetTotalMaintenanceAndSalaryPerDay()):N0}")))
            {
                KCTWarpController.Create(b);
                GUIStates.ShowBLPlus = false;
            }

            if (!b.IsFinished && GUILayout.Button("Move to Top"))
            {
                if (_isOperationsSelected)
                {
                    if (activeLC.BuildList.Remove(b))
                        activeLC.BuildList.Insert(0, b);
                    activeLC.RecalculateBuildRates();
                }
            }

            //if (!b.IsFinished &&
            //    (PresetManager.Instance.ActivePreset.GeneralSettings.MaxRushClicks == 0 || b.RushBuildClicks < PresetManager.Instance.ActivePreset.GeneralSettings.MaxRushClicks) &&
            //    (b.LC.Engineers == 0 ? GUILayout.Button(new GUIContent("Rush Build\nUnavailable", "Rush building requires Engineers!"), _redButton)
            //    : GUILayout.Button(new GUIContent($"Rush Build {(10d /** b.LC.Engineers / b.LC.MaxPersonnel*/):N0}%\n√{Math.Round(b.GetRushCost())}",
            //        $"Progress proportional to Engineers.\nWill cause {b.GetRushEfficiencyCost():P0}pt loss to efficiency\n at {b.LC.Name}."))))
            //{
            //    b.DoRushBuild();
            //}

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
            LCItem activeLC = KCTGameStates.EditorShipEditingMode ? KCTGameStates.EditedVessel.LC : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

            GUILayout.BeginVertical();
            _launchSiteScrollView = GUILayout.BeginScrollView(_launchSiteScrollView, GUILayout.Height((float)Math.Min(Screen.height * 0.75, 25 * _launchSites.Count + 10)));

            foreach (string launchsite in _launchSites)
            {
                if (GUILayout.Button(launchsite))
                {
                    if (_isSelectingLaunchSiteForVessel)
                    {
                        //Set the chosen vessel's launch site to the selected site
                        BuildListVessel blv = Utilities.FindBLVesselByID(null, _selectedVesselId);
                        blv.LaunchSite = launchsite;
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
            RemoveInputLocks();
            BuildListVessel b = Utilities.FindBLVesselByID(null, _selectedVesselId);
            if (b == null)
            {
                KCTDebug.Log("Tried to remove a vessel that doesn't exist!");
                return;
            }
            Utilities.ScrapVessel(b);
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
