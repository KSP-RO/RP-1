using SaveUpgradePipeline;
using System;
using System.Collections.Generic;

namespace RP0.UpgradeScripts
{
    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO")]
    public class v3_1_EarlySolids : UpgradeScript
    {
        public override string Name { get => "RP-1 Early Solid Node Removal"; }
        public override string Description { get => "Removes the Early Solids, Basic Solids, and 1956 Solids nodes from acquired tech, and unlocks the corresponding replacement nodes. "; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(3, 1, 0);
        public override Version TargetVersion => _targetVersion;

        public static readonly Dictionary<string, string[]> NodeSwaps = new Dictionary<string, string[]> 
        { 
            { "earlySolids", new string[] {"rocketryTesting"} }, 
            { "basicSolids", new string[] {"basicRocketryRP0", "earlyRocketry"} }, 
            { "solids1956", new string[] {"orbitalRocketry1956"} }
        };

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
                string target = kvp.Value[0];
                string[] allTargets = kvp.Value;
                if (techs.TryGetValue(source, out ConfigNode sourceNode))
                {
                    foreach (string tech in allTargets)
                    {
                        if (!techs.ContainsKey(tech))
                        {
                            ConfigNode techNode = node.AddNode("Tech");
                            techNode.AddValue("id", tech);
                            techNode.AddValue("state", RDTech.State.Available);
                            techNode.AddValue("cost", 0); // placeholder
                            techs[tech] = techNode;
                        }
                    }
                    foreach (var part in sourceNode.GetValues("Part"))
                    {
                        techs[target].AddValue("Part", part);
                    }
                    // good-bye!!
                    node.RemoveNode(sourceNode);
                    RP0Debug.Log($"{Name} removed {source} node");
                }
            }
        }
    }
}