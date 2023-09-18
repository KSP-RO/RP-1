using System;
using SaveUpgradePipeline;
using UnityEngine;

namespace RP0.UpgradeScripts
{
    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO")]
    public class v2_3_UpgradeUnlockCredit : UpgradeScript
    {
        public override string Name { get => "RP-1 Unlock Credit Upgrader"; }
        public override string Description { get => "Updates Unlock Credit from per-node to global"; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(2, 3, 0);
        public override Version TargetVersion => _targetVersion;

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            nodeName = node.GetValue("name");
            return nodeName == "UnlockSubsidyHandler" ? TestResult.Upgradeable : TestResult.Pass;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            string name = node.GetValue("name");
            if (name != "UnlockSubsidyHandler")
                return;

            node.SetValue("name", "UnlockCreditHandler");
            double totalCredit = 0;
            foreach (ConfigNode n in node.nodes)
            {
                double credit = 0d;
                n.TryGetValue("funds", ref credit);
                totalCredit += Math.Max(0d, credit);
            }
            node.AddValue("_totalCredit", totalCredit);

            RP0Debug.Log($"UpgradePipeline context {loadContext} updated UnlockSubsidyHandler to UnlockCreditHandler, total credit {totalCredit:N0}");
        }
    }
}
