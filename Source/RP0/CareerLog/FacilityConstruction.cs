using System;
using RP0.DataTypes;

namespace RP0
{
    public class FacilityConstruction : ConfigNodePersistenceBase, IConfigNode
    {
        [Persistent]
        public SpaceCenterFacility Facility;

        [Persistent]
        public int NewLevel;

        [Persistent]
        public double Cost;

        [Persistent]
        public Guid FacilityID;

        public FacilityConstruction()
        {
        }

        public FacilityConstruction(ConfigNode n)
        {
            Load(n);
        }
    }
}
