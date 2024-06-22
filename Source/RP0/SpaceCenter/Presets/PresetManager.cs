using System.Collections.Generic;
using System.IO;
using ROUtils;

namespace RP0
{
    public class PresetManager
    {
        public static PresetManager Instance;
        public KCT_Preset ActivePreset
        {
            get { return _activePreset; }
            set
            {
                _activePreset = value;

                if (value == null)
                    return;

                LocalizationHandler.UpdateLocalizedText();
            }
        }
        private KCT_Preset _activePreset;
        public List<KCT_Preset> Presets;
        public List<string> PresetPaths;

        public static string SettingsFilePath => $"{KSPUtil.ApplicationRootPath}/saves/{HighLogic.SaveFolder}/KCT_Settings.cfg";

        public PresetManager()
        {
            Presets = new List<KCT_Preset>();
            PresetPaths = new List<string>();
            FindPresetFiles();
            LoadPresets();
        }

        public static bool PresetLoaded()
        {
            return Instance != null && Instance.ActivePreset != null;
        }

        public KCT_Preset FindPresetByShortName(string name)
        {
            return Presets.Find(p => p.ShortName == name);
        }

        public void ClearPresets()
        {
            Presets.Clear();
            PresetPaths.Clear();
            ActivePreset = null;
        }

        public int GetIndex(KCT_Preset preset)
        {
            foreach (KCT_Preset preset2 in Presets)
            {
                if (KSPUtils.ConfigNodesAreEquivalent(preset.AsConfigNode(), preset2.AsConfigNode()))
                    return Presets.IndexOf(preset2);
            }
            return -1;
        }

        public string[] PresetShortNames(bool IncludeCustom)
        {
            var names = new List<string>();
            foreach (KCT_Preset preset in Presets)
            {
                names.Add(preset.ShortName);
            }
            if (IncludeCustom)
                names.Add("Custom");
            return names.ToArray();
        }

        public void SetActiveFromSaveData()
        {
            if (File.Exists(SettingsFilePath))
            {
                KCT_Preset saved = new KCT_Preset(SettingsFilePath);
                KCT_Preset source = FindPresetByShortName(saved.ShortName);
                if (source != null) //Get settings from the original preset, if it exists
                {
                    ActivePreset = source;
                    RP0Debug.Log("Loading settings from preset, rather than save. Name: " + ActivePreset.Name);
                }
                else
                {
                    ActivePreset = saved;
                    RP0Debug.Log("Loading saved settings.");
                }
            }
            else
            {
                KCT_Preset defaultSettings = FindPresetByShortName("RP1");
                if (defaultSettings != null)
                    ActivePreset = defaultSettings;
                else
                    ActivePreset = new KCT_Preset("UNINIT", "UNINIT", "NA", "NA");

                // Start sandbox saves with KCT disabled
                if (HighLogic.CurrentGame?.Mode == Game.Modes.SANDBOX)
                {
                    ActivePreset = new KCT_Preset(ActivePreset);
                    ActivePreset.RenameToCustom();
                    ActivePreset.GeneralSettings.Enabled = false;
                }
            }
        }

        public void SaveActiveToSaveData()
        {
            ActivePreset.SaveToFile(SettingsFilePath);
        }

        public void FindPresetFiles()
        {
            PresetPaths.Clear();

            var dir = new DirectoryInfo(KSPUtil.ApplicationRootPath + "GameData/RP-1/KCT_Presets");
            if (dir.Exists)
            {
                //Add all the files in the folder
                foreach (FileInfo fi in dir.GetFiles("*.cfg"))
                {
                    RP0Debug.Log("Found preset at " + fi.Name);
                    PresetPaths.Add(fi.FullName);
                }
            }
        }

        public void LoadPresets()
        {
            Presets.Clear();

            foreach (string file in PresetPaths)
            {
                try
                {
                    KCT_Preset newPreset = new KCT_Preset(file);
                    if (KSPUtils.CurrentGameIsCareer() && !newPreset.CareerEnabled) continue;    //Don't display presets that aren't designed for this game mode
                    if (HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX && !newPreset.ScienceEnabled) continue;
                    if (KSPUtils.CurrentGameIsSandbox() && !newPreset.SandboxEnabled) continue;
                    KCT_Preset existing = FindPresetByShortName(newPreset.ShortName);
                    if (existing != null) //Ensure there is only one preset with a given name. Take the last one found as the final one.
                    {
                        Presets.Remove(existing);
                    }
                    Presets.Add(newPreset);
                }
                catch
                {
                    RP0Debug.LogError("Could not load preset at " + file);
                }
            }
        }

        public void DeletePresetFile(string shortName)
        {
            KCT_Preset toDelete = FindPresetByShortName(shortName);
            if (toDelete != null && toDelete.AllowDeletion)
            {
                File.Delete(toDelete._presetFileLocation);
            }
            FindPresetFiles();
            LoadPresets();
        }
    }

    public class KCT_Preset
    {
        internal string _presetFileLocation = string.Empty;

        public KCT_Preset_General GeneralSettings = new KCT_Preset_General();

        public string Name = "UNINIT", ShortName = "UNINIT", Description = "NA", Author = "NA";
        public bool CareerEnabled = true, ScienceEnabled = true, SandboxEnabled = true;    //These just control whether it should appear during these game types
        public bool AllowDeletion = true;

        public KCT_Preset(string filePath)
        {
            LoadFromFile(filePath);
        }

        public KCT_Preset(string presetName, string presetShortName, string presetDescription, string presetAuthor)
        {
            Name = presetName;
            ShortName = presetShortName;
            Description = presetDescription;
            Author = presetAuthor;
        }

        public KCT_Preset(KCT_Preset Source)
        {
            Name = Source.Name;
            ShortName = Source.ShortName;
            Description = Source.Description;
            Author = Source.Author;
            AllowDeletion = Source.AllowDeletion;

            CareerEnabled = Source.CareerEnabled;
            ScienceEnabled = Source.ScienceEnabled;
            SandboxEnabled = Source.SandboxEnabled;

            ConfigNode.LoadObjectFromConfig(GeneralSettings, ConfigNode.CreateConfigFromObject(Source.GeneralSettings));
        }

        public ConfigNode AsConfigNode()
        {
            ConfigNode node = new ConfigNode("KCT_Preset");
            node.AddValue("name", Name);
            node.AddValue("shortName", ShortName);
            node.AddValue("description", Description);
            node.AddValue("author", Author);

            node.AddValue("allowDeletion", AllowDeletion);

            node.AddValue("career", CareerEnabled);
            node.AddValue("science", ScienceEnabled);
            node.AddValue("sandbox", SandboxEnabled);

            var gs = node.AddNode("KCT_Preset_General");
            ConfigNode.CreateConfigFromObject(GeneralSettings, gs);

            return node;
        }

        public void FromConfigNode(ConfigNode node)
        {
            Name = node.GetValue("name");
            ShortName = node.GetValue("shortName");
            Description = node.GetValue("description");
            Author = node.GetValue("author");

            bool.TryParse(node.GetValue("allowDeletion"), out AllowDeletion);

            bool.TryParse(node.GetValue("career"), out CareerEnabled);
            bool.TryParse(node.GetValue("science"), out ScienceEnabled);
            bool.TryParse(node.GetValue("sandbox"), out SandboxEnabled);

            ConfigNode gNode = node.GetNode("KCT_Preset_General");
            ConfigNode.LoadObjectFromConfig(GeneralSettings, gNode);
        }

        public void SaveToFile(string filePath)
        {
            var node = new ConfigNode("KCT_Preset");
            node.AddNode(AsConfigNode());
            node.Save(filePath);
        }

        public void LoadFromFile(string filePath)
        {
            RP0Debug.Log("Loading a preset from " + filePath);
            _presetFileLocation = filePath;
            ConfigNode node = ConfigNode.Load(filePath);
            FromConfigNode(node.GetNode("KCT_Preset"));
        }

        public void SetActive()
        {
            PresetManager.Instance.ActivePreset = this;
        }

        public void RenameToCustom()
        {
            Name = "Custom";
            ShortName = "Custom";
            Description = "A custom set of configs.";
            Author = HighLogic.SaveFolder;
        }
    }

    public class KCT_Preset_General
    {
        [Persistent]
        public bool Enabled = true, BuildTimes = true, TechUnlockTimes = true, KSCUpgradeTimes = true;
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
