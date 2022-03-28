namespace RP0
{
    public class MaintenanceSettings : IConfigNode
    {
        [Persistent]
        public double facilityLevelCostMult = 0.00002d;

        [Persistent]
        public double facilityLevelCostPow = 1d;

        [Persistent]
        public double kctBPMult = 20d;

        [Persistent]
        public double kctResearchMult = 100d * 86400d;

        [Persistent]
        public double nautYearlyUpkeepAdd = 5000d;

        [Persistent]
        public double nautYearlyUpkeepBase = 500d;

        [Persistent]
        public double nautInFlightDailyRate = 100d;

        [Persistent]
        public double nautOrbitProficiencyDailyRate = 20d;

        [Persistent]
        public double freeCoursesPerLevel = 0.5d;

        [Persistent]
        public double courseMultiplierDivisor = 3d;

        [Persistent]
        public FloatCurve subsidyCurve = new FloatCurve();

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);

            var fc = new FloatCurve();
            fc.Load(node.GetNode("subsidyCurve"));
            subsidyCurve = fc;
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
