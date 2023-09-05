using KerbalConstructionTime;
using System;
using UnityEngine;
using RP0.DataTypes;

namespace RP0
{
    public class LCLogItem : ConfigNodePersistenceBase, IConfigNode
    {
        [Persistent]
        public string Name;

        [Persistent]
        public float MassMax;

        [Persistent]
        public float MassOrig;

        [Persistent]
        public Vector3 SizeMax;

        [Persistent]
        public LaunchComplexType LcType;

        [Persistent]
        public bool IsHumanRated;

        [Persistent]
        public Guid ID;

        [Persistent]
        public Guid ModID;

        [Persistent]
        public double ModCost;

        public LCLogItem()
        {
        }

        public LCLogItem(ConfigNode n)
        {
            Load(n);
        }

        public LCLogItem(LCItem lc)
        {
            Name = lc.Name;
            MassMax = lc.MassMax;
            MassOrig = lc.MassOrig;
            SizeMax = lc.SizeMax;
            LcType = lc.LCType;
            IsHumanRated = lc.IsHumanRated;
            ID = lc.ID;
            ModID = lc.ModID;
        }

        public LCLogItem(LCConstruction data)
        {
            Name = data.name;
            MassMax = data.lcData.massMax;
            MassOrig = data.lcData.massOrig;
            SizeMax = data.lcData.sizeMax;
            LcType = data.lcData.lcType;
            IsHumanRated = data.lcData.isHumanRated;
            ID = data.lcID;
            ModID = data.modId;
            ModCost = data.cost;
        }
    }
}
