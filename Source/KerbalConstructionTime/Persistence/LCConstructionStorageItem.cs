using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCConstructionStorageItem : IConfigNode
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
        public bool isModify = false;

        [Persistent]
        public int buildListIndex = -1;

        [Persistent]
        public LCItem.LCData lcData = new LCItem.LCData();

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }

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
                IsModify = isModify,
                BuildListIndex = buildListIndex,
                LCData = new LCItem.LCData(lcData)
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
            isModify = lcc.IsModify;
            buildListIndex = lcc.BuildListIndex;
            lcData = new LCItem.LCData(lcc.LCData);
            return this;
        }
    }
}
