using System;
using System.Collections.Generic;
using UniLinq;
using SaveUpgradePipeline;
using UnityEngine;

namespace RP0.UpgradeScripts
{
    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO")]
    public class v3_1_EarlySolids : UpgradeScript
    {
        public override string Name { get => "RP-1 Early Solid Node Removal"; }
        public override string Description { get => "Removes the Early Solids, Basic Solids, and 1956 Solids nodes from acquired tech. "; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(3, 1, 0);
        public override Version TargetVersion => _targetVersion;

        public static readonly Dictionary<string, string> NodeSwaps = new Dictionary<string, string> { {"earlySolids", "rocketryTesting"}, {"basicSolids", "basicRocketryRP0"}, {"solids1956", "orbitalRocketry1956"} };

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            return node.GetValue("name") == "ResearchAndDevelopment" ? TestResult.Upgradeable : TestResult.Pass;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            var techs = new Dictionary<string, ConfigNode>();
            foreach (var tech in node.GetNodes("Tech"))
            {
                techs[tech.GetValue("id")] = tech;
            }
            foreach (var kvp in NodeSwaps)
            {
                string source = kvp.Key;
                string target = kvp.Value;
                if (techs.TryGetValue(source, out var sourceNode))
                {
                    if (techs.TryGetValue(target, out var targetNode))
                    {
                        // if relevant, move source Parts into target node
                        foreach (var part in sourceNode.GetValues("Part"))
                        {
                            targetNode.AddValue("Part", part);
                        }
                    }
                    // good-bye!!
                    node.RemoveNode(sourceNode);
                }
            }
        }
    }

    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO/TechList")]
    public class v3_1_RnD_EarlySolids : UpgradeScript
    {
        public override string Name => "RP-1 Early Solid Node Research Compensation";

        public override string Description => "Removes the early solid nodes' corresponding research projects.";

        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(3, 1, 0);
        public override Version TargetVersion => _targetVersion;

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            return TestResult.Upgradeable;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            foreach (var tech in node.GetNodes("ResearchProject"))
            {
                if (v3_1_EarlySolids.NodeSwaps.ContainsKey(tech.GetValue("techID")))
                {
                    node.RemoveNode(tech);
                }
            }
        }
    }
}