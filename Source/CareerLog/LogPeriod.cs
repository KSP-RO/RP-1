namespace RP0
{
    public class LogPeriod : IConfigNode
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
        public double ProgramFunds;

        [Persistent]
        public double ContractRewards;

        [Persistent]
        public double OtherFundsEarned;

        [Persistent]
        public double ScienceEarned;

        [Persistent]
        public double LaunchFees;

        [Persistent]
        public double MaintenanceFees;

        [Persistent]
        public double ToolingFees;

        [Persistent]
        public double EntryCosts;

        [Persistent]
        public double ConstructionFees;

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
        public double EfficiencyResearchers;

        [Persistent]
        public double EfficiencyEngineers;

        [Persistent]
        public double FundsGainMult;

        [Persistent]
        public int NumNautsKilled;

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

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
