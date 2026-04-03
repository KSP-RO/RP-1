using ROUtils.DataTypes;
using System;
using UnityEngine;

namespace RP0
{
    public class PendingLaunchData : ConfigNodePersistenceBase, IConfigNode
    {
        [Persistent]
        public double EffectiveCost;
        [Persistent]
        public double BuildPoints;
        [Persistent]
        public string LaunchSite;
        [Persistent]
        public string ShipName;
        [Persistent]
        public Guid ShipID;
        [Persistent]
        public bool HumanRated;
        [Persistent]
        public float Cost = 0;
        [Persistent]
        public float Mass = 0;
        [Persistent]
        public int NumStages = 0;
        [Persistent]
        public int NumStageParts = 0;
        [Persistent]
        public double StagePartCost = 0;
        [Persistent]
        public float EmptyCost = 0;
        [Persistent]
        public float EmptyMass = 0;
        [Persistent]
        public EditorFacility FacilityBuiltIn;
        [Persistent]
        public string KCTPersistentID;
        [Persistent]
        public Vector3 ShipSize = Vector3.zero;
        [Persistent]
        public PersistentList<StageStats> Stages = new PersistentList<StageStats>();

        public PendingLaunchData() { }

        public PendingLaunchData(VesselProject vp)
        {
            EffectiveCost = vp.effectiveCost;
            BuildPoints = vp.buildPoints;
            LaunchSite = vp.launchSite;
            ShipName = vp.shipName;
            ShipID = vp.shipID;
            HumanRated = vp.humanRated;
            Cost = vp.cost;
            Mass = vp.mass;
            NumStages = vp.numStages;
            NumStageParts = vp.numStageParts;
            StagePartCost = vp.stagePartCost;
            EmptyCost = vp.emptyCost;
            EmptyMass = vp.emptyMass;
            FacilityBuiltIn = vp.FacilityBuiltIn;
            KCTPersistentID = vp.KCTPersistentID;
            ShipSize = vp.ShipSize;
        }
    }
}
