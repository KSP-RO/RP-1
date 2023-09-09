using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using System.Collections;

namespace KerbalConstructionTime
{
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    public class Database : MonoBehaviour
    {
        public static HashSet<string> ValidFuelRes = new HashSet<string>();
        public static HashSet<string> WasteRes = new HashSet<string>();
        public static HashSet<string> PadIgnoreRes = new HashSet<string>();
        public static HashSet<string> HangarIgnoreRes = new HashSet<string>();
        public static List<SpaceCenterFacility> LockedFacilities = new List<SpaceCenterFacility>();
        public static Dictionary<SpaceCenterFacility, List<int>> FacilityLevelCosts = new Dictionary<SpaceCenterFacility, List<int>>();

        public static readonly RP0.SpaceCenterSettings SettingsSC = new RP0.SpaceCenterSettings();
        public static readonly RP0.CrewSettings SettingsCrew = new RP0.CrewSettings();

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
            Database.ValidFuelRes.Clear();
            Database.WasteRes.Clear();
            Database.PadIgnoreRes.Clear();
            Database.HangarIgnoreRes.Clear();

            var configNode = GameDatabase.Instance.GetConfigNodes("KCT_FUEL_RESOURCES").FirstOrDefault();
            if (configNode == null)
                yield break;

            int vCount = configNode.values.Count;
            for (int i = 0; i < vCount; ++i)
            {
                ConfigNode.Value v = configNode.values[i];
                if (string.IsNullOrEmpty(v.value))
                    continue;
                switch (v.name)
                {
                    case "fuelResource": Database.ValidFuelRes.Add(v.value); break;
                    case "wasteResource": Database.WasteRes.Add(v.value); break;
                    case "hangarIgnoreResource": Database.HangarIgnoreRes.Add(v.value); break;
                    // This one gets both
                    case "padIgnoreResource": Database.PadIgnoreRes.Add(v.value); Database.HangarIgnoreRes.Add(v.value); break;
                }
            }

            yield return null;
        }

        private IEnumerator LoadTags()
        {
            KerbalConstructionTime.KCTCostModifiers.Clear();
            var node = GameDatabase.Instance.GetConfigNodes("KCTTAGS")?.FirstOrDefault();
            if (node == null)
                yield break;

            var nodes = node.GetNodes("TAG");
            int len = nodes.Length;
            float inc = 1f / len;
            for (int i = 0; i < len; ++i)
            {
                _progress += inc;
                var tagNode = nodes[i];
                KCTCostModifier x = new KCTCostModifier();
                if (ConfigNode.LoadObjectFromConfig(x, tagNode) && !string.IsNullOrEmpty(x.name))
                {
                    if (string.IsNullOrEmpty(x.displayName))
                        x.displayName = x.name;
                    KerbalConstructionTime.KCTCostModifiers[x.name] = x;
                }
                if (i % 10 == 0)
                    yield return null;
            }
        }

        private IEnumerator LoadTechs()
        {
            KerbalConstructionTime.TechNodePeriods.Clear();
            var rootNode = GameDatabase.Instance.GetConfigNodes("KCT_TECH_NODE_PERIODS")?.FirstOrDefault();
            if (rootNode == null)
                yield break;

            var nodes = rootNode.GetNodes("TECHNode");
            int len = nodes.Length;
            float inc = 1f / len;
            for (int i = 0; i < len; ++i)
            {
                _progress += inc;
                var node = nodes[i];
                KCTTechNodePeriod x = new KCTTechNodePeriod();
                if (ConfigNode.LoadObjectFromConfig(x, node) && !string.IsNullOrEmpty(x.id))
                {
                    KerbalConstructionTime.TechNodePeriods[x.id] = x;
                }
                if (i % 10 == 0)
                    yield return null;
            }

            KerbalConstructionTime.NodeTypes.Clear();
            ConfigNode typeNode = GameDatabase.Instance.GetConfigNodes("KCT_TECH_NODE_TYPES")?.FirstOrDefault();
            if (typeNode != null)
                KerbalConstructionTime.NodeTypes.Load(typeNode);

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
                bool isFac = true;
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
                    default: isFac = false; break;
                }

                if (isFac)
                {
                    string up = string.Empty;
                    node.TryGetValue("upgrades", ref up);

                    if (up == "1, 1, 1")
                        Database.LockedFacilities.Add(fac);

                    string costs = string.Empty;
                    node.TryGetValue("upgrades", ref costs);
                    List<int> costList = new List<int>();
                    costList.FromCommaString<int>(costs);
                    Database.FacilityLevelCosts[fac] = costList;
                }
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
