using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UniLinq;
using RP0.DataTypes;

namespace KerbalConstructionTime
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

                RP0.LocalizationHandler.UpdateLocalizedText();
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
                if (Utilities.ConfigNodesAreEquivalent(preset.AsConfigNode(), preset2.AsConfigNode()))
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
                    KCTDebug.Log("Loading settings from preset, rather than save. Name: " + ActivePreset.Name);
                }
                else
                {
                    ActivePreset = saved;
                    KCTDebug.Log("Loading saved settings.");
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
                    KCTDebug.Log("Found preset at " + fi.Name);
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
                    if (Utilities.CurrentGameIsCareer() && !newPreset.CareerEnabled) continue;    //Don't display presets that aren't designed for this game mode
                    if (HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX && !newPreset.ScienceEnabled) continue;
                    if (Utilities.CurrentGameIsSandbox() && !newPreset.SandboxEnabled) continue;
                    KCT_Preset existing = FindPresetByShortName(newPreset.ShortName);
                    if (existing != null) //Ensure there is only one preset with a given name. Take the last one found as the final one.
                    {
                        Presets.Remove(existing);
                    }
                    Presets.Add(newPreset);
                }
                catch
                {
                    Debug.LogError("[KCT] Could not load preset at " + file);
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

        public int GetStartingPersonnel(Game.Modes mode)
        {
            if (mode == Game.Modes.CAREER)
            {
                return ActivePreset.GeneralSettings.StartingPersonnel[0];
            }
            else
            {
                return ActivePreset.GeneralSettings.StartingPersonnel[1];
            }
        }
    }

    public class KCT_Preset
    {
        internal string _presetFileLocation = string.Empty;

        public KCT_Preset_General GeneralSettings = new KCT_Preset_General();
        public KCT_Preset_Part_Variables PartVariables = new KCT_Preset_Part_Variables();

        public string Name = "UNINIT", ShortName = "UNINIT", Description = "NA", Author = "NA";
        public bool CareerEnabled = true, ScienceEnabled = true, SandboxEnabled = true;    //These just control whether it should appear during these game types
        public bool AllowDeletion = true;

        public int GetResearcherCap(int lvl = -1)
        {
            return -1;

            /*if (lvl == -1)
                lvl = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.ResearchAndDevelopment);
            return GeneralSettings.ResearcherCaps[lvl];*/
        }

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
            PartVariables.FromConfigNode(Source.PartVariables.AsConfigNode());
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

            node.AddNode(PartVariables.AsConfigNode());
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

            if (node.HasNode("KCT_Preset_Part_Variables"))
                PartVariables.FromConfigNode(node.GetNode("KCT_Preset_Part_Variables"));
        }

        public void SaveToFile(string filePath)
        {
            var node = new ConfigNode("KCT_Preset");
            node.AddNode(AsConfigNode());
            node.Save(filePath);
        }

        public void LoadFromFile(string filePath)
        {
            KCTDebug.Log("Loading a preset from " + filePath);
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

    public class ApplicantsFromContracts : EfficiencyUpgrades, IConfigNode
    {
        public int GetApplicantsFromContract(string contract) => (int)GetValue(contract);
    }

    public class EfficiencyUpgrades : IConfigNode
    {
        private Dictionary<string, double> techMultipliers = new Dictionary<string, double>();

        public void Load(ConfigNode node)
        {
            techMultipliers.Clear();
            foreach (ConfigNode.Value kvp in node.values)
            {
                if (double.TryParse(kvp.value, out double val))
                    techMultipliers[kvp.name] = val;
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var kvp in techMultipliers)
                node.AddValue(kvp.Key, kvp.Value);
        }

        public double GetMultiplier()
        {
            double mult = 1d;
            foreach (var kvp in techMultipliers)
            {
                if (ResearchAndDevelopment.GetTechnologyState(kvp.Key) == RDTech.State.Available)
                    mult += kvp.Value;
            }

            return mult;
        }

        public double GetSum()
        {
            double sum = 0d;
            foreach (var kvp in techMultipliers)
            {
                if (ResearchAndDevelopment.GetTechnologyState(kvp.Key) == RDTech.State.Available)
                    sum += kvp.Value;
            }
            return sum;
        }

        public double GetValue(string tech)
        {
            double val;
            if (techMultipliers.TryGetValue(tech, out val))
                return val;

            return 0d;
        }
    }

    public class KCT_Preset_General
    {
        [Persistent]
        public bool Enabled = true, BuildTimes = true, TechUnlockTimes = true, KSCUpgradeTimes = true;
        [Persistent]
        public string VABRecoveryTech = null;
        [Persistent]
        public int HireCost = 200;
        [Persistent]
        public double AdditionalPadCostMult = 0.5d, RushRateMult = 1.5d, RushSalaryMult = 2d, IdleSalaryMult = 0.25, MergingTimePenalty = 0.05d, 
            EffectiveCostPerLiterPerResourceMult = 0.1d, ResearcherSciEfficiencyOffset = -1000d, ResearcherSciEfficiencyMult = 0.0001d;
        [Persistent]
        public FloatCurve EngineerSkillupRate = new FloatCurve();
        [Persistent]
        public FloatCurve ConstructionRushCost = new FloatCurve();
        [Persistent]
        public FloatCurve YearBasedRateMult = new FloatCurve();
        [Persistent]
        public EfficiencyUpgrades LCEfficiencyUpgradesMin = new EfficiencyUpgrades();
        [Persistent]
        public EfficiencyUpgrades LCEfficiencyUpgradesMax = new EfficiencyUpgrades();
        [Persistent]
        public EfficiencyUpgrades ResearcherEfficiencyUpgrades = new EfficiencyUpgrades();

        [Persistent]
        public PersistentListValueType<int> StartingPersonnel = new PersistentListValueType<int>();
        [Persistent]
        public PersistentListValueType<int> ResearcherCaps = new PersistentListValueType<int>();

        [Persistent]
        public ApplicantsFromContracts ContractApplicants = new ApplicantsFromContracts();

        public double LCEfficiencyMin => LCEfficiencyUpgradesMin.GetSum();
        public double LCEfficiencyMax => LCEfficiencyUpgradesMax.GetSum();
        public double ResearcherEfficiency => ResearcherEfficiencyUpgrades.GetMultiplier()
            * System.Math.Max(0d, (KerbalConstructionTimeData.Instance.SciPointsTotal + ResearcherSciEfficiencyOffset) * ResearcherSciEfficiencyMult);
    }

    public class KCT_Preset_Part_Variables
    {
        //provides the variables [PV] and [RV] to the EffectiveCost functions
        public Dictionary<string, double> Part_Variables = new Dictionary<string, double>();
        public Dictionary<string, double> Resource_Variables = new Dictionary<string, double>();

        private ConfigNode DictionaryToNode(Dictionary<string, double> theDict, string nodeName)
        {
            var node = new ConfigNode(nodeName);
            foreach (KeyValuePair<string, double> kvp in theDict)
                node.AddValue(kvp.Key, kvp.Value);

            return node;
        }

        private Dictionary<string, double> NodeToDictionary(ConfigNode node)
        {
            var dict = new Dictionary<string, double>();

            foreach (ConfigNode.Value val in node.values)
            {
                double.TryParse(val.value, out double tmp);
                dict.Add(val.name, tmp);
            }

            return dict;
        }

        public ConfigNode AsConfigNode()
        {
            var node = new ConfigNode("KCT_Preset_Part_Variables");
            node.AddNode(DictionaryToNode(Part_Variables, "Part_Variables"));
            node.AddNode(DictionaryToNode(Resource_Variables, "Resource_Variables"));

            return node;
        }

        public void FromConfigNode(ConfigNode node)
        {
            Part_Variables.Clear();
            Resource_Variables.Clear();

            if (node.HasNode("Part_Variables"))
                Part_Variables = NodeToDictionary(node.GetNode("Part_Variables"));
            if (node.HasNode("Resource_Variables"))
                Resource_Variables = NodeToDictionary(node.GetNode("Resource_Variables"));
        }

        public double GetPartVariable(string partName)
        {
            if (Part_Variables.ContainsKey(partName))
                return Part_Variables[partName];
            return 1.0;
        }

        public double GetValueModifier(Dictionary<string, double> dict, IEnumerable<string> tags)
        {
            double value = 1.0;
            foreach (var name in tags)
            {
                if (dict?.ContainsKey(name) == true)
                    value *= dict[name];
            }
            return value;

        }

        public double GetValueModifierMax(Dictionary<string, double> dict, IEnumerable<string> tags)
        {
            double value = 1.0;
            foreach (var name in tags)
            {
                if (dict?.ContainsKey(name) == true)
                    value = System.Math.Max(value, dict[name]);
            }
            return value;

        }

        //These are all multiplied in case multiple variables exist on one part
        public double GetResourceVariablesMult(List<string> resourceNames) => GetValueModifier(Resource_Variables, resourceNames);

        public double GetResourceVariablesMult(PartResourceList resources)
        {
            double value = 1.0;
            foreach (PartResource r in resources)
            {
                if (Resource_Variables.ContainsKey(r.resourceName))
                    value *= Resource_Variables[r.resourceName];
            }
            return value;
        }

        public double GetResourceVariableMult(string resName)
        {
            if (Resource_Variables.TryGetValue(resName, out double m))
                return m;
            return 1d;
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
