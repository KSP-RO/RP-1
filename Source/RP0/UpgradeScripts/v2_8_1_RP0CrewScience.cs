using System;
using SaveUpgradePipeline;

namespace RP0.UpgradeScripts
{
    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME")]
    public class v2_8_1_RP0CrewScience : UpgradeScript
    {
        public override string Name { get => "RP-1 Crew Science ID Upgrader"; }
        public override string Description { get => "Updates the ID of various experiments"; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(2, 8, 1);
        public override Version TargetVersion => _targetVersion;

        private bool TestID(string id)
        {
            if (id == null)
                return false;

            switch (id)
            {
                case "RP0-LiquidsMicrogravity":
                case "RP0-VisualTracking":
                case "RP0-FlightControl":
                case "RP0-EarthPhotography":
                case "RP0-SupersonicLow1":
                case "RP0-SupersonicLow2":
                case "RP0-SupersonicHigh1":
                case "RP0-SupersonicHigh2":
                case "RP0-TelevisionBroadcast":
                case "RP0-IonSensingAltitudeControl":
                case "RP0-NightImageIntensification":
                case "RP0-TerrainPhotography":
                case "RP0-WeatherPhotography":
                case "RP0-OrbitalManeuvering":
                case "RP0-VisualAcuity":
                case "RP0-StarOccultationNav":
                case "RP0-PowerToolEvaluation":
                case "RP0-EggGrowth":
                case "RP0-BloodCells":
                case "RP0-SleepAnalysis":
                case "RP0-FoodEvaluation":
                case "RP0-WorkTolerance":
                case "RP0-SimpleNavigation":
                case "RP0-ZodiacalLightPhoto":
                case "RP0-VHFPolarization":
                    return true;

                default:
                    return false;
            }
        }

        private string GetExperimentID(string str)
        {
            if (str == null)
                return null;

            int idx = str.IndexOf('@');
            if (idx == -1)
                return null;
            return str.Substring(0, idx);
        }


        private TestResult TestRecurse(ConfigNode node)
        {
            if (node.name == "Science")
            {
                if (TestID(GetExperimentID(node.GetValue("id"))))
                    return TestResult.Upgradeable;
            }

            if (node.name == "drive")
            {
                string sends = node.GetValue("sendFileNames");
                if (sends != null)
                {
                    foreach (var split in sends.Split(','))
                    {
                        if (TestID(GetExperimentID(split)))
                            return TestResult.Upgradeable;
                    }
                }
            }

            // Fallback case, test the node name
            {
                if (TestID(GetExperimentID(node.name)))
                    return TestResult.Upgradeable;
            }

            foreach (ConfigNode n in node.nodes)
            {
                var res = TestRecurse(n);
                if (res != TestResult.Pass)
                    return res;
            }

            return TestResult.Pass;
        }

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            return TestRecurse(node);
        }

        private void UpgradeRecurse(ConfigNode node)
        {
            ConfigNode.Value v = null;
            if (node.name == "Science" && (v = node.values.values.Find(s => s.name == "id")) != null)
            {
                v.value = v.value.Replace("RP0-", "RP0");
                return;
            }

            if (node.name == "drive" && (v = node.values.values.Find(s => s.name == "sendFileNames")) != null)
            {
                v.value = v.value.Replace("RP0-", "RP0");
                return;
            }

            // Fallback case, node name itself
            if (TestID(GetExperimentID(node.name)))
            {
                node.name = node.name.Replace("RP0-", "RP0");
                return;
            }

            foreach (ConfigNode n in node.nodes)
                UpgradeRecurse(n);
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            UpgradeRecurse(node);
        }
    }
}
