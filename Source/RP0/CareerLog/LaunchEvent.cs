using UnityEngine;

namespace RP0
{
    public class LaunchEvent : CareerEvent
    {
        [Persistent]
        public string VesselName;
        [Persistent]
        public string VesselUID;
        [Persistent]
        public string LaunchID;
        [Persistent]
        public EditorFacility BuiltAt;
        [Persistent]
        public string LCID;
        [Persistent]
        public string LCModID;
        [Persistent]
        public double EffectiveCost;
        [Persistent]
        public double BuildPoints;
        [Persistent]
        public string LaunchSite;
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
        public Vector3 ShipSize = Vector3.zero;

        /// <summary>
        /// Used only for deserialization.
        /// </summary>
        public LaunchEvent()
        {
        }

        public LaunchEvent(double UT, PendingLaunchData pending) : base(UT)
        {
            CopyDataFromPendingLaunch(pending);
        }

        public LaunchEvent(ConfigNode n) : base(n)
        {
        }

        private void CopyDataFromPendingLaunch(PendingLaunchData data)
        {
            if (data == null) return;

            EffectiveCost = data.EffectiveCost;
            BuildPoints = data.BuildPoints;
            LaunchSite = data.LaunchSite;
            HumanRated = data.HumanRated;
            Cost = data.Cost;
            Mass = data.Mass;
            NumStages = data.NumStages;
            NumStageParts = data.NumStageParts;
            StagePartCost = data.StagePartCost;
            EmptyCost = data.EmptyCost;
            EmptyMass = data.EmptyMass;
            ShipSize = data.ShipSize;
        }
    }
}
