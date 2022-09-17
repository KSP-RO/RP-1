using System;

namespace RP0
{
    public class FacilityConstruction : IConfigNode
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
