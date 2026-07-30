using System;
using System.Linq;
using SaveUpgradePipeline;
using UnityEngine;

namespace RP0.UpgradeScripts
{
    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO/Tech")]
    public class v4_6_EarlySolids : UpgradeScript
    {
        public override string Name { get => "RP-1 Early Solid Node Removal"; }
        public override string Description { get => "Removes the Early Solids, Basic Solids, and 1956 Solids nodes from acquired tech. "; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(4, 6, 0);
        public override Version TargetVersion => _targetVersion;

        private static string[] nodes = { "earlySolids", "basicSolids", "solids1956" };

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            return nodes.Contains(node.GetValue("id")) ? TestResult.Upgradeable : TestResult.Pass;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            
        }
    }
}