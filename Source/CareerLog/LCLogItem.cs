using KerbalConstructionTime;
using System;
using UnityEngine;

namespace RP0
{
    public class LCLogItem : IConfigNode
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
            Name = data.Name;
            MassMax = data.LCData.massMax;
            MassOrig = data.LCData.massOrig;
            SizeMax = data.LCData.sizeMax;
            LcType = data.LCData.lcType;
            IsHumanRated = data.LCData.isHumanRated;
            ID = data.LCID;
            ModID = data.ModID;
            ModCost = data.Cost;
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            node.TryGetValue(nameof(ID), ref ID);
            node.TryGetValue(nameof(ModID), ref ModID);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
            node.AddValue(nameof(ID), ID);
            node.AddValue(nameof(ModID), ModID);
        }
    }
}
