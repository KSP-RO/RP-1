﻿using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using System.Collections;
using RP0.DataTypes;

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

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class Database : MonoBehaviour
    {
        public static readonly ResourceInfo ResourceInfo = new ResourceInfo();
        public static readonly PersistentDictionaryNodeKeyed<KCTCostModifier> KCTCostModifiers = new PersistentDictionaryNodeKeyed<KCTCostModifier>();
        public static readonly PersistentDictionaryNodeKeyed<TechPeriod> TechNodePeriods = new PersistentDictionaryNodeKeyed<TechPeriod>("id");
        public static readonly PersistentDictionaryValueTypes<string, NodeType> NodeTypes = new PersistentDictionaryValueTypes<string, NodeType>();
        public static readonly List<SpaceCenterFacility> LockedFacilities = new List<SpaceCenterFacility>();
        public static readonly Dictionary<SpaceCenterFacility, List<int>> FacilityLevelCosts = new Dictionary<SpaceCenterFacility, List<int>>();
        public static int GetFacilityLevelCount(SpaceCenterFacility fac) { return FacilityLevelCosts.ValueOrDefault(fac)?.Count ?? 0; }
        public static readonly Dictionary<string, SpaceCenterFacility> FacilityIDToFacility = new Dictionary<string, SpaceCenterFacility>();
        public static readonly Dictionary<string, string> TechNameToTitle = new Dictionary<string, string>();
        public static readonly Dictionary<string, List<string>> TechNameToParents = new Dictionary<string, List<string>>();

        public static readonly SpaceCenterSettings SettingsSC = new SpaceCenterSettings();
        public static readonly CrewSettings SettingsCrew = new CrewSettings();

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
        private const float NumLoaders = 6f;

        private IEnumerator LoadRoutine()
        {
            yield return StartCoroutine(LoadResources());
            _progress = 1f;
            yield return StartCoroutine(LoadTags());
            _progress = 2f;
            yield return StartCoroutine(LoadTechs());
            _progress = 3f;
            yield return StartCoroutine(LoadFacilityData());
            _progress = 4f;
            yield return StartCoroutine(LoadSpaceCenterSettings());
            _progress = 5f;
            yield return StartCoroutine(LoadCrewSettings());
            _progress = 6f;
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

        private IEnumerator LoadCrewSettings()
        {
            foreach (ConfigNode stg in GameDatabase.Instance.GetConfigNodes("CREWSETTINGS"))
            {
                Database.SettingsCrew.Load(stg);
                yield return null;
            }
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
