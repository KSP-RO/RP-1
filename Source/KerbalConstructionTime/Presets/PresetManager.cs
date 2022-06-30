﻿using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UniLinq;

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

                KSCContextMenuOverrider.AreTextsUpdated = false;

                if (value == null)
                    return;

                // Fixup upgrade text
                foreach (PartUpgradeHandler.Upgrade up in PartUpgradeManager.Handler)
                {
                    if (up.name.StartsWith("rp0EngineerUpgrade"))
                        up.description = KSP.Localization.Localizer.Format("#rp0EngineerUpgradeText", (value.GeneralSettings.EngineerEfficiencyUpgrades.GetValue(up.techRequired) * 100d).ToString("N0"));
                    else if (up.name.StartsWith("rp0ResearcherUpgrade"))
                        up.description = KSP.Localization.Localizer.Format("#rp0ResearcherUpgradeText", (value.GeneralSettings.ResearcherEfficiencyUpgrades.GetValue(up.techRequired) * 100d).ToString("N0"));
                }
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
        public int StartingPersonnel(Game.Modes mode)
        {
            if (mode == Game.Modes.CAREER)
            {
                return ActivePreset.StartPersonnel[0];
            }
            else if (mode == Game.Modes.SCIENCE_SANDBOX)
            {
                return ActivePreset.StartPersonnel[1];
            }
            else
            {
                return ActivePreset.StartPersonnel[2];
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

        private int[] _personnelInternal;
        public int[] StartPersonnel
        {
            get
            {
                if (_personnelInternal == null)
                {
                    _personnelInternal = new int[3] { 0, 0, 0 }; //career, science, sandbox
                    string[] personnel = GeneralSettings.StartingPersonnel.Split(new char[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < 3; i++)
                        if (!int.TryParse(personnel[i], out _personnelInternal[i]))
                            _personnelInternal[i] = 0;
                }
                return _personnelInternal;
            }
        }

        private int[] _researcherCaps = null;
        public int[] ResearcherCaps
        {
            get
            {
                if (_researcherCaps == null)
                {
                    string[] caps = GeneralSettings.ResearcherCaps.Split(new char[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    _researcherCaps = new int[caps.Length];
                    for (int i = 0; i < caps.Length; ++i)
                        if (int.TryParse(caps[i], out _researcherCaps[i]))
                        {
                            if (_researcherCaps[i] == -1)
                                _researcherCaps[i] = int.MaxValue;
                        }
                        else
                            _researcherCaps[i] = 5;
                }
                return _researcherCaps;
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

            ConfigNode fNode = FormulaSettings.AsConfigNode();
            if (FormulaSettings.YearBasedRateMult != null)
            {
                ConfigNode rateNode = fNode.AddNode("YearBasedRateMult");
                FormulaSettings.YearBasedRateMult.Save(rateNode);
            }
            node.AddNode(fNode);

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
            ConfigNode.LoadObjectFromConfig(TimeSettings, node.GetNode("KCT_Preset_Time"));

            ConfigNode fNode = node.GetNode("KCT_Preset_Formula");
            ConfigNode.LoadObjectFromConfig(FormulaSettings, fNode);

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

        public double GetValue(string tech)
        {
            double val;
            if (techMultipliers.TryGetValue(tech, out val))
                return val;

            return 0d;
        }
    }

    public class KCT_Preset_General : ConfigNodeStorage
    {
        [Persistent]
        public bool Enabled = true, BuildTimes = true, TechUnlockTimes = true, KSCUpgradeTimes = true;
        [Persistent]
        public string StartingPoints = "15,15,45", //Career, Science, and Sandbox modes
            StartingPersonnel = "20, 50, 10000",
            VABRecoveryTech = null,
            ResearcherCaps = "300, 500, 750, 1250, 2000, 3500, -1";
        [Persistent]
        public int MaxRushClicks = 0, HireCost = 200, UpgradeCost = 2000;
        [Persistent]
        public double EngineerStartEfficiency = 0.5, GlobalEngineerStartEfficiency = 0.5, ResearcherStartEfficiency = 0.5, 
            EngineerMaxEfficiency = 1.0, ResearcherMaxEfficiency = 1.0, GlobalEngineerMaxEfficiency = 1.0,
            EngineerDecayRate = 0.1, GlobalEngineerDecayRate = 0.1, ResearcherDecayRate = 0.1, AdditionalPadCostMult = 0.5d,
            RushRateMult = 1.5d, RushSalaryMult = 2d, RushEfficMult = 0.985d, RushEfficMin = 0.6d;
        [Persistent]
        public FloatCurve EngineerSkillupRate = new FloatCurve();
        [Persistent]
        public FloatCurve GlobalEngineerSkillupRate = new FloatCurve();
        [Persistent]
        public FloatCurve ResearcherSkillupRate = new FloatCurve();
        [Persistent]
        public EfficiencyUpgrades EngineerEfficiencyUpgrades = new EfficiencyUpgrades();
        [Persistent]
        public EfficiencyUpgrades ResearcherEfficiencyUpgrades = new EfficiencyUpgrades();
        [Persistent]
        public double SmallLCExtraFunds = 10000d;

        public double EngineerEfficiencyMultiplier => EngineerEfficiencyUpgrades.GetMultiplier();
        public double ResearcherEfficiencyMultiplier => ResearcherEfficiencyUpgrades.GetMultiplier();


        public override ConfigNode AsConfigNode()
        {
            ConfigNode node = base.AsConfigNode();
            ConfigNode tmp = node.GetNode("EngineerSkillupRate");
            if (tmp == null)
            {
                tmp = new ConfigNode("EngineerSkillupRate");
                EngineerSkillupRate.Save(tmp);
                node.AddNode(tmp);
            }
            tmp = node.GetNode("ResearcherSkillupRate");
            if (tmp == null)
            {
                tmp = new ConfigNode("ResearcherSkillupRate");
                ResearcherSkillupRate.Save(tmp);
                node.AddNode(tmp);
            }
            tmp = node.GetNode("GlobalEngineerSkillupRate");
            if (tmp == null)
            {
                tmp = new ConfigNode("GlobalEngineerSkillupRate");
                GlobalEngineerSkillupRate.Save(tmp);
                node.AddNode(tmp);
            }

            return node;
        }
    }

    public class KCT_Preset_Time : ConfigNodeStorage
    {
        [Persistent]
        public double OverallMultiplier = 1.0, BuildEffect = 1.0, InventoryEffect = 100.0, MergingTimePenalty = 0.05;
    }

    public class KCT_Preset_Formula : ConfigNodeStorage
    {
        [Persistent]
        public string NodeFormula = "2^([N]+1) / 86400",
            UpgradesForScience = "0",
            EffectivePartFormula = "min([C]/([I] + ([B]*([U]+1))) *[MV]*[PV], [C])",
            ProceduralPartFormula = "(([C]-[A]) + ([A]*10/max([I],1))) / max([B]*([U]+1),1) *[MV]*[PV]",
            BPFormula = "([E]^(1/2))*2000*[O]",
            KSCUpgradeFormula = "([C]^(1/2))*1000*[O]",
            ReconditioningFormula = "min([M]*[O]*[E], [X])*abs([RE]-[S])",
            BuildRateFormula = "(([I]+1)*0.05*[N] + max(0.1-[I], 0))*sign(2*[L]-[I]+1)",
            ConstructionRateFormula = "([N]*0.005)^0.625",
            InventorySaleFormula = "([V]+[P] / 10000)^(0.5)",    //Gives the TOTAL amount of points, decimals are kept //[V] = inventory value in funds, [P] = Value of all previous sales combined
            IntegrationTimeFormula = "0",    //[M]=Vessel loaded mass, [m]=vessel empty mass, [C]=vessel loaded cost, [c]=vessel empty cost, [BP]=vessel BPs, [E]=editor level, [L]=launch site level (pad), [VAB]=1 if VAB craft, 0 if SPH
            RolloutCostFormula = "0",    //[M]=Vessel loaded mass, [m]=vessel empty mass, [C]=vessel loaded cost, [c]=vessel empty cost, [BP]=vessel BPs, [E]=editor level, [L]=launch site level (pad), [VAB]=1 if VAB craft, 0 if SPH
            IntegrationCostFormula = "0",    //[M]=Vessel loaded mass, [m]=vessel empty mass, [C]=vessel loaded cost, [c]=vessel empty cost, [BP]=vessel BPs, [E]=editor level, [L]=launch site level (pad), [VAB]=1 if VAB craft, 0 if SPH
            RushCostFormula = "[TC]*0.2",
            AirlaunchCostFormula = "[E]*0.25",
            AirlaunchTimeFormula = "[BP]*0.25",
            EngineRefurbFormula = "0.5*(1+max(0,1-([RT]/10)))";    //[RT]=Runtime of used engine

        [Persistent]
        public FloatCurve YearBasedRateMult = new FloatCurve();
    }

    public class KCT_Preset_Part_Variables
    {
        //provides the variables [PV] and [RV] to the EffectiveCost functions
        public Dictionary<string, double> Part_Variables = new Dictionary<string, double>();
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
            node.AddNode(DictionaryToNode(Resource_Variables, "Resource_Variables"));
            node.AddNode(DictionaryToNode(Global_Variables, "Global_Variables"));

            return node;
        }

        public void FromConfigNode(ConfigNode node)
        {
            Part_Variables.Clear();
            Resource_Variables.Clear();
            Global_Variables.Clear();

            if (node.HasNode("Part_Variables"))
                Part_Variables = NodeToDictionary(node.GetNode("Part_Variables"));
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

        public double GetValueModifier(Dictionary<string, double> dict, List<string> tags)
        {
            double value = 1.0;
            foreach (var name in tags)
            {
                if (dict?.ContainsKey(name) == true)
                    value *= dict[name];
            }
            return value;

        }

        //These are all multiplied in case multiple variables exist on one part
        public double GetResourceVariable(List<string> resourceNames) => GetValueModifier(Resource_Variables, resourceNames);

        public double GetGlobalVariable(List<string> moduleNames) => GetValueModifier(Global_Variables, moduleNames);

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

        public void SetGlobalVariables(HashSet<string> variables, PartModuleList modules)
        {
            foreach (PartModule mod in modules)
            {
                if (Global_Variables.ContainsKey(mod.moduleName))
                    variables.Add(mod.moduleName);
            }
        }

        public void SetGlobalVariables(HashSet<string> variables, List<string> moduleNames)
        {
            foreach (var name in moduleNames)
            {
                if (Global_Variables.ContainsKey(name))
                    variables.Add(name);
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
