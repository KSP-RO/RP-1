using SaveUpgradePipeline;
using System;
using System.Collections.Generic;

namespace RP0.UpgradeScripts
{
    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO")]
    public class v4_8_MoveEarlySolidParts : UpgradeScript
    {
        public override string Name { get => "RP-1 Early Solid Part Movement"; }
        public override string Description { get => "Moves the parts in "; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(4, 8, 0);
        public override Version TargetVersion => _targetVersion;

        public static readonly Dictionary<string, string> NodeSwaps = new Dictionary<string, string> 
        { 
            { "earlySolids", "rocketryTesting" }, 
            { "basicSolids", "basicRocketryRP0" }, 
            { "solids1956", "orbitalRocketry1956" }
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
                string target = kvp.Value;
                if (techs.TryGetValue(source, out ConfigNode sourceNode) && techs.TryGetValue(target, out ConfigNode targetNode))
                {
                    foreach (var part in sourceNode.GetValues("Part"))
                    {
                        targetNode.AddValue("Part", part);
                    }
                    // good-bye!!
                    node.RemoveNode(sourceNode);
                    RP0Debug.Log($"{Name} removed {source} node");
                }
            }
        }
    }
}