using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCConstructionStorageItem
    {
        [Persistent]
        public int launchComplexID = 0;

        [Persistent]
        public string name;

        [Persistent]
        public double progress = 0, BP = 0, cost = 0;

        [Persistent]
        public bool upgradeProcessed = false;

        public LCConstruction ToLCConstruction()
        {
            return new LCConstruction
            {
                LaunchComplexIndex = launchComplexID,
                Name = name,
                Progress = progress,
                BP = BP,
                Cost = cost,
                UpgradeProcessed = upgradeProcessed
            };
        }

        public LCConstructionStorageItem FromLCConstruction(LCConstruction pc)
        {
            launchComplexID = pc.LaunchComplexIndex;
            name = pc.Name;
            progress = pc.Progress;
            BP = pc.BP;
            cost = pc.Cost;
            upgradeProcessed = pc.UpgradeProcessed;
            return this;
        }
    }
}
