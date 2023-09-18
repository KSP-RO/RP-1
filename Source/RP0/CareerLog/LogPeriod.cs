using RP0.DataTypes;

namespace RP0
{
    public class LogPeriod : ConfigNodePersistenceBase, IConfigNode
    {
        [Persistent]
        public double StartUT;

        [Persistent]
        public double EndUT;

        [Persistent]
        public double CurrentFunds;

        [Persistent]
        public double CurrentSci;

        [Persistent]
        public int RnDQueueLength;

        [Persistent]
        public double ProgramFunds;

        [Persistent]
        public double OtherFundsEarned;

        [Persistent]
        public double ScienceEarned;

        [Persistent]
        public double SalaryEngineers;

        [Persistent]
        public double SalaryResearchers;

        [Persistent]
        public double SalaryCrew;

        [Persistent]
        public double LaunchFees;

        [Persistent]
        public double VesselPurchase;

        [Persistent]
        public double VesselRecovery;

        [Persistent]
        public double LCMaintenance;

        [Persistent]
        public double FacilityMaintenance;

        /// <summary>
        /// All maintenance minus subsidy
        /// </summary>
        [Persistent]
        public double MaintenanceFees;

        [Persistent]
        public double TrainingFees;

        [Persistent]
        public double ToolingFees;

        [Persistent]
        public double EntryCosts;

        [Persistent]
        public double SpentUnlockCredit;

        [Persistent]
        public double ConstructionFees;

        [Persistent]
        public double HiringResearchers;

        [Persistent]
        public double HiringEngineers;

        [Persistent]
        public double OtherFees;

        [Persistent]
        public double SubsidySize;

        [Persistent]
        public double SubsidyPaidOut;

        [Persistent]
        public double RepFromPrograms;

        [Persistent]
        public int NumEngineers;

        [Persistent]
        public int NumResearchers;

        [Persistent]
        public double EfficiencyEngineers;

        [Persistent]
        public double FundsGainMult;

        [Persistent]
        public int NumNautsKilled;

        [Persistent]
        public double Confidence;

        [Persistent]
        public double Reputation;

        [Persistent]
        public double HeadlinesHype;

        public LogPeriod()
        {
        }

        public LogPeriod(double startUT, double endUT)
        {
            StartUT = startUT;
            EndUT = endUT;
        }

        public LogPeriod(ConfigNode n)
        {
            Load(n);
        }
    }
}
