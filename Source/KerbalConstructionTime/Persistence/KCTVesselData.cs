using System;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class KCTVesselData : ConfigNodePersistenceBase, IConfigNode
    {
        [Persistent]
        public EditorFacility FacilityBuiltIn;

        [Persistent]
        public string VesselID = string.Empty;

        [Persistent]
        public string LaunchID = string.Empty;

        [Persistent]
        public Guid LCID = Guid.Empty;

        [Persistent]
        public Guid LCModID = Guid.Empty;

        [Persistent]
        public bool HasStartedReconditioning = false;

        public bool IsInitialized => FacilityBuiltIn != default || VesselID != string.Empty || LaunchID != string.Empty;

        public static KCTVesselData Parse(KCTVesselTracker d)
        {
            return new KCTVesselData
            {
                FacilityBuiltIn = d.Data.FacilityBuiltIn,
                VesselID = d.Data.VesselID,
                LaunchID = d.Data.LaunchID,
                LCID = d.Data.LCID,
                LCModID = d.Data.LCModID,
                HasStartedReconditioning = d.Data.HasStartedReconditioning
            };
        }

        public KCTVesselData()
        {
        }

        public KCTVesselData(ConfigNode n)
        {
            Load(n);
        }

        public void SetFrom(KCTVesselData data)
        {
            FacilityBuiltIn = data.FacilityBuiltIn;
            LaunchID = data.LaunchID;
            VesselID = data.VesselID;
            LCID = data.LCID;
            LCModID = data.LCModID;
            HasStartedReconditioning = data.HasStartedReconditioning;
        }
    }
}
