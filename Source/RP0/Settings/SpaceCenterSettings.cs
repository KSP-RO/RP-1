using ROUtils.DataTypes;
using System.Collections.Generic;

namespace RP0
{
    public class SpaceCenterSettings : ConfigNodePersistenceBase
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
        public PersistentDictionaryValueTypes<string, double> nautYearlyUpkeepPerTraining = new PersistentDictionaryValueTypes<string, double>();
        public List<string> nautUpkeepTrainings = new List<string>();
        public List<bool> nautUpkeepTrainingBools = new List<bool>();

        [Persistent]
        public double nautInFlightDailyRate = 100d;

        [Persistent]
        public double nautInactiveMult = 0.5d;

        [Persistent]
        public double nautTrainingTypeCostMult = 0.25d;

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

        [Persistent]
        public string VABRecoveryTech = null;
        [Persistent]
        public int HireCost = 200;
        [Persistent]
        public double AdditionalPadCostMult = 0.5d, RushRateMult = 1.5d, RushSalaryMult = 2d, IdleSalaryMult = 0.25, MergingTimePenalty = 0.05d,
            EffectiveCostPerLiterPerResourceMult = 0.1d;
        [Persistent]
        public FloatCurve EngineerSkillupRate = new FloatCurve();
        [Persistent]
        public FloatCurve ConstructionRushCost = new FloatCurve();
        [Persistent]
        public FloatCurve YearBasedRateMult = new FloatCurve();
        [Persistent]
        public EfficiencyUpgrades LCEfficiencyUpgradesMin = new EfficiencyUpgrades();
        [Persistent]
        public EfficiencyUpgrades LCEfficiencyUpgradesMax = new EfficiencyUpgrades();
        [Persistent]
        public EfficiencyUpgrades ResearcherEfficiencyUpgrades = new EfficiencyUpgrades();
        [Persistent]
        public FloatCurve ScienceResearchEfficiency = new FloatCurve();

        [Persistent]
        public PersistentListValueType<int> StartingPersonnel = new PersistentListValueType<int>();
        [Persistent]
        public PersistentListValueType<int> ResearcherCaps = new PersistentListValueType<int>();

        [Persistent]
        public ApplicantsFromContracts ContractApplicants = new ApplicantsFromContracts();

        [Persistent]
        public PersistentDictionaryValueTypes<string, double> Part_Variables = new PersistentDictionaryValueTypes<string, double>();
        [Persistent]
        public PersistentDictionaryValueTypes<string, double> Resource_Variables = new PersistentDictionaryValueTypes<string, double>();

        public double LCEfficiencyMin => LCEfficiencyUpgradesMin.GetSum();
        public double LCEfficiencyMax => LCEfficiencyUpgradesMax.GetSum();
        public double ResearcherEfficiency => ResearcherEfficiencyUpgrades.GetMultiplier()
            * Formula.GetScienceResearchEfficiencyMult(SpaceCenterManagement.Instance.SciPointsTotal);

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            foreach (var k in nautYearlyUpkeepPerTraining.Keys)
            {
                nautUpkeepTrainings.Add(k);
                nautUpkeepTrainingBools.Add(false);
            }
        }

        public void ResetBools()
        {
            for (int i = nautUpkeepTrainingBools.Count; i-- > 0;)
                nautUpkeepTrainingBools[i] = false;
        }

        public int GetStartingPersonnel(Game.Modes mode)
        {
            if (mode == Game.Modes.CAREER)
            {
                return StartingPersonnel[0];
            }
            else
            {
                return StartingPersonnel[1];
            }
        }

        public double GetPartVariable(string partName)
        {
            if (Part_Variables.TryGetValue(partName, out double d))
                return d;
            return 1d;
        }

        public double GetValueModifier(Dictionary<string, double> dict, IEnumerable<string> tags)
        {
            if (dict == null)
                return 1d;

            double value = 1d;
            foreach (var name in tags)
            {
                if (dict.TryGetValue(name, out double d))
                    value *= d;
            }
            return value;

        }

        public double GetValueModifierMax(Dictionary<string, double> dict, IEnumerable<string> tags)
        {
            if (dict == null)
                return 1d;

            double value = 1d;
            foreach (var name in tags)
            {
                if (dict.TryGetValue(name, out double d))
                    value = System.Math.Max(value, d);
            }
            return value;

        }

        //These are all multiplied in case multiple variables exist on one part
        public double GetResourceVariablesMult(List<string> resourceNames) => GetValueModifier(Resource_Variables, resourceNames);

        public double GetResourceVariablesMult(PartResourceList resources)
        {
            double value = 1d;
            foreach (PartResource r in resources)
            {
                if (Resource_Variables.TryGetValue(r.resourceName, out double d))
                    value *= d;
            }
            return value;
        }

        public double GetResourceVariableMult(string resName)
        {
            if (Resource_Variables.TryGetValue(resName, out double m))
                return m;
            return 1d;
        }
    }

    public class ApplicantsFromContracts : EfficiencyUpgrades, IConfigNode
    {
        public int GetApplicantsFromContract(string contract) => (int)GetValue(contract);
    }

    public class EfficiencyUpgrades : PersistentDictionaryValueTypes<string, double>, IConfigNode
    {
        public double GetMultiplier()
        {
            double mult = 1d;
            foreach (var kvp in this)
            {
                if (ResearchAndDevelopment.GetTechnologyState(kvp.Key) == RDTech.State.Available)
                    mult += kvp.Value;
            }

            return mult;
        }

        public double GetSum()
        {
            double sum = 0d;
            foreach (var kvp in this)
            {
                if (ResearchAndDevelopment.GetTechnologyState(kvp.Key) == RDTech.State.Available)
                    sum += kvp.Value;
            }
            return sum;
        }

        public double GetValue(string tech)
        {
            double val;
            if (TryGetValue(tech, out val))
                return val;

            return 0d;
        }
    }
}
