using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class PresetManager
    {
        public static PresetManager Instance;
        public KCT_Preset ActivePreset;
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
            }
        }

        public void SaveActiveToSaveData()
        {
            ActivePreset.SaveToFile(SettingsFilePath);
        }

        public void FindPresetFiles()
        {
            PresetPaths.Clear();

            var dir = new DirectoryInfo(KSPUtil.ApplicationRootPath + "GameData/RP-0/KCT_Presets");
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

        public int StartingUpgrades(Game.Modes mode)
        {
            if (mode == Game.Modes.CAREER)
            {
                return ActivePreset.StartUpgrades[0];
            }
            else if (mode == Game.Modes.SCIENCE_SANDBOX)
            {
                return ActivePreset.StartUpgrades[1];
            }
            else
            {
                return ActivePreset.StartUpgrades[2];
            }
        }
    }

    public class KCT_Preset
    {
        internal string _presetFileLocation = string.Empty;

        public KCT_Preset_General GeneralSettings = new KCT_Preset_General();
        public KCT_Preset_Time TimeSettings = new KCT_Preset_Time();
        public KCT_Preset_Formula FormulaSettings = new KCT_Preset_Formula();
        public KCT_Preset_Part_Variables PartVariables = new KCT_Preset_Part_Variables();

        public string Name = "UNINIT", ShortName = "UNINIT", Description = "NA", Author = "NA";
        public bool CareerEnabled = true, ScienceEnabled = true, SandboxEnabled = true;    //These just control whether it should appear during these game types
        public bool AllowDeletion = true;

        private int[] _upgradesInternal;
        public int[] StartUpgrades
        {
            get
            {
                if (_upgradesInternal == null)
                {
                    _upgradesInternal = new int[3] {0, 0, 0}; //career, science, sandbox
                    string[] upgrades = GeneralSettings.StartingPoints.Split(',');
                    for (int i = 0; i < 3; i++)
                        if (!int.TryParse(upgrades[i], out _upgradesInternal[i]))
                            _upgradesInternal[i] = 0;
                }
                return _upgradesInternal;
            }
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

            ConfigNode.LoadObjectFromConfig(GeneralSettings, Source.GeneralSettings.AsConfigNode());
            ConfigNode.LoadObjectFromConfig(TimeSettings, Source.TimeSettings.AsConfigNode());
            ConfigNode.LoadObjectFromConfig(FormulaSettings, Source.FormulaSettings.AsConfigNode());
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

            node.AddNode(GeneralSettings.AsConfigNode());
            node.AddNode(TimeSettings.AsConfigNode());
            node.AddNode(FormulaSettings.AsConfigNode());
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

            ConfigNode.LoadObjectFromConfig(GeneralSettings, node.GetNode("KCT_Preset_General"));
            ConfigNode.LoadObjectFromConfig(TimeSettings, node.GetNode("KCT_Preset_Time"));
            ConfigNode.LoadObjectFromConfig(FormulaSettings, node.GetNode("KCT_Preset_Formula"));
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
    }

    public class KCT_Preset_General : ConfigNodeStorage
    {
        [Persistent]
        public bool Enabled = true, BuildTimes = true, ReconditioningTimes = true, ReconditioningBlocksPad = false, TechUnlockTimes = true, KSCUpgradeTimes = true,
            TechUpgrades = true, SharedUpgradePool = false, DisableLPUpgrades = false;
        [Persistent]
        public string StartingPoints = "15,15,45", //Career, Science, and Sandbox modes
            VABRecoveryTech = null;
        [Persistent]
        public int MaxRushClicks = 0;
    }

    public class KCT_Preset_Time : ConfigNodeStorage
    {
        [Persistent]
        public double OverallMultiplier = 1.0, BuildEffect = 1.0, InventoryEffect = 100.0, ReconditioningEffect = 1728, MaxReconditioning = 345600, RolloutReconSplit = 0.25;
    }

    public class KCT_Preset_Formula : ConfigNodeStorage
    {
        [Persistent]
        public string NodeFormula = "2^([N]+1) / 86400",
            UpgradeFundsFormula = "min(2^([N]+4) * 1000, 1024000)",
            UpgradesForScience = "0",
            ResearchFormula = "[N]*0.5/86400",
            EffectivePartFormula = "min([C]/([I] + ([B]*([U]+1))) *[MV]*[PV], [C])",
            ProceduralPartFormula = "(([C]-[A]) + ([A]*10/max([I],1))) / max([B]*([U]+1),1) *[MV]*[PV]",
            BPFormula = "([E]^(1/2))*2000*[O]",
            KSCUpgradeFormula = "([C]^(1/2))*1000*[O]",
            ReconditioningFormula = "min([M]*[O]*[E], [X])*abs([RE]-[S])",
            BuildRateFormula = "(([I]+1)*0.05*[N] + max(0.1-[I], 0))*sign(2*[L]-[I]+1)",
            UpgradeResetFormula = "2*([N]+1)",    //N = number of times it's been reset
            InventorySaleFormula = "([V]+[P] / 10000)^(0.5)",    //Gives the TOTAL amount of points, decimals are kept //[V] = inventory value in funds, [P] = Value of all previous sales combined
            IntegrationTimeFormula = "0",    //[M]=Vessel loaded mass, [m]=vessel empty mass, [C]=vessel loaded cost, [c]=vessel empty cost, [BP]=vessel BPs, [E]=editor level, [L]=launch site level (pad), [VAB]=1 if VAB craft, 0 if SPH
            RolloutCostFormula = "0",    //[M]=Vessel loaded mass, [m]=vessel empty mass, [C]=vessel loaded cost, [c]=vessel empty cost, [BP]=vessel BPs, [E]=editor level, [L]=launch site level (pad), [VAB]=1 if VAB craft, 0 if SPH
            IntegrationCostFormula = "0",    //[M]=Vessel loaded mass, [m]=vessel empty mass, [C]=vessel loaded cost, [c]=vessel empty cost, [BP]=vessel BPs, [E]=editor level, [L]=launch site level (pad), [VAB]=1 if VAB craft, 0 if SPH
            NewLaunchPadCostFormula = "100000*([N]^3)",    //[N]=total number of unlocked launchpads (negative disables)
            RushCostFormula = "[TC]*0.2",
            AirlaunchCostFormula = "[E]*0.25",
            AirlaunchTimeFormula = "[BP]*0.25";
    }

    public class KCT_Preset_Part_Variables
    {
        //provides the variables [PV] and [MV] to the EffectiveCost functions
        public Dictionary<string, double> Part_Variables = new Dictionary<string, double>();
        public Dictionary<string, double> Module_Variables = new Dictionary<string, double>();
        public Dictionary<string, double> Resource_Variables = new Dictionary<string, double>();

        public Dictionary<string, double> Global_Variables = new Dictionary<string, double>();

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
            node.AddNode(DictionaryToNode(Module_Variables, "Module_Variables"));
            node.AddNode(DictionaryToNode(Resource_Variables, "Resource_Variables"));
            node.AddNode(DictionaryToNode(Global_Variables, "Global_Variables"));

            return node;
        }

        public void FromConfigNode(ConfigNode node)
        {
            Part_Variables.Clear();
            Module_Variables.Clear();
            Resource_Variables.Clear();
            Global_Variables.Clear();

            if (node.HasNode("Part_Variables"))
                Part_Variables = NodeToDictionary(node.GetNode("Part_Variables"));
            if (node.HasNode("Module_Variables"))
                Module_Variables = NodeToDictionary(node.GetNode("Module_Variables"));
            if (node.HasNode("Resource_Variables"))
                Resource_Variables = NodeToDictionary(node.GetNode("Resource_Variables"));
            if (node.HasNode("Global_Variables"))
                Global_Variables = NodeToDictionary(node.GetNode("Global_Variables"));
        }

        public double GetPartVariable(string partName)
        {
            if (Part_Variables.ContainsKey(partName))
                return Part_Variables[partName];
            return 1.0;
        }

        //These are all multiplied in case multiple modules exist on one part
        public double GetModuleVariable(List<string> moduleNames)
        {
            double value = 1.0;
            for (int i = moduleNames.Count - 1; i >= 0; i--)
            {
                string name = moduleNames[i];

                if (Module_Variables.ContainsKey(name))
                    value *= Module_Variables[name];
            }
            return value;
        }

        public double GetResourceVariable(List<string> resourceNames)
        {
            double value = 1.0;
            for (int i = resourceNames.Count - 1; i >= 0; i--)
            {
                string name = resourceNames[i];

                if (Resource_Variables.ContainsKey(name))
                    value *= Resource_Variables[name];
            }
            return value;
        }

        public double GetGlobalVariable(List<string> moduleNames)
        {
            double value = 1.0;
            for (int i = moduleNames.Count - 1; i >= 0; i--)
            {
                string name = moduleNames[i];
                if (Global_Variables.ContainsKey(name))
                    value *= Global_Variables[name];
            }
            return value;
        }

        //These are all multiplied in case multiple modules exist on one part (this one takes a PartModuleList instead)
        public double GetModuleVariable(PartModuleList modules, out bool hasResourceMult)
        {
            double value = 1.0;
            hasResourceMult = true;
            foreach (PartModule mod in modules)
            {
                if (mod.moduleName == "ModuleTagNoResourceCostMult")
                    hasResourceMult = false;

                if (Module_Variables.ContainsKey(mod.moduleName))
                    value *= Module_Variables[mod.moduleName];
            }
            return value;
        }

        public double GetResourceVariable(PartResourceList resources)
        {
            double value = 1.0;
            foreach (PartResource r in resources)
            {
                if (Resource_Variables.ContainsKey(r.resourceName))
                    value *= Resource_Variables[r.resourceName];
            }
            return value;
        }

        public void SetGlobalVariables(List<string> variables, PartModuleList modules)
        {
            foreach (PartModule mod in modules)
            {
                if (Global_Variables.ContainsKey(mod.moduleName))
                    variables.AddUnique(mod.moduleName);
            }
        }

        public void SetGlobalVariables(List<string> variables, List<string> moduleNames)
        {
            for (int i = moduleNames.Count - 1; i >= 0; i--)
            {
                string name = moduleNames[i];
                if (Global_Variables.ContainsKey(name))
                    variables.AddUnique(name);
            }
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
