using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using System.Collections;
using RP0.ConfigurableStart;
using ROUtils.DataTypes;
using ROUtils;

namespace RP0
{
    [System.Flags]
    public enum ResourceTagType
    {
        None = 0,
        Toxic = 1,
        Cryogenic = 2,
    }

    [System.Flags]
    public enum LCResourceType
    {
        None = 0,
        Fuel = 1,
        Waste = 2,
        PadIgnore = 4,
        HangarIgnore = 8,
    }

    public class ResourceInfo
    {
        [Persistent]
        public PersistentDictionaryValueTypes<string, LCResourceType> LCResourceTypes = new PersistentDictionaryValueTypes<string, LCResourceType>();
        
        [Persistent]
        public PersistentDictionaryValueTypes<string, ResourceTagType> ResourceTagTypes = new PersistentDictionaryValueTypes<string, ResourceTagType>();
    }

    /// <summary>
    /// Holds raw cfg data from SCMREFURBTECHS and SCMRECOVERYTECHS nodes.
    /// </summary>
    public class RecoveryTechSettings
    {
        public double RefurbishmentRateBase { get; set; } = 1.0d;
        public double RecoveryRateBase { get; set; } = 1.0d;
        public double RecoveryCostBase { get; set; } = 1.0d;

        public struct RefurbTechEntry
        {
            public string techID { get; set; }
            public double rateRefurbishment { get; set; }
            public double costRefurbishment { get; set; }
            public double splashdownPenaltyMult { get; set; }
        }

        public struct RecoveryTechEntry
        {
            public string techID { get; set; }
            public double rateRecovery { get; set; }
            public double costRecovery { get; set; }
        }

        public List<RefurbTechEntry> RefurbEntries { get; } = new List<RefurbTechEntry>();
        public List<RecoveryTechEntry> RecoveryEntries { get; } = new List<RecoveryTechEntry>();

        public double RecoveryRateMult { get; private set; } = 1.0d;
        public double RecoveryCostMult { get; private set; } = 1.0d;
        public double RefurbishmentRateMult { get; private set; } = 1.0d;
        public double RefurbishmentCostMult { get; private set; } = 1.0d;
        public double SplashdownPenaltyMult { get; private set; } = 1.0d;

        public void LoadRefurb(ConfigNode refurbNode)
        {
            RefurbEntries.Clear();
            if (refurbNode == null) return;

            if (refurbNode.HasNode("BASE"))
            {
                var b = refurbNode.GetNode("BASE");
                double tempBase = RefurbishmentRateBase;
                if (b.TryGetValue("RateRefurbishment", ref tempBase))
                    RefurbishmentRateBase = tempBase;
            }

            foreach (ConfigNode tech in refurbNode.GetNodes("TECH"))
            {
                var e = new RefurbTechEntry();
                e.techID = tech.GetValue("id");
                
                double tRate = 1.0d;
                double tCost = 1.0d;
                double tSplash = 1.0d;

                tech.TryGetValue("RateRefurbishment", ref tRate);
                tech.TryGetValue("CostRefurbishment", ref tCost);
                tech.TryGetValue("SplashdownPenaltyMult", ref tSplash);

                e.rateRefurbishment = tRate;
                e.costRefurbishment = tCost;
                e.splashdownPenaltyMult = tSplash;

                if (!string.IsNullOrEmpty(e.techID))
                    RefurbEntries.Add(e);
            }
        }

        public void LoadRecovery(ConfigNode recNode)
        {
            RecoveryEntries.Clear();
            if (recNode == null) return;

            if (recNode.HasNode("BASE"))
            {
                var b = recNode.GetNode("BASE");
                double tRate = RecoveryRateBase;
                double tCost = RecoveryCostBase;
                
                b.TryGetValue("RateRecovery", ref tRate);
                b.TryGetValue("CostRecovery", ref tCost);
                
                RecoveryRateBase = tRate;
                RecoveryCostBase = tCost;
            }

            foreach (ConfigNode tech in recNode.GetNodes("TECH"))
            {
                var e = new RecoveryTechEntry();
                e.techID = tech.GetValue("id");
                
                double tRate = 1.0d;
                double tCost = 1.0d;
                
                tech.TryGetValue("RateRecovery", ref tRate);
                tech.TryGetValue("CostRecovery", ref tCost);
                
                e.rateRecovery = tRate;
                e.costRecovery = tCost;

                if (!string.IsNullOrEmpty(e.techID))
                    RecoveryEntries.Add(e);
            }
        }

        public void RecalculateAndApply()
        {
            if (HighLogic.CurrentGame == null) return;
            if (HighLogic.CurrentGame.Mode != Game.Modes.SANDBOX && ResearchAndDevelopment.Instance == null) return;

            // Refurbishment
            double refurbRate = RefurbishmentRateBase;
            double refurbCost = 1.0d;
            double splashdown = 1.0d;

            foreach (var e in RefurbEntries)
            {
                if (ResearchAndDevelopment.GetTechnologyState(e.techID) == RDTech.State.Available)
                {
                    refurbRate *= e.rateRefurbishment;
                    refurbCost *= e.costRefurbishment;
                    splashdown *= e.splashdownPenaltyMult;
                }
            }

            RefurbishmentRateMult  = refurbRate;
            RefurbishmentCostMult  = refurbCost;
            SplashdownPenaltyMult  = splashdown;

            // Recovery
            double recRate = RecoveryRateBase;
            double recCost = RecoveryCostBase;

            foreach (var e in RecoveryEntries)
            {
                if (ResearchAndDevelopment.GetTechnologyState(e.techID) == RDTech.State.Available)
                {
                    recRate *= e.rateRecovery;
                    recCost *= e.costRecovery;
                }
            }

            RecoveryRateMult = recRate;
            RecoveryCostMult = recCost;
        }
    }

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class Database : MonoBehaviour
    {
        public static readonly ResourceInfo ResourceInfo = new ResourceInfo();

        public static readonly PersistentDictionaryNodeKeyed<PartEffectiveCostModifier> KCTCostModifiers = new PersistentDictionaryNodeKeyed<PartEffectiveCostModifier>();

        public static readonly List<SpaceCenterFacility> LockedFacilities = new List<SpaceCenterFacility>();
        public static readonly Dictionary<SpaceCenterFacility, List<int>> FacilityLevelCosts = new Dictionary<SpaceCenterFacility, List<int>>();
        public static int GetFacilityLevelCount(SpaceCenterFacility fac) { return FacilityLevelCosts.ValueOrDefault(fac)?.Count ?? 1; }

        public static readonly Dictionary<string, string> TechNameToTitle = new Dictionary<string, string>();
        public static readonly Dictionary<string, List<string>> TechNameToParents = new Dictionary<string, List<string>>();
        public static readonly PersistentDictionaryNodeKeyed<TechPeriod> TechNodePeriods = new PersistentDictionaryNodeKeyed<TechPeriod>("id");
        public static readonly PersistentDictionaryValueTypes<string, NodeType> NodeTypes = new PersistentDictionaryValueTypes<string, NodeType>();
        public static readonly Dictionary<string, Dictionary<string, HashSet<ExperimentSituations>>> StartCompletedExperiments = new Dictionary<string, Dictionary<string, HashSet<ExperimentSituations>>>();
        public static Dictionary<string, Scenario> CustomScenarios = new Dictionary<string, Scenario>();

        public static readonly SpaceCenterSettings SettingsSC = new SpaceCenterSettings();
        public static readonly CrewSettings SettingsCrew = new CrewSettings();

        /// <summary>
        /// Holds raw cfg data and active multipliers for both Refurbishment and Recovery tech progression.
        /// </summary>
        public static readonly RecoveryTechSettings SettingsRecovery = new RecoveryTechSettings();

        private void Awake()
        {
            if (LoadingScreen.Instance?.loaders is List<LoadingSystem> loaders)
            {
                if (!(loaders.FirstOrDefault(x => x is KCTDataLoader) is KCTDataLoader))
                {
                    var go = new GameObject("KCTDataLoader");
                    var configLoader = go.AddComponent<KCTDataLoader>();

                    int index = loaders.FindIndex(x => x is PartLoader);
                    if (index == -1)
                        index = System.Math.Max(0, loaders.Count - 1);
                    loaders.Insert(index, configLoader);
                }
            }
        }

        public static void LoadTree()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX || HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                // On starting a new game, MM has not yet patched the tech tree URL so we're
                // going to use that directly instead of the one in HighLogic.
                if (HighLogic.CurrentGame.Parameters.Career.TechTreeUrl.Contains("Squad"))
                    HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = System.IO.Path.Combine("GameData", "ModuleManager.TechTree");

                string fullPath = KSPUtil.ApplicationRootPath + HighLogic.CurrentGame.Parameters.Career.TechTreeUrl;
                RP0Debug.Log($"Loading tech tree from {fullPath}");

                if (ConfigNode.Load(fullPath) is ConfigNode fileNode && fileNode.HasNode("TechTree"))
                {
                    TechNameToTitle.Clear();
                    TechNameToParents.Clear();

                    ConfigNode treeNode = fileNode.GetNode("TechTree");
                    foreach (ConfigNode n in treeNode.GetNodes("RDNode"))
                    {
                        string techID = n.GetValue("id");
                        if (techID != null)
                        {
                            string title = n.GetValue("title");
                            if (title != null)
                                TechNameToTitle[techID] = title;

                            var pList = new List<string>();
                            foreach (ConfigNode p in n.GetNodes("Parent"))
                            {
                                string pID = p.GetValue("parentID");
                                if (pID != null)
                                    pList.Add(pID);
                            }
                            TechNameToParents[techID] = pList;
                        }
                    }
                }
            }
        }
    }

    public class KCTDataLoader : LoadingSystem
    {
        private const int NumLoaders = 9;

        private IEnumerator LoadRoutine()
        {
            int idx = 0;

            yield return StartCoroutine(LoadResources());
            _progress = ++idx;
            
            yield return StartCoroutine(LoadTags());
            _progress = ++idx;
            
            yield return StartCoroutine(LoadTechs());
            _progress = ++idx;
            
            yield return StartCoroutine(LoadFacilityData());
            _progress = ++idx;
            
            yield return StartCoroutine(LoadSpaceCenterSettings());
            _progress = ++idx;

            yield return StartCoroutine(LoadRecRefurbTechSettings());
            _progress = ++idx;

            yield return StartCoroutine(LoadCrewSettings());
            _progress = ++idx;
            
            yield return StartCoroutine(LoadCompletedExperiments());
            _progress = ++idx;
            
            yield return StartCoroutine(LoadConfigurableStartPresets());
            _progress = ++idx;
            
            yield return null;

            isReady = true;
        }

        private IEnumerator LoadResources()
        {
            var configNode = GameDatabase.Instance.GetConfigNodes("RP1_Resource_Info").FirstOrDefault();
            if (configNode == null)
                yield break;

            ConfigNode.LoadObjectFromConfig(Database.ResourceInfo, configNode);

            yield return null;
        }

        private IEnumerator LoadTags()
        {
            var node = GameDatabase.Instance.GetConfigNodes("KCTTAGS")?.FirstOrDefault();
            if (node == null)
                yield break;

            Database.KCTCostModifiers.Load(node);
        }

        private IEnumerator LoadTechs()
        {
            var rootNode = GameDatabase.Instance.GetConfigNodes("KCT_TECH_NODE_PERIODS")?.FirstOrDefault();
            if (rootNode == null)
                yield break;

            Database.TechNodePeriods.Load(rootNode);
            yield return null;

            Database.NodeTypes.Clear();
            ConfigNode typeNode = GameDatabase.Instance.GetConfigNodes("KCT_TECH_NODE_TYPES")?.FirstOrDefault();
            if (typeNode != null)
                Database.NodeTypes.Load(typeNode);

            yield return null;
        }

        private IEnumerator LoadFacilityData()
        {
            Database.LockedFacilities.Clear();

            var rootNode = GameDatabase.Instance.GetConfigNodes("CUSTOMBARNKIT")?.FirstOrDefault();
            if (rootNode == null)
                yield break;

            foreach (ConfigNode node in rootNode.nodes)
            {
                SpaceCenterFacility fac = SpaceCenterFacility.Administration;
                switch (node.name)
                {
                    case "ASTRONAUTS": fac = SpaceCenterFacility.AstronautComplex; break;
                    case "MISSION": fac = SpaceCenterFacility.MissionControl; break;
                    case "TRACKING": fac = SpaceCenterFacility.TrackingStation; break;
                    case "ADMINISTRATION": fac = SpaceCenterFacility.Administration; break;
                    case "VAB": fac = SpaceCenterFacility.VehicleAssemblyBuilding; break;
                    case "SPH": fac = SpaceCenterFacility.SpaceplaneHangar; break;
                    case "LAUNCHPAD": fac = SpaceCenterFacility.LaunchPad; break;
                    case "RUNWAY": fac = SpaceCenterFacility.Runway; break;
                    case "RESEARCH": fac = SpaceCenterFacility.ResearchAndDevelopment; break;
                    default: continue;
                }

                string up = string.Empty;
                node.TryGetValue("upgrades", ref up);

                if (up == "1, 1, 1")
                    Database.LockedFacilities.Add(fac);

                string costs = string.Empty;
                node.TryGetValue("upgrades", ref costs);
                List<int> costList = new List<int>();
                costList.FromCommaString(costs);
                Database.FacilityLevelCosts[fac] = costList;
            }
        }

        private IEnumerator LoadSpaceCenterSettings()
        {
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("SPACECENTERSETTINGS"))
            {
                Database.SettingsSC.Load(n);
                yield return null;
            }
        }

        /// <summary>
        /// Loads refurb and recovery cfgs
        /// </summary>
        private static IEnumerator LoadRecRefurbTechSettings()
        {
            ConfigNode refurbNode = GameDatabase.Instance.GetConfigNodes("SCMREFURBTECHS")?.FirstOrDefault();
            Database.SettingsRecovery.LoadRefurb(refurbNode);

            ConfigNode recNode = GameDatabase.Instance.GetConfigNodes("SCMRECOVERYTECHS")?.FirstOrDefault();
            Database.SettingsRecovery.LoadRecovery(recNode);

            yield return null;
        }

        private IEnumerator LoadCrewSettings()
        {
            foreach (ConfigNode stg in GameDatabase.Instance.GetConfigNodes("CREWSETTINGS"))
            {
                Database.SettingsCrew.Load(stg);
                yield return null;
            }
        }

        private IEnumerator LoadCompletedExperiments()
        {
            foreach (ConfigNode rootCN in GameDatabase.Instance.GetConfigNodes("IGNORED_EXPERIMENTS"))
            {
                yield return LoadExperiments(rootCN, Database.StartCompletedExperiments);
            }
        }

        public static IEnumerator LoadExperiments(ConfigNode rootCN, Dictionary<string, Dictionary<string, HashSet<ExperimentSituations>>> experimentDict)
        {
            foreach (ConfigNode bodyCN in rootCN.GetNodes("BODY"))
            {
                string bodyName = bodyCN.GetValue("name");
                if (!experimentDict.TryGetValue(bodyName, out var dict))
                {
                    dict = new Dictionary<string, HashSet<ExperimentSituations>>();
                    experimentDict[bodyName] = dict;
                }

                foreach (ConfigNode expCN in bodyCN.GetNodes("EXPERIMENT"))
                {
                    string experimentName = expCN.GetValue("name");
                    if (!dict.TryGetValue(experimentName, out var set))
                    {
                        set = new HashSet<ExperimentSituations>();
                        dict[experimentName] = set;
                    }

                    foreach (ConfigNode sitCN in expCN.GetNodes("SITUATIONS"))
                    {
                        foreach (string situationName in sitCN.GetValues("name"))
                        {
                            if (!System.Enum.TryParse(situationName, out ExperimentSituations situation))
                            {
                                RP0Debug.LogError($"MarkExperimentAsDone: Invalid situation {situationName}");
                                continue;
                            }
                            set.Add(situation);
                        }
                    }
                    yield return null;
                }
            }
        }

        public static IEnumerator LoadConfigurableStartPresets()
        {
            Dictionary<string, Scenario> scenarioDict = Database.CustomScenarios;
            scenarioDict.Clear();
            scenarioDict[ScenarioHandler.EmptyScenarioName] = new Scenario(ScenarioHandler.EmptyScenarioName);
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("CUSTOMSCENARIO");

            foreach (ConfigNode scenarioNode in nodes)
            {
                if (scenarioNode == null)
                    continue;
                try
                {
                    var s = new Scenario(scenarioNode);
                    scenarioDict[s.ScenarioName] = s;
                }
                catch (Exception ex)
                {
                    RP0Debug.LogError($"{ex}");
                }
                yield return null;
            }

            int actualCount = scenarioDict.Count - 1;
            RP0Debug.Log($"Found {actualCount} scenario{(actualCount > 1 ? "s" : "")}");
        }

        private bool isReady = false;
        public override bool IsReady() => isReady;

        float _progress = 0f;
        public override float ProgressFraction() => _progress / NumLoaders;

        public override float LoadWeight() => 0.01f;

        public override string ProgressTitle() => "RP-1 Initialization & Setup";

        public override void StartLoad()
        {
            StartCoroutine(LoadRoutine());
        }
    }
}
