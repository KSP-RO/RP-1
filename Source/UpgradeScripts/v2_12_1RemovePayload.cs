using System;
using System.Linq;
using SaveUpgradePipeline;
using UnityEngine;

namespace RP0.UpgradeScripts
{

    [UpgradeModule(LoadContext.Craft, craftNodeUrl = "PART/RESOURCE")]
    public class v2_12_1RemovePayload : UpgradeScript
    {
        public override string Name { get => "RP-1 Payload Upgrader"; }
        public override string Description { get => "Removes WeatherSatPayload, ComSatPayload and NavSatPayload from older Tanks"; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(2, 12, 1);
        public override Version TargetVersion => _targetVersion;

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            ConfigNode.Value v = null;
            if (node.name == "RESOURCE" && (v = node.values.values.Find(s => s.name == "name")) != null)
            {
                if (v.value == "ComSatPayload" || v.value == "NavSatPayload" || v.value == "WeatherSatPayload")
                {
                    return TestResult.Upgradeable;
                }
            }

            return TestResult.Pass;
        }

        //upgrade the craft
        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            ConfigNode.Value v = null;
            if (node.name == "RESOURCE" && (v = node.values.values.Find(s => s.name == "name")) != null)
            {
                if (v.value == "ComSatPayload" || v.value == "NavSatPayload" || v.value == "WeatherSatPayload")
                {
                    Debug.Log($"[RP-1] UpgradePipeline removed {v.value} from {node.name}");
                    node.ClearData();
                }
            }
        }
    }
}
