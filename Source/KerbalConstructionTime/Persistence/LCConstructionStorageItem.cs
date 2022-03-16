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

        [Persistent]
        public int buildListIndex = -1;

        public LCConstruction ToLCConstruction()
        {
            return new LCConstruction
            {
                LaunchComplexIndex = launchComplexID,
                Name = name,
                Progress = progress,
                BP = BP,
                Cost = cost,
                UpgradeProcessed = upgradeProcessed,
                BuildListIndex = buildListIndex
            };
        }

        public LCConstructionStorageItem FromLCConstruction(LCConstruction lcc)
        {
            launchComplexID = lcc.LaunchComplexIndex;
            name = lcc.Name;
            progress = lcc.Progress;
            BP = lcc.BP;
            cost = lcc.Cost;
            upgradeProcessed = lcc.UpgradeProcessed;
            buildListIndex = lcc.BuildListIndex;
            return this;
        }
    }
}
