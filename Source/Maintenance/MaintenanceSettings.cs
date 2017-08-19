using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;
using System.Reflection;

namespace RP0
{
    public class MaintenanceSettings : IConfigNode
    {
        [Persistent]
        public double facilityLevelCostMult = 0.0000005d;

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
