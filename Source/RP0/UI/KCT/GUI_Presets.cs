﻿using System;
using UnityEngine;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private const int _presetsWidth = 900, _presetsHeight = 600;
        private static Rect _presetPosition = new Rect((Screen.width-_presetsWidth) / 2, (Screen.height-_presetsHeight) / 2, _presetsWidth, _presetsHeight);
        private static Rect _presetNamingWindowPosition = new Rect((Screen.width - 250) / 2, (Screen.height - 50) / 2, 250, 50);
        private static int _presetIndex = -1;
        private static KCT_Preset _workingPreset;
        private static Vector2 _presetScrollView, _presetMainScroll;
        private static bool _isChanged = false;

        private static string _saveName, _saveShort, _saveDesc, _saveAuthor;
        private static bool _saveCareer, _saveScience, _saveSandbox;
        private static KCT_Preset _toSave;

        private static bool _disableAllMsgs, _showSimWatermark, _debug, _overrideLaunchBtn, _autoAlarms, _cleanUpKSCDebris, _useDates, _inPlaceEdit;
        private static int _newTimewarp;

        public static void DrawPresetWindow(int windowID)
        {
            if (_workingPreset == null)
            {
                SetNewWorkingPreset(new KCT_Preset(PresetManager.Instance.ActivePreset), false); //might need to copy instead of assign here
                _presetIndex = PresetManager.Instance.GetIndex(_workingPreset);
            }

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            //preset selector
            GUILayout.BeginVertical();
            GUILayout.Label("Presets", _yellowText, GUILayout.ExpandHeight(false));
            //preset toolbar in a scrollview
            _presetScrollView = GUILayout.BeginScrollView(_presetScrollView, GUILayout.Width(_presetPosition.width / 6f)); //TODO: update HighLogic.Skin.textArea
            string[] presetShortNames = PresetManager.Instance.PresetShortNames(true);
            if (_presetIndex == -1)
            {
                SetNewWorkingPreset(null, true);
            }
            if (_isChanged && _presetIndex < presetShortNames.Length - 1 && !KCTUtilities.ConfigNodesAreEquivalent(_workingPreset.AsConfigNode(), PresetManager.Instance.Presets[_presetIndex].AsConfigNode())) //!KCT_PresetManager.Instance.PresetsEqual(WorkingPreset, KCT_PresetManager.Instance.Presets[presetIndex], true)
            {
                SetNewWorkingPreset(null, true);
            }

            int prev = _presetIndex;
            _presetIndex = GUILayout.SelectionGrid(_presetIndex, presetShortNames, 1);
            if (prev != _presetIndex)    //If a new preset was selected
            {
                if (_presetIndex != presetShortNames.Length - 1)
                {
                    SetNewWorkingPreset(new KCT_Preset(PresetManager.Instance.Presets[_presetIndex]), false);
                }
                else
                {
                    SetNewWorkingPreset(null, true);
                }
            }

            //presetIndex = GUILayout.Toolbar(presetIndex, presetNames);

            GUILayout.EndScrollView();
            if (GUILayout.Button("Save as\nNew Preset", GUILayout.ExpandHeight(false)))
            {
                //create new preset
                SaveAsNewPreset(_workingPreset);
            }
            if (_workingPreset.AllowDeletion && _presetIndex != presetShortNames.Length - 1 && GUILayout.Button("Delete Preset")) //allowed to be deleted and isn't Custom
            {
                DialogGUIBase[] options = new DialogGUIBase[2];
                options[0] = new DialogGUIButton("Delete File", DeleteActivePreset);
                options[1] = new DialogGUIButton("Cancel", () => { });
                MultiOptionDialog dialog = new MultiOptionDialog("deletePresetPopup", "Are you sure you want to delete the selected Preset, file and all? This cannot be undone!", "Confirm Deletion", null, options);
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dialog, false, HighLogic.UISkin).HideGUIsWhilePopup();
            }
            GUILayout.EndVertical();

            //Main sections
            GUILayout.BeginVertical();
            _presetMainScroll = GUILayout.BeginScrollView(_presetMainScroll);
            //Preset info section)
            GUILayout.BeginVertical(HighLogic.Skin.textArea);
            GUILayout.Label("Preset Name: " + _workingPreset.Name);
            GUILayout.Label("Description: " + _workingPreset.Description);
            GUILayout.Label("Author(s): " + _workingPreset.Author);
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            //Features section
            GUILayout.BeginVertical();
            GUILayout.Label("Features", _yellowText);
            GUILayout.BeginVertical(HighLogic.Skin.textArea);
            _workingPreset.GeneralSettings.Enabled = GUILayout.Toggle(_workingPreset.GeneralSettings.Enabled, "Mod Enabled", HighLogic.Skin.button);
            _workingPreset.GeneralSettings.BuildTimes = GUILayout.Toggle(_workingPreset.GeneralSettings.BuildTimes, "Build Times", HighLogic.Skin.button);
            _workingPreset.GeneralSettings.TechUnlockTimes = GUILayout.Toggle(_workingPreset.GeneralSettings.TechUnlockTimes, "Tech Unlock Times", HighLogic.Skin.button);
            _workingPreset.GeneralSettings.KSCUpgradeTimes = GUILayout.Toggle(_workingPreset.GeneralSettings.KSCUpgradeTimes, "KSC Upgrade Times", HighLogic.Skin.button);

            
            GUILayout.EndVertical();
            GUILayout.EndVertical(); //end Features


            
            GUILayout.EndHorizontal(); //end feature/time setting split

            
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save", GUILayout.ExpandWidth(false)))
            {
                PresetManager.Instance.ActivePreset = _workingPreset;
                PresetManager.Instance.SaveActiveToSaveData();
                _workingPreset = null;

                if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                    KCTUtilities.DisableModFunctionality();
                KerbalConstructionTime.Settings.MaxTimeWarp = _newTimewarp;
                KerbalConstructionTime.Settings.DisableAllMessages = _disableAllMsgs;
                KerbalConstructionTime.Settings.ShowSimWatermark = _showSimWatermark;
                KerbalConstructionTime.Settings.OverrideLaunchButton = _overrideLaunchBtn;
                KerbalConstructionTime.Settings.AutoKACAlarms = _autoAlarms;
                KerbalConstructionTime.Settings.CleanUpKSCDebris = _cleanUpKSCDebris;
                KerbalConstructionTime.Settings.UseDates = _useDates;
                KerbalConstructionTime.Settings.InPlaceEdit = _inPlaceEdit;

                KerbalConstructionTime.Settings.Save();
                GUIStates.ShowSettings = false;
                if (!IsPrimarilyDisabled && !GUIStates.ShowFirstRun)
                {
                    ResetBLWindow();
                    GUIStates.ShowBuildList = true;
                    RefreshToolbarState();
                }
                if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled) InputLockManager.RemoveControlLock(KerbalConstructionTime.KCTKSCLock);

                KerbalConstructionTimeData.Instance.RecalculateBuildRates();

                ResetFormulaRateHolders();
                Harmony.PatchKSCFacilityContextMenu.AreTextsUpdated = false;
            }
            if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(false)))
            {
                _workingPreset = null;
                GUIStates.ShowSettings = false;
                if (!IsPrimarilyDisabled && !GUIStates.ShowFirstRun)
                {
                    ResetBLWindow();
                    GUIStates.ShowBuildList = true;
                    RefreshToolbarState();
                }

                KerbalConstructionTimeData.Instance.RecalculateBuildRates();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical(); //end column 2

            GUILayout.BeginVertical(GUILayout.Width(100)); //Start general settings
            GUILayout.Label("General Settings", _yellowText);
            GUILayout.Label("NOTE: Affects all saves!", _yellowText);
            GUILayout.BeginVertical(HighLogic.Skin.textArea);
            GUILayout.Label("Max Timewarp");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.ExpandWidth(false)))
            {
                _newTimewarp = Math.Max(_newTimewarp - 1, 0);
            }
            //current warp setting
            GUILayout.Label(TimeWarp.fetch.warpRates[_newTimewarp] + "x");
            if (GUILayout.Button("+", GUILayout.ExpandWidth(false)))
            {
                _newTimewarp = Math.Min(_newTimewarp + 1, TimeWarp.fetch.warpRates.Length - 1);
            }
            GUILayout.EndHorizontal();

            _autoAlarms = GUILayout.Toggle(_autoAlarms, "Auto KAC Alarms", HighLogic.Skin.button);
            _overrideLaunchBtn = GUILayout.Toggle(_overrideLaunchBtn, "Override Launch Button", HighLogic.Skin.button);
            //useBlizzyToolbar = GUILayout.Toggle(useBlizzyToolbar, "Use Toolbar Mod", HighLogic.Skin.button);
            _disableAllMsgs = !GUILayout.Toggle(!_disableAllMsgs, "Use Message System", HighLogic.Skin.button);
            _showSimWatermark = GUILayout.Toggle(_showSimWatermark, "Show sim watermark", HighLogic.Skin.button);
            _debug = GUILayout.Toggle(_debug, "Debug Logging", HighLogic.Skin.button);
            _cleanUpKSCDebris = GUILayout.Toggle(_cleanUpKSCDebris, "Autoclean KSC Debris", HighLogic.Skin.button);
            _useDates = GUILayout.Toggle(_useDates, "Use Dates Not +Days", HighLogic.Skin.button);
            _inPlaceEdit = GUILayout.Toggle(_inPlaceEdit, "Edit Keeps Buildorder", HighLogic.Skin.button);

            GUILayout.EndVertical();
            GUILayout.EndVertical();
            
            GUILayout.EndHorizontal(); //end main split
            GUILayout.EndVertical(); //end window

            _isChanged = GUI.changed;

            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
        }

        public static void SetNewWorkingPreset(KCT_Preset preset, bool setCustom)
        {
            if (preset != null)
                _workingPreset = preset;
            if (setCustom)
            {
                _presetIndex = PresetManager.Instance.PresetShortNames(true).Length - 1;
                _workingPreset.RenameToCustom();
            }
        }

        public static void DrawPresetSaveWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Preset name:");
            _saveName = GUILayout.TextField(_saveName, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Preset short name:");
            _saveShort = GUILayout.TextField(_saveShort, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Preset author(s):");
            _saveAuthor = GUILayout.TextField(_saveAuthor, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            GUILayout.Label("Preset description:");
            _saveDesc = GUILayout.TextField(_saveDesc, GUILayout.Width(220));
            //GUILayout.EndHorizontal();

            _saveCareer = GUILayout.Toggle(_saveCareer, " Show in Career Games");
            _saveScience = GUILayout.Toggle(_saveScience, " Show in Science Games");
            _saveSandbox = GUILayout.Toggle(_saveSandbox, " Show in Sandbox Games");

            KCT_Preset existing = PresetManager.Instance.FindPresetByShortName(_saveShort);
            bool AlreadyExists = existing != null;
            bool CanOverwrite = AlreadyExists ? existing.AllowDeletion : true;

            if (AlreadyExists)
                GUILayout.Label("Warning: A preset with that short name already exists!");

            GUILayout.BeginHorizontal();
            if (CanOverwrite && GUILayout.Button("Save"))
            {
                _toSave.Name = _saveName;
                _toSave.ShortName = _saveShort;
                _toSave.Description = _saveDesc;
                _toSave.Author = _saveAuthor;

                _toSave.CareerEnabled = _saveCareer;
                _toSave.ScienceEnabled = _saveScience;
                _toSave.SandboxEnabled = _saveSandbox;

                _toSave.AllowDeletion = true;

                _toSave.SaveToFile(KSPUtil.ApplicationRootPath + "/GameData/RP-1/KCT_Presets/" + _toSave.ShortName+".cfg");
                GUIStates.ShowPresetSaver = false;
                PresetManager.Instance.FindPresetFiles();
                PresetManager.Instance.LoadPresets();
            }
            if (GUILayout.Button("Cancel"))
            {
                GUIStates.ShowPresetSaver = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            CenterWindow(ref _presetNamingWindowPosition);
        }

        public static void SaveAsNewPreset(KCT_Preset newPreset)
        {
            _toSave = newPreset;
            _saveCareer = newPreset.CareerEnabled;
            _saveScience = newPreset.ScienceEnabled;
            _saveSandbox = newPreset.SandboxEnabled;

            _saveName = newPreset.Name;
            _saveShort = newPreset.ShortName;
            _saveDesc = newPreset.Description;
            _saveAuthor = newPreset.Author;

            GUIStates.ShowPresetSaver = true;
        }

        public static void DeleteActivePreset()
        {
            PresetManager.Instance.DeletePresetFile(_workingPreset.ShortName);
        }

        private static void ShowSettings()
        {
            _newTimewarp = KerbalConstructionTime.Settings.MaxTimeWarp;
            _disableAllMsgs = KerbalConstructionTime.Settings.DisableAllMessages;
            _showSimWatermark = KerbalConstructionTime.Settings.ShowSimWatermark;
            _overrideLaunchBtn = KerbalConstructionTime.Settings.OverrideLaunchButton;
            _autoAlarms = KerbalConstructionTime.Settings.AutoKACAlarms;
            _cleanUpKSCDebris = KerbalConstructionTime.Settings.CleanUpKSCDebris;
            _useDates = KerbalConstructionTime.Settings.UseDates;
            _inPlaceEdit = KerbalConstructionTime.Settings.InPlaceEdit;

            GUIStates.ShowSettings = !GUIStates.ShowSettings;
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
