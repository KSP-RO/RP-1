using System;
using System.Collections.Generic;
using UniLinq;
using SaveUpgradePipeline;
using UnityEngine;

namespace RP0.UpgradeScripts
{
    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO")]
    public class v4_6_EarlySolids : UpgradeScript
    {
        public override string Name { get => "RP-1 Early Solid Node Removal"; }
        public override string Description { get => "Removes the Early Solids, Basic Solids, and 1956 Solids nodes from acquired tech. "; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(4, 6, 0);
        public override Version TargetVersion => _targetVersion;

        private static readonly Dictionary<string, string> nodes = new Dictionary<string, string> { {"earlySolids", "rocketryTesting"}, {"basicSolids", "basicRocketryRP0"}, {"solids1956", "orbitalRocketry1956"} };

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            return node.GetValue("name") == "ResearchAndDevelopment" ? TestResult.Upgradeable : TestResult.Pass;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            node.GetNodes("Tech");
        }
    }
}