namespace RP0
{
    public class MaintenanceSettings
    {
        [Persistent]
        public double facilityLevelCostMult = 0.00002d;

        [Persistent]
        public double facilityLevelCostPow = 1d;

        [Persistent]
        public int salaryEngineers = 1000;

        [Persistent]
        public int salaryResearchers = 1000;

        [Persistent]
        public double nautYearlyUpkeepAdd = 5000d;

        [Persistent]
        public double nautYearlyUpkeepBase = 500d;

        [Persistent]
        public double nautInFlightDailyRate = 100d;

        [Persistent]
        public double nautOrbitProficiencyUpkeepAdd = 20d;

        [Persistent]
        public double nautSubOrbitProficiencyUpkeepAdd = 20d;
        [Persistent]
        public double nautTrainingCostMultiplier = 100d;

        [Persistent]
        public double freeCoursesPerLevel = 0.5d;

        [Persistent]
        public double courseMultiplierDivisor = 3d;

        [Persistent]
        public double repToSubsidyConversion = 100d;

        [Persistent]
        public double subsidyMultiplierForMax = 2d;

        [Persistent]
        public FloatCurve subsidyCurve = new FloatCurve();
    }
}
