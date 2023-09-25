﻿using RP0.DataTypes;

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
        public PersistentSortedListValueTypes<int, double> researchersToUnlockCreditSalaryMultipliers = new PersistentSortedListValueTypes<int, double>();

        [Persistent]
        public double hangarCostForMaintenanceOffset = 240000d;

        [Persistent]
        public double hangarCostForMaintenanceMin = 20000d;

        [Persistent]
        public double lcCostMultiplier = 2d;

        [Persistent]
        public PersistentListValueType<double> nautYearlyUpkeepPerFacLevel = new PersistentListValueType<double>();

        [Persistent]
        public double nautInFlightDailyRate = 100d;

        [Persistent]
        public double nautOrbitProficiencyUpkeepAdd = 20d;

        [Persistent]
        public double nautInactiveMult = 0.5d;

        [Persistent]
        public double nautSubOrbitProficiencyUpkeepAdd = 20d;

        [Persistent]
        public PersistentListValueType<double> nautTrainingCostPerFacLevel = new PersistentListValueType<double>();

        [Persistent]
        public double repToSubsidyConversion = 100d;

        [Persistent]
        public double subsidyMultiplierForMax = 2d;

        [Persistent]
        public double repPortionLostPerDay = 0.9995d;

        [Persistent]
        public FloatCurve subsidyCurve = new FloatCurve();
    }
}
