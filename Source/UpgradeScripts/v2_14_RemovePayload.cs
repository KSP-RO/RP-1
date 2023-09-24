using System;
using System.Linq;
using SaveUpgradePipeline;
using UnityEngine;

namespace RP0.UpgradeScripts
{

    [UpgradeModule(LoadContext.Craft, craftNodeUrl = "PART/RESOURCE")]
    public class v2_14_1RemovePayload : UpgradeScript
    {
        public override string Name { get => "RP-1 Payload Upgrader"; }
        public override string Description { get => "Removes WeatherSatPayload, ComSatPayload and NavSatPayload from older Tanks"; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(2, 14, 0);
        public override Version TargetVersion => _targetVersion;

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            ConfigNode.Value v = null;
            if ((v = node.values.values.Find(s => s.name == "name")) != null)
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
            if ((v = node.values.values.Find(s => s.name == "name")) != null)
            {
                if (v.value == "ComSatPayload" || v.value == "NavSatPayload" || v.value == "WeatherSatPayload")
                {
                    node.SetValue("maxAmount", 0f);
                    node.SetValue("amount", 0f);
                    Debug.Log($"[RP-1] UpgradePipeline removed {v.value} from {node.name}");
                }
            }
        }
    }
}

