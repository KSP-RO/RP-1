﻿using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace KerbalConstructionTime
{
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    public class GuiDataAndWhitelistItemsDatabase : MonoBehaviour
    {
        public static HashSet<string> ValidFuelRes = new HashSet<string>();
        public static HashSet<string> WasteRes = new HashSet<string>();
        public static HashSet<string> PadIgnoreRes = new HashSet<string>();
        public static HashSet<string> HangarIgnoreRes = new HashSet<string>();

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
        private void LoadCustomItems()
        {
            foreach (var configNode in GameDatabase.Instance.GetConfigNodes("KCT_FUEL_RESOURCES"))
            {
                foreach (var item in configNode?.GetValuesList("fuelResource"))
                {
                    if (!string.IsNullOrEmpty(item))
                        GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Add(item);
                }
                foreach (var item in configNode?.GetValuesList("wasteResource"))
                {
                    if (!string.IsNullOrEmpty(item))
                        GuiDataAndWhitelistItemsDatabase.WasteRes.Add(item);
                }
                foreach (var item in configNode?.GetValuesList("padIgnoreResource"))
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        GuiDataAndWhitelistItemsDatabase.PadIgnoreRes.Add(item);
                        GuiDataAndWhitelistItemsDatabase.HangarIgnoreRes.Add(item);
                    }
                }
                foreach (var item in configNode?.GetValuesList("hangarIgnoreResource"))
                {
                    if (!string.IsNullOrEmpty(item))
                        GuiDataAndWhitelistItemsDatabase.HangarIgnoreRes.Add(item);
                }
            }

            KerbalConstructionTime.KCTCostModifiers.Clear();
            var nodes = GameDatabase.Instance.GetConfigNodes("KCTTAGS")?.FirstOrDefault();
            foreach (var tagNode in nodes?.GetNodes("TAG") ?? Enumerable.Empty<ConfigNode>())
            {
                KCTCostModifier x = new KCTCostModifier();
                if (ConfigNode.LoadObjectFromConfig(x, tagNode) && !string.IsNullOrEmpty(x.name))
                {
                    if (string.IsNullOrEmpty(x.displayName))
                        x.displayName = x.name;
                    KerbalConstructionTime.KCTCostModifiers[x.name] = x;
                }
            }

            KerbalConstructionTime.TechNodePeriods.Clear();
            nodes = GameDatabase.Instance.GetConfigNodes("KCT_TECH_NODE_PERIODS")?.FirstOrDefault();
            foreach (var node in nodes?.GetNodes("TECHNode") ?? Enumerable.Empty<ConfigNode>())
            {
                KCTTechNodePeriod x = new KCTTechNodePeriod();
                if (ConfigNode.LoadObjectFromConfig(x, node) && !string.IsNullOrEmpty(x.id))
                {
                    KerbalConstructionTime.TechNodePeriods[x.id] = x;
                }
            }

            KerbalConstructionTime.NodeTypes.Clear();
            ConfigNode typeNode = GameDatabase.Instance.GetConfigNode("KCT_TECH_NODE_TYPES");
            if (typeNode != null)
                KerbalConstructionTime.NodeTypes.Load(typeNode);
        }

        public override bool IsReady() => LoadingScreen.Instance?.loaders != null;

        public override float ProgressFraction() => 0;

        public override string ProgressTitle() => "KerbalConstructionTime Initialization & Setup";

        public override void StartLoad()
        {
            LoadCustomItems();
        }
    }
}
