using System;
using System.Collections.Generic;
using System.Linq;

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
        public double OtherFees;

        [Persistent]
        public int VABUpgrades;

        [Persistent]
        public int SPHUpgrades;

        [Persistent]
        public int RnDUpgrades;

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
